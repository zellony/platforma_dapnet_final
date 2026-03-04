import React, { useEffect, useState } from "react";
import { apiFetch } from "../apiFetch";

interface DbSessionsPanelProps {
    onClose: () => void;
}

export function DbSessionsPanel({ onClose }: DbSessionsPanelProps) {
    const [sessions, setSessions] = useState<any[]>([]);
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

    return (
        <div style={{
            position: "absolute",
            bottom: "12px",
            left: "700px", // Obok panelu szczegółów (340 + 350 + 10)
            width: "650px",
            background: "var(--bg-panel)",
            border: "1px solid var(--border)",
            borderRadius: "10px",
            boxShadow: "30px 10px 60px rgba(0,0,0,0.5)",
            zIndex: 5002,
            display: "flex",
            flexDirection: "column",
            maxHeight: "400px",
            animation: "slideRight 0.3s ease-out"
        }}>
            <div style={{ padding: "8px 12px", background: "rgba(86, 204, 242, 0.05)", borderBottom: "1px solid var(--bg-soft)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <span style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.8)", letterSpacing: "0.5px" }}>MONITOR SESJI BAZY DANYCH</span>
                <div style={{ display: "flex", gap: 8 }}>
                    <button onClick={loadData} style={{ background: "transparent", border: "none", color: "var(--accent)", cursor: "pointer", fontSize: "10px" }}>ODŚWIEŻ</button>
                    <button onClick={onClose} style={{ background: "transparent", border: "none", color: "var(--text-muted)", cursor: "pointer", padding: "2px" }}>✕</button>
                </div>
            </div>

            <div style={{ padding: "8px", overflowY: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "11px" }}>
                    <thead>
                        <tr style={{ textAlign: "left", color: "var(--text-muted)", borderBottom: "1px solid var(--bg-soft)" }}>
                            <th style={{ padding: "6px" }}>PID</th>
                            <th style={{ padding: "6px" }}>KOMPUTER</th>
                            <th style={{ padding: "6px" }}>STATUS</th>
                            <th style={{ padding: "6px" }}>OSTATNIA AKTYWNOŚĆ</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? (
                            <tr><td colSpan={4} style={{ padding: 20, textAlign: "center", opacity: 0.5 }}>Pobieranie danych...</td></tr>
                        ) : sessions.map(s => (
                            <tr key={s.pid} style={{ borderBottom: "1px solid rgba(255,255,255,0.02)" }}>
                                <td style={{ padding: "6px", color: "rgba(86, 204, 242, 0.8)", fontWeight: 600 }}>{s.pid}</td>
                                <td style={{ padding: "6px" }}>{s.machineName}</td>
                                <td style={{ padding: "6px" }}>
                                    <span style={{ fontSize: "9px", fontWeight: 700, padding: "1px 6px", borderRadius: "10px", background: s.state === "active" ? "rgba(69, 160, 73, 0.1)" : "rgba(0,0,0,0.1)", color: s.state === "active" ? "#45a049" : "var(--text-muted)" }}>
                                        {s.state.toUpperCase()}
                                    </span>
                                </td>
                                <td style={{ padding: "6px", color: "var(--text-muted)" }}>{new Date(s.lastQueryTime).toLocaleTimeString()}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
