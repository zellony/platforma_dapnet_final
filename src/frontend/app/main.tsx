import "./app.css";
import React, { useEffect, useState, useCallback, useRef } from "react";
import ReactDom from "react-dom/client";
import { Shell } from "./layout/Shell";
import { ReadyScreen } from "./layout/ReadyScreen";
import { SetupScreen } from "./features/Setup/SetupScreen";
import { LoginScreen } from "./features/Login/LoginScreen";
import { CompanySetupView } from "./features/Setup/CompanySetupView";
import { LicenseActivationScreen } from "./features/Setup/LicenseActivationScreen";
import { CreateFirstAdminView } from "./features/Setup/CreateFirstAdminView";
import {
    apiFetch,
    setApiBaseUrl,
    clearAuthToken,
    setOnActivity,
    clearApiLogs
} from "./apiFetch";

const SESSION_TIMEOUT_SEC = 15 * 60;

const HudItem = ({ children, label }: { children: React.ReactNode, label?: string }) => (
    <div className="hud-item">
        <div className="hud-bracket hud-bracket--tl"></div>
        <div className="hud-bracket hud-bracket--tr"></div>
        <div className="hud-bracket hud-bracket--bl"></div>
        <div className="hud-bracket hud-bracket--br"></div>
        {label && <div style={{ fontSize: "8px", fontWeight: 700, color: "rgba(143, 160, 178, 0.4)", marginBottom: 2, letterSpacing: "0.5px" }}>{label}</div>}
        {children}
    </div>
);

