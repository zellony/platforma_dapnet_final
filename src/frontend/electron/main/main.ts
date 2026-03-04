import { app, BrowserWindow, Menu, globalShortcut, safeStorage, ipcMain, dialog, MenuItemConstructorOptions, session } from "electron";
import * as path from "path";
import * as fs from "fs";
import * as https from "https";
import { spawn, spawnSync, ChildProcess } from "child_process";
import { registerIpc } from "./ipc";

const BACKEND_FALLBACK_URL = "https://127.0.0.1:5001";
const RUNTIME_FILE_MAX_AGE_MS = 5 * 60 * 1000;
const isDbServiceMode = process.argv.includes("--db-service-mode");
const backendInstanceId = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
const RUNTIME_DISCOVERY_TIMEOUT_MS = isDbServiceMode ? 4000 : 10000;
const BACKEND_LIVE_TIMEOUT_MS = isDbServiceMode ? 10000 : 30000;
const BACKEND_READY_POLL_INTERVAL_MS = isDbServiceMode ? 120 : 300;

let backendBaseUrl = BACKEND_FALLBACK_URL;
let backendProcess: ChildProcess | null = null;
let mainWindow: BrowserWindow | null = null;
let devToolsAllowed = false;
const isLocalDevHost = (hostname: string) => hostname === "127.0.0.1" || hostname === "localhost";
type RuntimeSource = "local" | "shared";

function isDevToolsAllowed() { return !app.isPackaged || devToolsAllowed; }

function registerDevToolsShortcuts(): void {
    if (app.isPackaged) return;

    const openDevTools = () => {
        if (!mainWindow || mainWindow.isDestroyed()) return;
        mainWindow.webContents.openDevTools({ mode: "detach" });
    };

    globalShortcut.register("F12", openDevTools);
    globalShortcut.register("CommandOrControl+Shift+I", openDevTools);
}

function appendDevStartupLog(message: string): void {
    if (app.isPackaged) return;
    try {
        const repoRoot = path.resolve(app.getAppPath(), "..", "..", "..");
        const logsDir = path.join(repoRoot, "artifacts", "logs");
        fs.mkdirSync(logsDir, { recursive: true });
        const logPath = path.join(logsDir, "electron-startup.log");
        const line = `[${new Date().toISOString()}] ${message}\n`;
        fs.appendFileSync(logPath, line, "utf-8");
    } catch { }
}

function terminateBackendProcess(): void {
    if (!backendProcess) return;
    try {
        if (process.platform === "win32" && backendProcess.pid) {
            spawnSync("taskkill", ["/PID", String(backendProcess.pid), "/T", "/F"], {
                windowsHide: true,
                stdio: "ignore"
            });
        } else {
            backendProcess.kill();
        }
    } catch { }
    backendProcess = null;
}

function isWindowsAdmin(): boolean {
    if (process.platform !== "win32") return false;
    try {
        const check = spawnSync("net", ["session"], { stdio: "ignore", windowsHide: true });
        return check.status === 0;
    } catch {
        return false;
    }
}

async function launchDbServiceModeAsAdmin(): Promise<boolean> {
    if (process.platform !== "win32") return false;
    try {
        const executable = process.execPath;
        const appPath = app.getAppPath();
        const args = [appPath, "--db-service-mode"];

        appendDevStartupLog(`Service mode launch requested. exe=${executable}, appPath=${appPath}, isAdmin=${isWindowsAdmin()}`);

        if (isWindowsAdmin()) {
            // Already elevated: launch directly without UAC prompt.
            const child = spawn(executable, args, {
                detached: true,
                windowsHide: true,
                stdio: "ignore"
            });
            child.unref();
            const ok = !!child.pid;
            appendDevStartupLog(`Service mode launch (already admin) result=${ok}, pid=${child.pid ?? -1}`);
            return ok;
        }

        const escapedArgs = args.map(a => `'${a.replace(/'/g, "''")}'`).join(", ");
        const command = `Start-Process -Verb RunAs -FilePath '${executable.replace(/'/g, "''")}' -ArgumentList ${escapedArgs}`;
        const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command], {
            windowsHide: true,
            encoding: "utf8"
        });
        const ok = result.status === 0;
        appendDevStartupLog(`Service mode launch (runas) exit=${result.status}, stderr=${(result.stderr || "").toString().trim()}`);
        return ok;
    } catch {
        appendDevStartupLog("Service mode launch failed with exception.");
        return false;
    }
}

function normalizeLoopbackUrl(url: string): string {
    try {
        const parsed = new URL(url);
        if (parsed.hostname === "localhost" || parsed.hostname === "::1" || parsed.hostname === "[::1]") {
            parsed.hostname = "127.0.0.1";
        }
        return parsed.toString().replace(/\/$/, "");
    } catch {
        return url;
    }
}

