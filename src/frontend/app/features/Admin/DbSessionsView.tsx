import React, { useEffect, useState } from "react";
import { apiFetch } from "../../apiFetch";

interface DbSessionDto {
    pid: string;
    ip: string;
    machineName: string;
    startTime: string;
    state: string;
    lastQueryTime: string;
}

export function DbSessionsView() {
    const [sessions, setSessions] = useState<DbSessionDto[]>([]);
    const [loading, setLoading] = useState(true);

    const loadData = async () => {
        setLoading(true);
        try {
            const res = await apiFetch("/system/db-sessions");
            if (res.ok) setSessions(await res.json());
        } catch (e) { console.error(e); }
        finally { setLoading(false); }
    };

    useEffect(() => { loadData(); }, []);

    const gridTemplate = "80px 120px 150px 180px 120px minmax(180px, auto)";
    const borderColor = "var(--bg-soft)";

    return (
        <div className="view-container">
            <div className="view-header">
                <div style={{ fontSize: "11px", fontWeight: 600, color: "var(--text-muted)", flex: 1 }}>AKTYWNE POŁĄCZENIA Z BAZĄ DANYCH</div>
                <button className="primary" onClick={loadData} style={{ fontSize: 11, padding: "4px 12px" }}>Odśwież</button>
            </div>

            <div className="view-content">
                <div className="ui-panel" style={{ flex: 1, display: "flex", flexDirection: "column", minHeight: 0 }}>
                    <div style={{ border: "1px solid var(--bg-soft)", borderRadius: 8, overflow: "hidden", background: "var(--bg-main)", margin: "8px", display: "flex", flexDirection: "column", flex: 1, minHeight: 0 }}>
                        <div style={{ display: "grid", gridTemplateColumns: gridTemplate, fontSize: "10px", fontWeight: 700, color: "var(--text-muted)", letterSpacing: "0.5px", background: "rgba(0,0,0,0.2)", borderBottom: "2px solid var(--bg-soft)", flexShrink: 0 }}>
                            <div style={{ padding: "6px 12px", borderRight: `1px solid ${borderColor}` }}>PID</div>
                            <div style={{ padding: "6px 12px", borderRight: `1px solid ${borderColor}` }}>ADRES IP</div>
                            <div style={{ padding: "6px 12px", borderRight: `1px solid ${borderColor}` }}>KOMPUTER</div>
                            <div style={{ padding: "6px 12px", borderRight: `1px solid ${borderColor}` }}>CZAS STARTU</div>
                            <div style={{ padding: "6px 12px", borderRight: `1px solid ${borderColor}` }}>STATUS</div>
                            <div style={{ padding: "6px 12px" }}>OSTATNIE ZAPYTANIE</div>
                        </div>

                        <div style={{ overflowY: "auto", flex: 1, minHeight: 0 }}>
                            {loading ? (
                                <div style={{ padding: 20, textAlign: "center", opacity: 0.5 }}>Ładowanie sesji...</div>
                            ) : sessions.map((s, index) => (
                                <div key={s.pid} style={{ display: "grid", gridTemplateColumns: gridTemplate, borderBottom: "1px solid var(--bg-soft)", background: index % 2 === 0 ? "transparent" : "rgba(255,255,255,0.01)" }}>
                                    <div style={{ padding: "6px 12px", fontSize: "11px", color: "rgba(86, 204, 242, 0.8)", fontWeight: 600, borderRight: `1px solid ${borderColor}` }}>{s.pid}</div>
                                    <div style={{ padding: "6px 12px", fontSize: "11px", borderRight: `1px solid ${borderColor}` }}>{s.ip}</div>
                                    <div style={{ padding: "6px 12px", fontSize: "11px", color: "var(--text-main)", fontWeight: 500, borderRight: `1px solid ${borderColor}` }}>{s.machineName}</div>
                                    <div style={{ padding: "6px 12px", fontSize: "11px", color: "var(--text-muted)", borderRight: `1px solid ${borderColor}` }}>{new Date(s.startTime).toLocaleString()}</div>
                                    <div style={{ padding: "6px 12px", borderRight: `1px solid ${borderColor}`, display: "flex", alignItems: "center" }}>
                                        <span style={{ fontSize: "9px", fontWeight: 700, padding: "1px 6px", borderRadius: "10px", background: s.state === "active" ? "rgba(69, 160, 73, 0.1)" : "rgba(0,0,0,0.2)", color: s.state === "active" ? "#45a049" : "var(--text-muted)" }}>
                                            {s.state.toUpperCase()}
                                        </span>
                                    </div>
                                    <div style={{ padding: "6px 12px", fontSize: "11px", color: "var(--text-muted)" }}>{new Date(s.lastQueryTime).toLocaleString()}</div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