function App() {
    const [status, setStatus] = useState<any>(null);
    const [busy, setBusy] = useState(false);
    const [hasToken, setHasToken] = useState(() => !!sessionStorage.getItem("user_info"));
    const [currentLogin, setCurrentUserLogin] = useState("");
    const [timeLeft, setTimeLeft] = useState(SESSION_TIMEOUT_SEC);
    const [tokenTimeLeft, setTokenTimeLeft] = useState<number | null>(null);
    const [showUserMenu, setShowUserMenu] = useState(false);
    const [isCompanyConfigured, setIsCompanyConfigured] = useState(true); 
    const [isCheckingCompany, setIsCheckingCompany] = useState(false);
    const [isBackendReady, setIsBackendReady] = useState(false);
    const [startupContext, setStartupContext] = useState<{ isDbServiceMode: boolean; isWindowsAdmin: boolean; isPackaged: boolean } | null>(null);
    const [dbUnavailableError, setDbUnavailableError] = useState<string | null>(null);
    const [serviceModeLaunched, setServiceModeLaunched] = useState(false);
    const [startupStage, setStartupStage] = useState("Uruchamianie backendu...");
    const [startupError, setStartupError] = useState<string | null>(null);

    const userMenuRef = useRef<HTMLDivElement>(null);
    const isBusyRef = useRef(false);
    useEffect(() => { isBusyRef.current = busy; }, [busy]);

    const resetTimer = useCallback(() => { setTimeLeft(SESSION_TIMEOUT_SEC); }, []);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
                setShowUserMenu(false);
            }
        };
        if (showUserMenu) document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, [showUserMenu]);

    const waitForBackend = async () => {
        const fallbackUrl = "https://127.0.0.1:5001";
        let url = fallbackUrl;
        setStartupError(null);
        setStartupStage("Uruchamianie backendu...");

        try {
            setStartupStage("Laczenie z usluga lokalna...");
            const resolvedUrl = await (window as any).electron.getApiBaseUrl();
            if (resolvedUrl && typeof resolvedUrl === "string") {
                url = resolvedUrl;
            }
        } catch {
            // IPC not ready or unavailable; keep fallback URL.
        }

        try {
            if ((window as any).electron?.getStartupContext) {
                const ctx = await (window as any).electron.getStartupContext();
                setStartupContext(ctx);
            }
        } catch { }

        let attempts = 0;
        const maxAttempts = startupContext?.isDbServiceMode ? 80 : 20;
        while (attempts < maxAttempts) {
            try {
                const latestUrl = await (window as any).electron.getApiBaseUrl();
                if (latestUrl && typeof latestUrl === "string") {
                    url = latestUrl;
                }
            } catch { }

            setApiBaseUrl(url);
            try {
                setStartupStage("Laczenie z usluga lokalna...");
                const res = await fetch(`${url}/system/status`, { method: "GET" });
                if (res.ok || res.status === 401 || res.status === 404) {
                    setStartupStage("Sprawdzanie polaczenia z baza danych...");
                    setIsBackendReady(true);
                    return;
                }
            } catch { }
            attempts++;
            await new Promise(r => setTimeout(r, 500));
        }
        setStartupError("Backend nie odpowiada. Sprobuj ponownie lub sprawdz log diagnostyczny.");
        setIsBackendReady(true);
    };

    const refresh = async () => {
        try {
            setStartupStage("Sprawdzanie polaczenia z baza danych...");
            // âś… KROK 1: Najpierw sprawdzamy status systemu
            const statusRes = await apiFetch("/system/status");
            if (statusRes.ok) {
                setStartupStage("Weryfikacja konfiguracji systemu...");
                const data = await statusRes.json();
                setStatus(data);
                if (data?.dbState === "DB_READY") {
                    setServiceModeLaunched(false);
                }
                setDbUnavailableError(
                    data?.dbState === "DB_UNAVAILABLE" || data?.dbState === "DB_CONFIG_BROKEN"
                        ? (data?.error || "Brak dostepu do bazy danych.")
                        : null
                );

                // âś… KROK 2: Tylko jeĹ›li system NIE wymaga setupu, sprawdzamy firmÄ™
                // Zapobiega to bĹ‚Ä™dom poĹ‚Ä…czenia z bazÄ… w logach backendu przy pierwszym starcie
                if (!data.setupRequired && data?.dbState === "DB_READY") {
                    setStartupStage("Ladowanie modulow...");
                    const companyRes = await apiFetch("/company/status").catch(() => null);
                    if (companyRes && companyRes.ok) {
                        const companyData = await companyRes.json();
                        setIsCompanyConfigured(companyData.isConfigured);
                    }
                } else {
                    setIsCompanyConfigured(true); // Ignorujemy firmÄ™, gdy system wymaga setupu bazy
                }
                setStartupStage("Gotowe. Uruchamianie ekranu logowania...");
            } else {
                setStatus({ setupRequired: true });
                setDbUnavailableError(null);
            }
        } catch (e) { 
            setStatus({ setupRequired: true }); 
            setDbUnavailableError(null);
        }
    };

    const handleManualRefresh = async () => {
        setBusy(true);
        try {
            await refresh();
            window.dispatchEvent(new CustomEvent("auth-changed"));
            window.dispatchEvent(new CustomEvent("toast-notify", { 
                detail: { message: "System zostaĹ‚ zsynchronizowany", type: "success" } 
            }));
        } catch (e) {
            window.dispatchEvent(new CustomEvent("toast-notify", { 
                detail: { message: "BĹ‚Ä…d synchronizacji", type: "error" } 
            }));
        } finally {
            setBusy(false);
            setShowUserMenu(false);
        }
    };

    const handleClearLogs = () => {
        clearApiLogs();
        window.dispatchEvent(new CustomEvent("toast-notify", { 
            detail: { message: "Logi zostaĹ‚y wyczyszczone", type: "success" } 
        }));
        setShowUserMenu(false);
    };

    useEffect(() => { waitForBackend(); }, [startupContext?.isDbServiceMode, startupContext?.isWindowsAdmin]);
    useEffect(() => { if (isBackendReady) refresh(); }, [isBackendReady]);
    useEffect(() => {
        if (!serviceModeLaunched) return;
        const timer = setInterval(() => { refresh(); }, 3000);
        return () => clearInterval(timer);
    }, [serviceModeLaunched]);

    const normalizeExpiresAt = (value: any): number | null => {
        if (value === null || value === undefined) return null;
        const num = typeof value === "string" ? Number(value) : value;
        if (!Number.isFinite(num)) return null;
        return num > 1_000_000_000_000 ? Math.floor(num / 1000) : Math.floor(num);
    };

    useEffect(() => {
        const userInfoStr = sessionStorage.getItem("user_info");
        if (userInfoStr) {
            try {
                const userInfo = JSON.parse(userInfoStr);
                setCurrentUserLogin(userInfo.login || "");
                const expiresAt = normalizeExpiresAt(userInfo.expiresAt);
                if (expiresAt) {
                    const now = Math.floor(Date.now() / 1000);
                    setTokenTimeLeft(expiresAt - now);
                } else {
                    setTokenTimeLeft(null);
                }
            } catch (e) {
                sessionStorage.removeItem("user_info");
                setHasToken(false);
                setTokenTimeLeft(null);
            }
        } else {
            setTokenTimeLeft(null);
        }
    }, [hasToken]);

    useEffect(() => {
        if (hasToken) {
            setOnActivity(resetTimer);
            const events = ["mousedown", "keydown", "scroll", "touchstart"];
            events.forEach(name => window.addEventListener(name, resetTimer));
            const heartbeat = setInterval(() => { if (sessionStorage.getItem("user_info")) refresh(); }, 5 * 60 * 1000);
            return () => {
                events.forEach(name => window.removeEventListener(name, resetTimer));
                clearInterval(heartbeat);
            };
        }
    }, [hasToken, resetTimer]);

    useEffect(() => {
        if (tokenTimeLeft === null) return;
        const timer = setInterval(() => {
            setTokenTimeLeft(prev => {
                if (prev === null || prev <= 1) { clearInterval(timer); handleLogout(); return 0; }
                return prev - 1;
            });
        }, 1000);
        return () => clearInterval(timer);
    }, [tokenTimeLeft === null]);

    useEffect(() => {
        if (!hasToken) return;
        const timer = setInterval(() => {
            if (isBusyRef.current) { resetTimer(); return; }
            setTimeLeft(prev => {
                if (prev <= 1) { clearInterval(timer); handleLogout(); return 0; }
                return prev - 1;
            });
        }, 1000);
        return () => clearInterval(timer);
    }, [hasToken, resetTimer]);

    const handleLogout = async () => {
        try { await apiFetch("/auth/logout", { method: "POST" }); } catch (e) { }
        finally {
            clearAuthToken(); setHasToken(false); setCurrentUserLogin(""); setTokenTimeLeft(null);
            setTimeLeft(SESSION_TIMEOUT_SEC); setShowUserMenu(false); window.location.reload();
        }
    };

    const formatTime = (seconds: number) => {
        const h = Math.floor(seconds / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = seconds % 60;
        if (h > 0) return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
        return `${m}:${s.toString().padStart(2, '0')}`;
    };

    const topRight = hasToken ? (
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <HudItem label="MONITOR SESJI">
                <div style={{ display: "flex", flexDirection: "column", gap: 1, minWidth: "140px" }}>
                    {tokenTimeLeft !== null && (
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "9px", color: "var(--text-muted)", gap: 10 }}>
                            <span>TOKEN:</span>
                            <span style={{ color: tokenTimeLeft < 300 ? "rgba(235, 87, 87, 0.7)" : "rgba(184, 196, 208, 0.8)", fontFamily: "'JetBrains Mono', monospace", fontSize: "11px", fontWeight: 600 }}>{formatTime(tokenTimeLeft)}</span>
                        </div>
                    )}
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "9px", color: "var(--text-muted)", gap: 10 }}>
                        <span>WYGASA:</span>
                        <span style={{ color: timeLeft < 60 ? "rgba(235, 87, 87, 0.7)" : "rgba(184, 196, 208, 0.8)", fontFamily: "'JetBrains Mono', monospace", fontSize: "11px", fontWeight: 600 }}>{formatTime(timeLeft)}</span>
                    </div>
                </div>
            </HudItem>

            <div style={{ position: "relative" }} ref={userMenuRef}>
                <div onClick={() => setShowUserMenu(!showUserMenu)} style={{ display: "flex", alignItems: "center", gap: 8, cursor: "pointer", padding: "4px 12px", borderRadius: "6px", background: showUserMenu ? "rgba(255,255,255,0.05)" : "transparent", transition: "all 0.2s", position: "relative", zIndex: 1001, userSelect: "none" }}>
                    <div style={{ width: "24px", height: "24px", borderRadius: "50%", background: "var(--bg-soft)", border: "1px solid var(--hud-border)", display: "flex", alignItems: "center", justifyContent: "center" }}>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--text-muted)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H4a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                    </div>
                    <span style={{ fontSize: "13px", color: "rgba(184, 196, 208, 0.8)", fontWeight: 600, fontFamily: "'JetBrains Mono', monospace" }}>{currentLogin?.toUpperCase() || "..." }</span>
                </div>
                {showUserMenu && (
                    <div style={{ position: "absolute", top: "100%", right: 0, width: "200px", background: "var(--bg-panel)", border: "1px solid var(--border)", borderRadius: "8px", marginTop: "8px", boxShadow: "0 10px 30px rgba(0,0,0,0.5)", zIndex: 999999, padding: "4px" }}>
                        <style>{`
                            .user-menu-btn {
                                width: 100%;
                                background: transparent;
                                border: none;
                                padding: 10px 12px;
                                fontSize: 12px;
                                color: var(--text-main);
                                cursor: pointer;
                                border-radius: 4px;
                                display: flex;
                                alignItems: center;
                                gap: 10px;
                                textAlign: left;
                                transition: all 0.2s;
                            }
                            .user-menu-btn:hover {
                                background: rgba(255,255,255,0.05);
                                color: var(--accent);
                            }
                            .user-menu-btn--danger:hover {
                                background: rgba(235, 87, 87, 0.1);
                                color: var(--danger);
                            }
                        `}</style>
                        <button onClick={handleManualRefresh} disabled={busy} className="user-menu-btn">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M23 4v6h-6"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>
                            Synchronizuj system
                        </button>
                        <button onClick={handleClearLogs} className="user-menu-btn">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>
                            WyczyĹ›Ä‡ logi
                        </button>
                        <div style={{ height: "1px", background: "var(--border)", margin: "4px 0" }}></div>
                        <button onClick={handleLogout} className="user-menu-btn user-menu-btn--danger">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
                            Wyloguj siÄ™
                        </button>
                    </div>
                )}
            </div>
        </div>
    ) : null;

    if (!isBackendReady || !status) {
        return (
            <div style={{ height: "100vh", background: "#1E2A38", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 20 }}>
                <div style={{ width: 60, height: 60, position: 'relative' }}>
                     <div style={{ position: 'absolute', inset: 0, border: "3px solid rgba(86, 204, 242, 0.1)", borderTopColor: "var(--accent)", borderRadius: "50%", animation: "spin 1s linear infinite" }}></div>
                     <div style={{ position: 'absolute', inset: 10, border: "2px solid rgba(86, 204, 242, 0.05)", borderBottomColor: "var(--accent)", borderRadius: "50%", animation: "spin 1.5s linear infinite reverse" }}></div>
                </div>
                <div style={{ color: "var(--text-muted)", fontSize: "12px", letterSpacing: "2px", fontWeight: 600, fontFamily: "'JetBrains Mono', monospace", opacity: 0.6 }}>
                    {startupStage}
                </div>
                {!!startupError && (
                    <div style={{ color: "rgba(235, 87, 87, 0.9)", fontSize: "12px", maxWidth: 620, textAlign: "center", lineHeight: 1.4 }}>
                        {startupError}
                    </div>
                )}
                <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
            </div>
        );
    }

    if (status?.dbState === "DB_UNAVAILABLE" || status?.dbState === "DB_CONFIG_BROKEN") {
        const canEnterServiceMode = !!(window as any).electron?.launchDbServiceModeAsAdmin;
        const isInElevatedServiceMode = !!startupContext?.isDbServiceMode && !!startupContext?.isWindowsAdmin;

        if (isInElevatedServiceMode) {
            return <Shell topRight={topRight} initialStatus={status}><SetupScreen onDone={refresh} busy={busy} setBusy={setBusy} onBaseUrlChange={() => {}} /></Shell>;
        }

        return (
            <Shell topRight={topRight} initialStatus={status}>
                <div className="setup-container">
                    <div className="setup-box" style={{ textAlign: "center" }}>
                        <h2 className="setup-title" style={{ marginBottom: 12 }}>BRAK DOSTEPU DO BAZY</h2>
                        <div className="setup-subtitle" style={{ marginBottom: 20 }}>
                            System ma konfiguracje bazy, ale nie moze nawiazac polaczenia.
                        </div>
                        <div style={{ fontSize: 12, color: "var(--text-muted)", marginBottom: 24 }}>
                            {serviceModeLaunched
                                ? "Tryb serwisowy zostal uruchomiony. Czekam na zapis konfiguracji i odswiezam status automatycznie..."
                                : (dbUnavailableError || "Sprawdz polaczenie z serwerem PostgreSQL lub uruchom tryb serwisowy.")}
                        </div>
                        <div style={{ display: "flex", gap: 12, justifyContent: "center", flexWrap: "wrap" }}>
                            <button className="secondary" onClick={() => refresh()} disabled={busy}>
                                SPROBUJ PONOWNIE
                            </button>
                            <button
                                className="primary"
                                disabled={!canEnterServiceMode || busy}
                                onClick={async () => {
                                    if (!canEnterServiceMode) return;
                                    setBusy(true);
                                    try {
                                        const launched = await (window as any).electron.launchDbServiceModeAsAdmin();
                                        if (launched) {
                                            setServiceModeLaunched(true);
                                            await (window as any).electron.quitApp();
                                        } else {
                                            setDbUnavailableError("Nie udalo sie uruchomic trybu serwisowego jako administrator.");
                                        }
                                    } finally {
                                        setBusy(false);
                                    }
                                }}
                            >
                                TRYB SERWISOWY (ADMIN)
                            </button>
                            <button className="secondary danger" onClick={() => (window as any).electron.quitApp()} disabled={busy}>
                                ZAMKNIJ APLIKACJE
                            </button>
                        </div>
                    </div>
                </div>
            </Shell>
        );
    }

    if (status?.setupRequired) return <Shell topRight={topRight} initialStatus={status}><SetupScreen onDone={refresh} busy={busy} setBusy={setBusy} onBaseUrlChange={() => {}} /></Shell>;
    if (!isCompanyConfigured && !isCheckingCompany) return <Shell topRight={topRight} initialStatus={status}><CompanySetupView onDone={() => { setIsCompanyConfigured(true); refresh(); }} /></Shell>;
    if (status?.licenseRequired) return <Shell topRight={topRight} initialStatus={status}><LicenseActivationScreen onActivated={refresh} /></Shell>;
    if (status?.adminRequired) return <Shell topRight={topRight} initialStatus={status}><CreateFirstAdminView onDone={refresh} /></Shell>;
    if (!hasToken) return <Shell topRight={topRight} initialStatus={status}><LoginScreen busy={busy} setBusy={setBusy} onLoggedIn={() => setHasToken(true)} /></Shell>;

    return <Shell topRight={topRight} initialStatus={status}><ReadyScreen status={status || { ok: true }} setGlobalBusy={setBusy} /></Shell>;
}

const root = ReactDom.createRoot(document.getElementById("root")!);
root.render(<App />);