function isAllowedBackendUrl(url: string): boolean {
    try {
        const parsed = new URL(url);
        if (parsed.protocol !== "https:") return false;
        if (parsed.hostname !== "127.0.0.1") return false;
        const port = Number(parsed.port);
        return Number.isInteger(port) && port > 0 && port <= 65535;
    } catch {
        return false;
    }
}

function tryReadBackendRuntimeUrl(runtimePath: string, minStartedAtMs: number, nowMs: number): string | null {
    try {
        if (!fs.existsSync(runtimePath)) return null;

        const stat = fs.statSync(runtimePath);
        const modifiedAtMs = stat.mtimeMs;
        if (!Number.isFinite(modifiedAtMs) || nowMs - modifiedAtMs > RUNTIME_FILE_MAX_AGE_MS) {
            return null;
        }

        const raw = fs.readFileSync(runtimePath, "utf-8").replace(/^\uFEFF/, "");
        const parsed = JSON.parse(raw) as { preferredBaseUrl?: string; urls?: string[]; startedAtUtc?: string };
        const startedAtMs = parsed.startedAtUtc ? Date.parse(parsed.startedAtUtc) : NaN;
        if (!Number.isFinite(startedAtMs)) return null;
        if (startedAtMs < minStartedAtMs) return null;
        if (nowMs - startedAtMs > RUNTIME_FILE_MAX_AGE_MS) return null;

        const preferred = parsed.preferredBaseUrl;
        if (preferred && typeof preferred === "string") {
            const normalized = normalizeLoopbackUrl(preferred);
            if (isAllowedBackendUrl(normalized)) return normalized;
        }

        const httpsUrl = parsed.urls?.find(u => {
            if (typeof u !== "string") return false;
            const normalized = normalizeLoopbackUrl(u);
            return isAllowedBackendUrl(normalized);
        });
        if (httpsUrl) return normalizeLoopbackUrl(httpsUrl);

    } catch {
        // ignore malformed runtime file and continue polling
    }
    return null;
}

async function waitForBackendRuntimeUrlFromCandidates(
    runtimePaths: Array<{ path: string; source: RuntimeSource }>,
    timeoutMs: number,
    minStartedAtMs: number
): Promise<{ url: string; source: RuntimeSource } | null> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
        const nowMs = Date.now();
        for (const runtimePath of runtimePaths) {
            const url = tryReadBackendRuntimeUrl(runtimePath.path, minStartedAtMs, nowMs);
            if (url) return { url, source: runtimePath.source };
        }
        await new Promise(resolve => setTimeout(resolve, 200));
    }
    return null;
}

async function isBackendReady(baseUrl: string): Promise<boolean> {
    return new Promise(resolve => {
        const req = https.request(`${baseUrl}/health/ready`, {
            method: "GET",
            rejectUnauthorized: false,
            timeout: 3000
        }, res => {
            res.resume();
            const statusCode = res.statusCode ?? 0;
            resolve(statusCode >= 200 && statusCode < 300);
        });

        req.on("error", () => resolve(false));
        req.on("timeout", () => {
            req.destroy();
            resolve(false);
        });

        req.end();
    });
}

async function isBackendLive(baseUrl: string): Promise<boolean> {
    return new Promise(resolve => {
        const req = https.request(`${baseUrl}/health/live`, {
            method: "GET",
            rejectUnauthorized: false,
            timeout: 3000
        }, res => {
            res.resume();
            const statusCode = res.statusCode ?? 0;
            resolve(statusCode >= 200 && statusCode < 300);
        });

        req.on("error", () => resolve(false));
        req.on("timeout", () => {
            req.destroy();
            resolve(false);
        });

        req.end();
    });
}

async function waitForBackendLive(baseUrl: string, timeoutMs: number): Promise<boolean> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
        if (await isBackendLive(baseUrl)) return true;
        await new Promise(resolve => setTimeout(resolve, BACKEND_READY_POLL_INTERVAL_MS));
    }
    return false;
}

async function waitForBackendReady(baseUrl: string, timeoutMs: number): Promise<boolean> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
        if (await isBackendReady(baseUrl)) return true;
        await new Promise(resolve => setTimeout(resolve, BACKEND_READY_POLL_INTERVAL_MS));
    }
    return false;
}

