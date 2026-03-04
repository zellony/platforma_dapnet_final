import { ipcMain, app } from "electron";
import * as os from "os";

export type StartupContext = {
    isDbServiceMode: boolean;
    isWindowsAdmin: boolean;
    isPackaged: boolean;
};

export function registerIpc(
    getApiBaseUrl: () => string | null,
    restartBackend: () => Promise<string>,
    getStartupContext: () => StartupContext,
    launchDbServiceModeAsAdmin: () => Promise<boolean>
) {
    ipcMain.handle("getApiBaseUrl", async () => {
        return getApiBaseUrl();
    });

    ipcMain.handle("restartBackend", async () => {
        return await restartBackend();
    });

    ipcMain.handle("getAppVersion", () => {
        return app.getVersion();
    });

    ipcMain.handle("getMachineName", () => {
        return os.hostname();
    });

    // ✅ NOWA FUNKCJA: POBIERANIE LOKALNEGO ADRESU IP
    ipcMain.handle("getLocalIp", () => {
        const interfaces = os.networkInterfaces();
        for (const name of Object.keys(interfaces)) {
            for (const iface of interfaces[name]!) {
                // Szukamy adresu IPv4, który nie jest adresem pętli zwrotnej (127.0.0.1)
                if (iface.family === "IPv4" && !iface.internal) {
                    return iface.address;
                }
            }
        }
        return "127.0.0.1";
    });

    ipcMain.handle("quitApp", () => {
        app.quit();
    });

    ipcMain.handle("getStartupContext", () => {
        return getStartupContext();
    });

    ipcMain.handle("launchDbServiceModeAsAdmin", async () => {
        return await launchDbServiceModeAsAdmin();
    });

}
