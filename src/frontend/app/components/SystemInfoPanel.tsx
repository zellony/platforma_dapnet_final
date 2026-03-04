import React, { useEffect, useState } from "react";
import { apiFetch } from "../apiFetch";

interface SystemInfoPanelProps {
    onClose: () => void;
    onOpenSessions: () => void;
    isSidebarCollapsed: boolean; // ✅ NOWY PROP
}

export function SystemInfoPanel({ onClose, onOpenSessions, isSidebarCollapsed }: SystemInfoPanelProps) {
    const [dbInfo, setDbInfo] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const [showDetails, setShowDetails] = useState(false);

    useEffect(() => {
        apiFetch("/system/db-info")
            .then(r => r.json())
            .then(d => {
                setDbInfo(d);
                setLoading(false);
            })
            .catch(() => setLoading(false));
    }, []);

    // ✅ DYNAMICZNA POZYCJA LEWA
    const leftPos = isSidebarCollapsed ? "60px" : "290px";
    const detailsLeftPos = isSidebarCollapsed ? "390px" : "620px"; // 290 + 320 + 10

    return (
        <>
            {/* GŁÓWNY PANEL */}
            <div style={{
                position: "fixed", bottom: "50px", left: leftPos, width: "320px",
                background: "var(--bg-panel)", border: "1px solid var(--border)", borderRadius: "10px",
                boxShadow: "0 10px 40px rgba(0,0,0,0.6)", zIndex: 6000, display: "flex", flexDirection: "column",
                overflow: "hidden", animation: "slideUp 0.3s ease-out", transition: "left 0.2s ease-out" // ✅ PŁYNNE PRZESUWANIE
            }}>
                <div style={{ padding: "8px 12px", background: "rgba(86, 204, 242, 0.05)", borderBottom: "1px solid var(--bg-soft)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <span style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.8)", letterSpacing: "0.5px" }}>SYSTEM - INFORMACJE</span>
                    <button onClick={onClose} style={{ background: "transparent", border: "none", color: "var(--text-muted)", cursor: "pointer", padding: "2px" }}>✕</button>
                </div>

                <div style={{ padding: "12px", display: "flex", flexDirection: "column", gap: 10 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "11px" }}>
                        <span style={{ color: "var(--text-muted)" }}>Wersja aplikacji:</span>
                        <span style={{ color: "var(--text-main)" }}>1.0.4-stable</span>
                    </div>
                    
                    <div 
                        onClick={() => setShowDetails(!showDetails)}
                        style={{ display: "flex", justifyContent: "space-between", fontSize: "11px", cursor: "pointer", padding: "4px 0", borderRadius: "4px", transition: "all 0.2s" }}
                        onMouseEnter={(e) => e.currentTarget.style.background = "rgba(255,255,255,0.03)"}
                        onMouseLeave={(e) => e.currentTarget.style.background = "transparent"}
                    >
                        <span style={{ color: "var(--text-muted)" }}>Baza danych:</span>
                        <span style={{ color: "rgba(86, 204, 242, 0.8)", fontWeight: 600, borderBottom: "1px dashed rgba(86, 204, 242, 0.4)" }}>
                            {loading ? "Pobieranie..." : (dbInfo?.databaseName || "Błąd")}
                        </span>
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "11px" }}>
                        <span style={{ color: "var(--text-muted)" }}>Środowisko:</span>
                        <span style={{ color: "var(--text-main)" }}>PRODUKCJA</span>
                    </div>
                </div>
            </div>

            {/* PANEL SZCZEGÓŁÓW */}
            {showDetails && dbInfo && (
                <div style={{
                    position: "fixed", bottom: "50px", left: detailsLeftPos, width: "350px",
                    background: "var(--bg-panel)", border: "1px solid var(--border)", borderRadius: "10px",
                    boxShadow: "20px 10px 50px rgba(0,0,0,0.5)", zIndex: 6001, display: "flex", flexDirection: "column",
                    animation: "slideRight 0.3s ease-out", transition: "left 0.2s ease-out"
                }}>
                    <div style={{ padding: "8px 12px", borderBottom: "1px solid var(--bg-soft)", fontSize: "10px", fontWeight: 700, color: "var(--text-muted)" }}>
                        SZCZEGÓŁY POŁĄCZENIA POSTGRESQL
                    </div>
                    <div style={{ padding: "12px", display: "flex", flexDirection: "column", gap: 12 }}>
                        <div className="detail-row">
                            <label style={{ fontSize: "9px", color: "var(--text-muted)", display: "block", marginBottom: 2 }}>WERSJA SERWERA</label>
                            <div style={{ fontSize: "11px", color: "var(--text-main)", lineHeight: 1.4 }}>{dbInfo.version}</div>
                        </div>
                        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                            <div>
                                <label style={{ fontSize: "9px", color: "var(--text-muted)", display: "block", marginBottom: 2 }}>ROZMIAR BAZY</label>
                                <div style={{ fontSize: "13px", color: "rgba(86, 204, 242, 0.8)", fontWeight: 600 }}>{dbInfo.size}</div>
                            </div>
                            
                            <div 
                                onClick={onOpenSessions}
                                style={{ cursor: "pointer", padding: "4px", borderRadius: "6px", transition: "all 0.2s" }}
                                onMouseEnter={(e) => e.currentTarget.style.background = "rgba(86, 204, 242, 0.05)"}
                                onMouseLeave={(e) => e.currentTarget.style.background = "transparent"}
                            >
                                <label style={{ fontSize: "9px", color: "var(--text-muted)", display: "block", marginBottom: 2 }}>AKTYWNE SESJE ↗</label>
                                <div style={{ fontSize: "13px", color: "rgba(86, 204, 242, 0.8)", fontWeight: 600, textDecoration: "underline", textDecorationStyle: "dotted" }}>
                                    {dbInfo.activeSessions}
                                </div>
                            </div>
                        </div>
                        <div className="detail-row">
                            <label style={{ fontSize: "9px", color: "var(--text-muted)", display: "block", marginBottom: 2 }}>ADRES SERWERA</label>
                            <div style={{ fontSize: "11px", color: "var(--text-main)", fontFamily: "monospace", background: "rgba(0,0,0,0.2)", padding: "4px 8px", borderRadius: 4 }}>
                                {dbInfo.serverAddress}
                            </div>
                        </div>
                    </div>
                </div>
            )}

            <style>{`
                @keyframes slideUp { from { transform: translateY(20px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }
                @keyframes slideRight { from { transform: translateX(-20px); opacity: 0; } to { transform: translateX(0); opacity: 1; } }
            `}</style>
        </>
    );
}
