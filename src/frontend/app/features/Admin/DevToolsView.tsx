import React, { useState } from "react";
import { clearApiLogs, getApiLogs } from "../../apiFetch";
import { useToast } from "../../layout/Shell";
import { Panel } from "../../ui/Panel";

export function DevToolsView() {
    const [showHud, setShowHud] = useState(() => localStorage.getItem("dev_show_hud") === "true");
    const [showPaths, setShowPaths] = useState(() => localStorage.getItem("dev_show_paths") === "true");
    const { addToast } = useToast();

    const toggleHud = () => {
        const val = !showHud;
        setShowHud(val);
        localStorage.setItem("dev_show_hud", String(val));
        window.dispatchEvent(new CustomEvent("dev-hud-changed", { detail: val }));
    };

    const togglePaths = () => {
        const val = !showPaths;
        setShowPaths(val);
        localStorage.setItem("dev_show_paths", String(val));
        window.dispatchEvent(new CustomEvent("dev-paths-changed", { detail: val }));
    };

    const openTool = (key: string) => {
        window.dispatchEvent(new CustomEvent("open-dev-tool", { detail: key }));
    };

    const exportLogs = async () => {
        const logs = getApiLogs();
        const userInfo = sessionStorage.getItem("user_info");
        const systemInfo = {
            userAgent: navigator.userAgent,
            platform: navigator.platform,
            timestamp: new Date().toISOString(),
            user: userInfo ? JSON.parse(userInfo) : "Not logged in"
        };

        let content = "=== PLATFORMA DAPNET DIAGNOSTIC LOGS ===\n";
        content += `Generated: ${new Date().toLocaleString()}\n`;
        content += `System Info: ${JSON.stringify(systemInfo, null, 2)}\n\n`;
        content += "=== API REQUEST HISTORY ===\n";
        
        logs.forEach((log, index) => {
            content += `[${index + 1}] ${log.timestamp} | ${log.method} ${log.path}\n`;
            content += `Status: ${log.status} | Duration: ${log.duration}ms\n`;
            content += `Request: ${JSON.stringify(log.requestBody)}\n`;
            content += `Response: ${JSON.stringify(log.responseBody)}\n`;
            content += "-------------------------------------------\n";
        });

        const success = await (window as any).electron.saveDiagnosticLogs(content);
        if (success) {
            window.dispatchEvent(new CustomEvent("toast-notify", { detail: { message: "Logi zostały zapisane pomyślnie!", type: "success" } }));
        }
    };

    const clearLogs = () => {
        clearApiLogs();
        addToast("Logi zostały wyczyszczone", "success");
    };

    const rowStyle: React.CSSProperties = { display: "flex", alignItems: "center", justifyContent: "space-between", padding: "10px 12px", background: "rgba(0,0,0,0.2)", borderRadius: "6px", marginBottom: "8px" };
    const shortcutStyle: React.CSSProperties = { fontFamily: "'JetBrains Mono', monospace", fontSize: "11px", color: "var(--accent)", background: "rgba(86, 204, 242, 0.1)", padding: "2px 6px", borderRadius: "4px", border: "1px solid rgba(86, 204, 242, 0.2)" };

    return (
        <div className="dapnet-view">
            <div className="dapnet-view-header" style={{ paddingBottom: 8 }}>
                <div style={{ fontSize: "12px", color: "var(--text-muted)", letterSpacing: "1px", fontWeight: 700 }}>
                    DEVELOPER CONTROL CENTER
                </div>
            </div>

            <div className="dapnet-view-body dapnet-view-body--fill" style={{ display: "grid", gridTemplateColumns: "1.1fr 1fr", gap: 16 }}>
                <Panel title="LIVE MONITORS" className="ui-panel">
                    <div style={{ padding: "12px 12px 4px 12px" }}>
                        <div style={rowStyle}>
                            <span style={{ fontSize: "13px" }}>Debug HUD (RAM/Ping)</span>
                            <button onClick={toggleHud} className={showHud ? "primary" : "secondary"} style={{ padding: "4px 12px", fontSize: "10px" }}>
                                {showHud ? "ON" : "OFF"}
                            </button>
                        </div>
                        <div style={rowStyle}>
                            <span style={{ fontSize: "13px" }}>Wizualizacja ścieżek (.tsx)</span>
                            <button onClick={togglePaths} className={showPaths ? "primary" : "secondary"} style={{ padding: "4px 12px", fontSize: "10px" }}>
                                {showPaths ? "ON" : "OFF"}
                            </button>
                        </div>
                    </div>
                </Panel>

                <Panel title="DIAGNOSTIC TOOLS" className="ui-panel">
                    <div style={{ padding: "12px" }}>
                        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px" }}>
                            <button onClick={() => openTool("dev.logger")} className="secondary" style={{ fontSize: "11px", padding: "10px" }}>Logger Żądań</button>
                            <button onClick={() => openTool("admin.db-sessions")} className="secondary" style={{ fontSize: "11px", padding: "10px" }}>Inspektor Bazy</button>
                            <button onClick={() => openTool("dev.emulator")} className="secondary" style={{ fontSize: "11px", padding: "10px" }}>Emulator Uprawnień</button>
                            <button onClick={exportLogs} className="secondary" style={{ fontSize: "11px", padding: "10px" }}>Eksport Logów</button>
                            <button onClick={clearLogs} className="secondary" style={{ fontSize: "11px", padding: "10px" }}>Wyczyść logi</button>
                        </div>
                    </div>
                </Panel>

                <Panel title="SHORTCUTS" className="ui-panel">
                    <div style={{ padding: "12px", display: "flex", flexDirection: "column", gap: "8px" }}>
                        <div style={{ display: "flex", justifyContent: "space-between", fontSize: "11px" }}>
                            <span style={{ color: "var(--text-muted)" }}>Otwórz DevTools</span>
                            <code style={shortcutStyle}>Ctrl + Shift + I</code>
                        </div>
                        <div style={{ display: "flex", justifyContent: "space-between", fontSize: "11px" }}>
                            <span style={{ color: "var(--text-muted)" }}>Przełącz HUD</span>
                            <code style={shortcutStyle}>Ctrl + Alt + H</code>
                        </div>
                        <div style={{ display: "flex", justifyContent: "space-between", fontSize: "11px" }}>
                            <span style={{ color: "var(--text-muted)" }}>Przełącz Ścieżki</span>
                            <code style={shortcutStyle}>Ctrl + Alt + F</code>
                        </div>
                    </div>
                </Panel>

                <Panel title="SECURITY NOTE" className="ui-panel">
                    <div style={{ padding: "12px", fontSize: "11px", color: "var(--text-muted)", lineHeight: 1.5 }}>
                        Logi są redagowane. Dane logowania i tokeny są ukryte, ale nadal obowiązują zasady bezpieczeństwa po stronie backendu i urządzenia.
                    </div>
                </Panel>
            </div>
        </div>
    );
}