async function startBackend(): Promise<void> {
    const backendExeOverride = process.env.BACKEND_EXE_PATH;
    let exePath = "";
    if (backendExeOverride) {
        exePath = backendExeOverride;
    } else if (app.isPackaged) {
        exePath = path.join(process.resourcesPath, "backend", "Platform.Api.exe");
    } else {
        const repoRoot = path.resolve(app.getAppPath(), "..", "..", "..");
        exePath = path.join(repoRoot, "artifacts", "backend", "Platform.Api.exe");
    }

    if (!fs.existsSync(exePath)) {
        backendBaseUrl = BACKEND_FALLBACK_URL;
        appendDevStartupLog(`Backend EXE not found. Using fallback URL: ${backendBaseUrl}`);
        return;
    }

    const localRuntimePath = path.join(path.dirname(exePath), "runtime.json");
    const sharedRuntimePath = path.join(process.env.ProgramData || "C:\\ProgramData", "PlatformaDapnet", "runtime.json");
    try { if (fs.existsSync(localRuntimePath)) fs.unlinkSync(localRuntimePath); } catch { }
    try { if (fs.existsSync(sharedRuntimePath)) fs.unlinkSync(sharedRuntimePath); } catch { }

    const startTimeMs = Date.now();

    const backendArgs = [`--parent-pid=${process.pid}`, `--instance-id=${backendInstanceId}`];
    if (isDbServiceMode) {
        backendArgs.push("--db-service-mode");
    }

    const backendEnv = { ...process.env };
    if (!app.isPackaged) {
        backendEnv.ASPNETCORE_ENVIRONMENT = "Development";
        backendEnv.DOTNET_ENVIRONMENT = "Development";
    }

    backendProcess = spawn(exePath, backendArgs, {
        cwd: path.dirname(exePath),
        windowsHide: true,
        stdio: ["pipe", "inherit", "inherit"],
        env: backendEnv
    });

    const runtimeCandidates = app.isPackaged
        ? [
            { path: localRuntimePath, source: "local" as const },
            { path: sharedRuntimePath, source: "shared" as const }
        ]
        : [{ path: localRuntimePath, source: "local" as const }];

    let detected = await waitForBackendRuntimeUrlFromCandidates(
        runtimeCandidates,
        RUNTIME_DISCOVERY_TIMEOUT_MS,
        startTimeMs - 2000
    );

    if (!detected) {
        const nowMs = Date.now();
        for (const runtimePath of runtimeCandidates) {
            const url = tryReadBackendRuntimeUrl(runtimePath.path, 0, nowMs);
            if (url) {
                detected = { url, source: runtimePath.source };
                break;
            }
        }
    }

    backendBaseUrl = detected?.url ?? BACKEND_FALLBACK_URL;
    const sourceInfo = detected ? detected.source : "fallback";
    console.log(`[ELECTRON] Backend base URL: ${backendBaseUrl} [source=${sourceInfo}]`);
    appendDevStartupLog(`Backend base URL resolved: ${backendBaseUrl} [source=${sourceInfo}]`);

    const live = await waitForBackendLive(backendBaseUrl, BACKEND_LIVE_TIMEOUT_MS);
    if (!live) {
        appendDevStartupLog(`Backend live check failed after ${BACKEND_LIVE_TIMEOUT_MS}ms (${backendBaseUrl})`);
        throw new Error(`Backend process is not live after ${BACKEND_LIVE_TIMEOUT_MS}ms (${backendBaseUrl})`);
    }

    const ready = await waitForBackendReady(backendBaseUrl, 3000);
    if (!ready) {
        console.log("[ELECTRON] Backend started without full readiness (DB may be unavailable). Continuing to UI.");
    }
}

async function restartBackend(): Promise<string> {
    terminateBackendProcess();
    await new Promise(resolve => setTimeout(resolve, 500));
    await startBackend();
    return backendBaseUrl;
}

