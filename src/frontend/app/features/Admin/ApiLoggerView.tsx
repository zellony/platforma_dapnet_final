import React, { useState, useEffect } from "react";
import { getApiLogs, ApiLogEntry } from "../../apiFetch";

export function ApiLoggerView() {
    const [logs, setLogs] = useState<ApiLogEntry[]>([]);
    const [selectedLog, setSelectedLog] = useState<ApiLogEntry | null>(null);

    useEffect(() => {
        const refresh = () => setLogs(getApiLogs());
        refresh();
        window.addEventListener("api-log-added", refresh);
        window.addEventListener("api-log-updated", refresh);
        window.addEventListener("api-log-cleared", refresh);
        return () => {
            window.removeEventListener("api-log-added", refresh);
            window.removeEventListener("api-log-updated", refresh);
            window.removeEventListener("api-log-cleared", refresh);
        };
    }, []);

    const getStatusColor = (status?: number) => {
        if (!status) return "var(--text-muted)";
        if (status >= 200 && status < 300) return "#4ade80";
        if (status >= 400) return "#f87171";
        return "#fbbf24";
    };

    return (
        <div style={{ display: "flex", height: "100%", background: "var(--bg-main)", color: "var(--text-main)", fontSize: "12px" }}>
            {/* LISTA LOGÓW */}
            <div style={{ flex: 1, borderRight: "1px solid var(--border)", overflowY: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead style={{ position: "sticky", top: 0, background: "var(--bg-panel)", zIndex: 1 }}>
                        <tr>
                            <th style={{ padding: "8px", textAlign: "left", fontSize: "10px", color: "var(--text-muted)" }}>TIME</th>
                            <th style={{ padding: "8px", textAlign: "left", fontSize: "10px", color: "var(--text-muted)" }}>METHOD</th>
                            <th style={{ padding: "8px", textAlign: "left", fontSize: "10px", color: "var(--text-muted)" }}>PATH</th>
                            <th style={{ padding: "8px", textAlign: "left", fontSize: "10px", color: "var(--text-muted)" }}>STATUS</th>
                            <th style={{ padding: "8px", textAlign: "left", fontSize: "10px", color: "var(--text-muted)" }}>MS</th>
                        </tr>
                    </thead>
                    <tbody>
                        {logs.map(log => (
                            <tr 
                                key={log.id} 
                                onClick={() => setSelectedLog(log)}
                                style={{ 
                                    cursor: "pointer", 
                                    background: selectedLog?.id === log.id ? "rgba(86, 204, 242, 0.1)" : "transparent",
                                    borderBottom: "1px solid var(--bg-soft)"
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.background = "rgba(255,255,255,0.03)"}
                                onMouseLeave={(e) => e.currentTarget.style.background = selectedLog?.id === log.id ? "rgba(86, 204, 242, 0.1)" : "transparent"}
                            >
                                <td style={{ padding: "8px", fontFamily: "monospace" }}>{log.timestamp}</td>
                                <td style={{ padding: "8px", fontWeight: 700 }}>{log.method}</td>
                                <td style={{ padding: "8px", color: "var(--text-muted)" }}>{log.path}</td>
                                <td style={{ padding: "8px", color: getStatusColor(log.status), fontWeight: 700 }}>{log.status || "..."}</td>
                                <td style={{ padding: "8px", color: "var(--text-muted)" }}>{log.duration ? `${log.duration}ms` : "-"}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* DETALE LOGU */}
            <div style={{ width: "400px", padding: "16px", overflowY: "auto", background: "rgba(0,0,0,0.1)" }}>
                {selectedLog ? (
                    <div>
                        <h4 style={{ margin: "0 0 16px 0", color: "var(--accent)", fontSize: "11px", letterSpacing: "1px" }}>REQUEST DETAILS</h4>
                        
                        <div style={{ marginBottom: "20px" }}>
                            <span style={{ fontSize: "9px", color: "var(--text-muted)", display: "block", marginBottom: "4px" }}>REQUEST BODY</span>
                            <pre style={{ background: "rgba(0,0,0,0.3)", padding: "10px", borderRadius: "6px", overflowX: "auto", fontSize: "10px" }}>
                                {JSON.stringify(selectedLog.requestBody, null, 2) || "No body"}
                            </pre>
                        </div>

                        <div>
                            <span style={{ fontSize: "9px", color: "var(--text-muted)", display: "block", marginBottom: "4px" }}>RESPONSE BODY</span>
                            <pre style={{ background: "rgba(0,0,0,0.3)", padding: "10px", borderRadius: "6px", overflowX: "auto", fontSize: "10px", color: "#e2e8f0" }}>
                                {JSON.stringify(selectedLog.responseBody, null, 2)}
                            </pre>
                        </div>
                    </div>
                ) : (
                    <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--text-muted)", fontSize: "11px" }}>
                        Wybierz zapytanie, aby zobaczyć szczegóły
                    </div>
                )}
            </div>
        </div>
    );
}
