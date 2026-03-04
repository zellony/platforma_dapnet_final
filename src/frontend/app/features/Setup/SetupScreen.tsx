import React, { useState, useEffect } from "react";
import { apiFetch, setApiBaseUrl, getErrorMessage } from "../../apiFetch";
import iconImg from "../../assets/icon.png";

type SetupScreenProps = {
    onDone: () => Promise<void>;
    busy: boolean;
    setBusy: (v: boolean) => void;
    onBaseUrlChange: (url: string) => void;
};

export function SetupScreen({ onDone, busy, setBusy, onBaseUrlChange }: SetupScreenProps) {
    const [host, setHost] = useState("");
    const [port, setPort] = useState("5432");
    const [database, setDatabase] = useState("");
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [pingResult, setPingResult] = useState("");
    const [saveResult, setSaveResult] = useState("");
    const [isInitializing, setIsInitializing] = useState(false);
    const [initStatus, setInitStatus] = useState("");
    const [isLoadingConfig, setIsLoadingConfig] = useState(true);
    const [configLoadError, setConfigLoadError] = useState("");

    useEffect(() => {
        let cancelled = false;
        async function loadExistingConfig() {
            setIsLoadingConfig(true);
            setConfigLoadError("");
            try {
                const res = await apiFetch("/system/config/db", { method: "GET" });
                if (!res.ok) {
                    if (!cancelled) setConfigLoadError("Brak zapisanej konfiguracji lub brak dostepu.");
                    return;
                }
                const data = await res.json();
                if (cancelled) return;
                if (typeof data.host === "string") setHost(data.host);
                if (data.port !== undefined && data.port !== null) setPort(String(data.port));
                if (typeof data.database === "string") setDatabase(data.database);
                if (typeof data.username === "string") setUsername(data.username);
                if (typeof data.password === "string") setPassword(data.password);
            } catch {
                if (!cancelled) setConfigLoadError("Nie udalo sie wczytac zapisanej konfiguracji.");
            } finally {
                if (!cancelled) setIsLoadingConfig(false);
            }
        }
        loadExistingConfig();
        return () => { cancelled = true; };
    }, []);

    // ? AUTOMATYCZNE CZYSZCZENIE KOMUNIKATÓW
    useEffect(() => {
        if (pingResult || saveResult) {
            const timer = setTimeout(() => {
                setPingResult("");
                setSaveResult("");
            }, 5000);
            return () => clearTimeout(timer);
        }
    }, [pingResult, saveResult]);

    const buildCS = () => `Host=${host};Port=${port};Database=${database};Username=${username};Password=${password}`;

    async function waitForEndpoint(baseUrl: string, endpointPath: string, timeoutMs: number): Promise<boolean> {
        const start = Date.now();
        while (Date.now() - start < timeoutMs) {
            try {
                const controller = new AbortController();
                const timer = setTimeout(() => controller.abort(), 1500);
                const res = await fetch(`${baseUrl}${endpointPath}`, { method: "GET", signal: controller.signal });
                clearTimeout(timer);
                if (res.ok) return true;
            } catch { }
            await new Promise(resolve => setTimeout(resolve, 250));
        }
        return false;
    }

    async function doPing() {
        setPingResult("");
        setBusy(true);
        try {
            const res = await apiFetch("/system/db/ping", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ connectionString: buildCS() })
            });
            
            const data = await res.json();
            
            // ? POPRAWKA: Sprawdzamy pole 'ok' wewnątrz JSON-a, nie tylko status HTTP
            if (res.ok && data.ok) {
                setPingResult("? POŁĄCZENIE PRAWIDŁOWE");
            } else {
                // Jeśli API zwróciło błąd w JSON (data.error) lub status HTTP nie jest OK
                const msg = data.error || await getErrorMessage(res);
                setPingResult(`? BŁĄD: ${msg}`);
            }
        } catch (e) { 
            setPingResult(`? BŁĄD: ${e instanceof Error ? e.message : String(e)}`); 
        } finally { 
            setBusy(false); 
        }
    }

    async function doSave() {
        setSaveResult("");
        setBusy(true);
        try {
            setInitStatus("ZAPISYWANIE KONFIGURACJI...");
            const saveRes = await apiFetch("/system/config/db", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ connectionString: buildCS() })
            });

            if (!saveRes.ok) {
                const msg = await getErrorMessage(saveRes);
                throw new Error(msg);
            }
            
            setIsInitializing(true);
            setInitStatus("RESTARTOWANIE USŁUG SYSTEMOWYCH...");
            const newUrl = await (window as any).electron.restartBackend();
            setApiBaseUrl(newUrl);
            onBaseUrlChange(newUrl);

            setInitStatus("OCZEKIWANIE NA SERWER...");
            const live = await waitForEndpoint(newUrl, "/health/live", 20000);
            if (!live) {
                throw new Error("Backend nie odpowiada (/health/live).");
            }

            setInitStatus("SPRAWDZANIE GOTOWOSCI SERWERA...");
            await waitForEndpoint(newUrl, "/health/ready", 10000);

            setInitStatus("INICJALIZACJA SCHEMATU BAZY (MIGRACJE)...");
            let initError = "";
            let initialized = false;
            for (let attempt = 1; attempt <= 3; attempt++) {
                const initRes = await apiFetch("/system/init", { method: "POST" });
                if (initRes.ok) {
                    initialized = true;
                    break;
                }
                initError = await getErrorMessage(initRes);
                await new Promise(resolve => setTimeout(resolve, 800));
            }
            if (!initialized) {
                throw new Error(initError || "Nie udalo sie zainicjalizowac bazy danych.");
            }

            setInitStatus("KONFIGURACJA ZAKOŃCZONA!");
            await new Promise(resolve => setTimeout(resolve, 1500));
            
            setBusy(false);
            setIsInitializing(false);
            await onDone();
        } catch (e) { 
            setSaveResult(`? KRYTYCZNY BŁĄD: ${e instanceof Error ? e.message : String(e)}`); 
            setBusy(false);
            setIsInitializing(false);
        }
    }

    const btnStyle: React.CSSProperties = { width: "160px", padding: "10px 0", fontSize: "11px" };

    return (
        <div className="setup-container">
            <div className="setup-box" style={{ width: "480px" }}>
                {isInitializing && (
                    <div style={{ position: "absolute", inset: 0, background: "var(--bg-main)", zIndex: 100, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 24, padding: "40px", borderRadius: "12px" }}>
                        <div className="spinner"></div>
                        <div style={{ color: "var(--accent)", fontSize: "12px", fontWeight: 700, letterSpacing: "2px", textAlign: "center", lineHeight: "1.6" }}>
                            {initStatus}<br/>
                            <span style={{ fontSize: "10px", color: "var(--text-muted)", fontWeight: 400 }}>Proszę nie wyłączać aplikacji...</span>
                        </div>
                    </div>
                )}

                <div className="setup-header">
                    <img src={iconImg} alt="DAPNET" style={{ width: "48px", height: "48px", marginBottom: "12px", opacity: 0.6 }} />
                    <h2 className="setup-title">KONFIGURACJA BAZY</h2>
                    <div className="setup-subtitle">PARAMETRY POŁĄCZENIA POSTGRESQL</div>
                    {isLoadingConfig && (
                        <div style={{ marginTop: 8, fontSize: 11, color: "var(--text-muted)" }}>
                            Wczytywanie konfiguracji...
                        </div>
                    )}
                    {!isLoadingConfig && !!configLoadError && (
                        <div style={{ marginTop: 8, fontSize: 11, color: "var(--text-muted)" }}>
                            {configLoadError}
                        </div>
                    )}
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    <div style={{ display: "grid", gridTemplateColumns: "1fr 100px", gap: 16 }}>
                        <div className="setup-field">
                            <label className="setup-label">ADRES SERWERA (HOST)</label>
                            <input autoFocus className="setup-input" value={host} onChange={e => setHost(e.target.value)} placeholder="np. localhost" disabled={busy} />
                        </div>
                        <div className="setup-field">
                            <label className="setup-label">PORT</label>
                            <input className="setup-input" value={port} onChange={e => setPort(e.target.value)} placeholder="5432" disabled={busy} />
                        </div>
                    </div>

                    <div className="setup-field">
                        <label className="setup-label">NAZWA BAZY DANYCH</label>
                        <input className="setup-input" value={database} onChange={e => setDatabase(e.target.value)} placeholder="Wprowadź nazwę bazy..." disabled={busy} />
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
                        <div className="setup-field">
                            <label className="setup-label">UŻYTKOWNIK</label>
                            <input className="setup-input" value={username} onChange={e => setUsername(e.target.value)} placeholder="postgres" disabled={busy} />
                        </div>
                        <div className="setup-field">
                            <label className="setup-label">HASŁO DOSTĘPU</label>
                            <input className="setup-input" type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="********" disabled={busy} />
                        </div>
                    </div>

                    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 12, marginTop: 20 }}>
                        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, width: "100%" }}>
                            <button 
                                className="secondary"
                                onClick={doPing} 
                                disabled={busy || isLoadingConfig} 
                                style={btnStyle}
                            >
                                TESTUJ POŁĄCZENIE
                            </button>
                            <button 
                                className="primary" 
                                onClick={doSave} 
                                disabled={busy || isLoadingConfig || !host || !database || !username} 
                                style={btnStyle}
                            >
                                ZAPISZ I URUCHOM
                            </button>
                        </div>

                        <button 
                            className="secondary danger"
                            onClick={() => (window as any).electron.quitApp()}
                            style={{ border: "none", background: "transparent", fontSize: "10px", width: "auto", marginTop: "10px", opacity: 0.6 }}
                        >
                            ZAMKNIJ APLIKACJĘ
                        </button>
                    </div>

                    {(pingResult || saveResult) && !isInitializing && (
                        <div style={{ 
                            marginTop: 15, padding: "12px", borderRadius: 8, 
                            background: (pingResult.includes("?") || saveResult.includes("?")) ? "rgba(74, 222, 128, 0.05)" : "rgba(235, 87, 87, 0.05)", 
                            fontSize: "11px", textAlign: "center", 
                            border: `1px solid ${(pingResult.includes("?") || saveResult.includes("?")) ? "rgba(74, 222, 128, 0.2)" : "rgba(235, 87, 87, 0.2)"}`, 
                            fontWeight: 600,
                            color: (pingResult.includes("?") || saveResult.includes("?")) ? "#4ADE80" : "#EB5757",
                            wordBreak: "break-word",
                            lineHeight: "1.4"
                        }}>
                            {pingResult || saveResult}
                        </div>
                    )}
                </div>
            </div>
            <style>{`
                .spinner { width: 40px; height: 40px; border: 3px solid rgba(86, 204, 242, 0.1); border-top-color: var(--accent); border-radius: 50%; animation: spin 1s linear infinite; }
                @keyframes spin { to { transform: rotate(360deg); } }
            `}</style>
        </div>
    );
}