function setAppMenu(enableDevTools: boolean = false) {
    const shouldEnable = enableDevTools && isDevToolsAllowed();

    const template: MenuItemConstructorOptions[] = [
        {
            label: "Widok",
            submenu: [
                { label: "Odśwież", accelerator: "CmdOrCtrl+R", role: "reload" },
                {
                    label: "Narzędzia deweloperskie",
                    accelerator: "CmdOrCtrl+Shift+I",
                    visible: shouldEnable,
                    enabled: shouldEnable,
                    click: () => { if (isDevToolsAllowed()) mainWindow?.webContents.toggleDevTools(); }
                },
                { type: "separator" },
                { label: "Pełny ekran", accelerator: "F11", role: "togglefullscreen" }
            ]
        }
    ];

    const menu = Menu.buildFromTemplate(template);
    Menu.setApplicationMenu(menu);

    if (mainWindow) {
        mainWindow.setMenuBarVisibility(false);
    }
}

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1200, height: 800,
        title: "Platforma DAPNET",
        backgroundColor: "#1E2A38",
        show: false,
        webPreferences: {
            preload: path.join(__dirname, "../preload/preload.js"),
            contextIsolation: true,
            nodeIntegration: false,
            devTools: true
        }
    });

    setAppMenu(false);

    session.defaultSession.setCertificateVerifyProc((request, callback) => {
        const { hostname } = request;
        if (isLocalDevHost(hostname)) { callback(0); return; }
        callback(-3);
    });

    mainWindow.webContents.on("before-input-event", (event, input) => {
        const isDevToolsShortcut = (input.control || input.meta) && input.shift && input.key?.toLowerCase() === "i";
        const isF12 = input.key === "F12";
        if (!isDevToolsAllowed() && (isDevToolsShortcut || isF12)) {
            event.preventDefault();
        }
    });

    mainWindow.webContents.on("devtools-opened", () => {
        if (!isDevToolsAllowed()) mainWindow?.webContents.closeDevTools();
    });

    mainWindow.once("ready-to-show", () => {
        if (!mainWindow) return;
        mainWindow.maximize();
        mainWindow.show();
    });

    if (app.isPackaged) mainWindow.setContentProtection(true);

    if (app.isPackaged) {
        mainWindow.loadFile(path.join(app.getAppPath(), "dist", "renderer", "index.html"));
    } else {
        if (isDbServiceMode) {
            const localDistIndex = path.join(app.getAppPath(), "dist", "renderer", "index.html");
            let loadedFallback = false;

            const loadFallbackIfAvailable = () => {
                if (loadedFallback) return;
                if (!fs.existsSync(localDistIndex)) return;
                loadedFallback = true;
                appendDevStartupLog(`Renderer load fallback to file: ${localDistIndex}`);
                mainWindow?.loadFile(localDistIndex);
            };

            mainWindow.webContents.once("did-fail-load", (_, errorCode, errorDescription, validatedURL) => {
                appendDevStartupLog(`Renderer did-fail-load: code=${errorCode}, url=${validatedURL}, err=${errorDescription}`);
                loadFallbackIfAvailable();
            });

            mainWindow.loadURL("http://127.0.0.1:5173").catch(() => {
                appendDevStartupLog("Renderer loadURL failed for http://127.0.0.1:5173");
                loadFallbackIfAvailable();
            });
        } else {
            mainWindow.loadURL("http://127.0.0.1:5173");
        }
    }

    if (!app.isPackaged) {
        mainWindow.webContents.openDevTools({ mode: "detach" });
    }

    mainWindow.on("closed", () => { mainWindow = null; });
}

app.whenReady().then(async () => {
    try {
        appendDevStartupLog(`Electron ready. dbServiceMode=${isDbServiceMode}`);
        registerDevToolsShortcuts();
        if (isDbServiceMode && !isWindowsAdmin()) {
            dialog.showErrorBox("Tryb serwisowy wymaga uprawnien", "Uruchomienie trybu serwisowego wymaga uprawnien administratora Windows.");
            app.quit();
            return;
        }

        registerIpc(
            () => backendBaseUrl,
            restartBackend,
            () => ({ isDbServiceMode, isWindowsAdmin: isWindowsAdmin(), isPackaged: app.isPackaged }),
            launchDbServiceModeAsAdmin
        );
        createWindow();
        await startBackend();
    } catch (error) {
        const details = error instanceof Error ? error.message : String(error);
        appendDevStartupLog(`Startup failed: ${details}`);
        dialog.showErrorBox("Nie udalo sie uruchomic backendu", `Backend lub baza danych nie sa gotowe.\n\n${details}`);
        app.quit();
    }
});

ipcMain.on("enable-devtools", () => {
    devToolsAllowed = true;
    setAppMenu(true);
});

ipcMain.on("disable-devtools", () => {
    devToolsAllowed = false;
    if (mainWindow?.webContents.isDevToolsOpened()) mainWindow.webContents.closeDevTools();
    setAppMenu(false);
});

ipcMain.handle("save-diagnostic-logs", async (_, content: string) => {
    if (!mainWindow) return false;
    const { filePath } = await dialog.showSaveDialog(mainWindow, {
        title: "Zapisz logi diagnostyczne",
        defaultPath: path.join(app.getPath("desktop"), `dapnet-logs-${Date.now()}.txt`),
        filters: [{ name: "Pliki tekstowe", extensions: ["txt"] }]
    });
    if (filePath) {
        fs.writeFileSync(filePath, content, "utf-8");
        return true;
    }
    return false;
});

ipcMain.handle("safe-storage-encrypt", (_, text: string) => {
    if (!safeStorage.isEncryptionAvailable()) return text;
    return safeStorage.encryptString(text).toString("base64");
});

ipcMain.handle("safe-storage-decrypt", (_, encryptedBase64: string) => {
    if (!safeStorage.isEncryptionAvailable()) return encryptedBase64;
    try {
        const buffer = Buffer.from(encryptedBase64, "base64");
        return safeStorage.decryptString(buffer);
    } catch { return ""; }
});

app.on("will-quit", () => { globalShortcut.unregisterAll(); });
app.on("before-quit", () => { terminateBackendProcess(); });
app.on("window-all-closed", () => {
    if (process.platform !== "darwin") app.quit();
});
