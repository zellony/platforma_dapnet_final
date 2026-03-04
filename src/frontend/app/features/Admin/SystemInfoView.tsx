import React, { useState, useEffect } from "react";
import { apiFetch } from "../../apiFetch";
import { DapnetIcon } from "../../components/DapnetIcon";

interface SystemModule {
    name: string;
    version: string;
}

interface SystemInfo {
    platformVersion: string;
    releaseDate: string;
    apiVersion: string;
    databaseName: string;
    os: string;
    dotnetVersion: string;
    modules: SystemModule[];
}

export function SystemInfoView() {
    const [info, setInfo] = useState<SystemInfo | null>(null);
    const [showModules, setShowModules] = useState(false);

    useEffect(() => {
        const fetchInfo = async () => {
            try {
                const res = await apiFetch("/system/info");
                if (res.ok) setInfo(await res.json());
            } catch (e) {}
        };
        fetchInfo();
    }, []);

    const InfoItem = ({ label, value, status }: { label: string, value: string, status?: boolean }) => (
        <div className="info-item">
            <span className="info-label">{label}</span>
            <div className="info-value-container">
                {status !== undefined && (
                    <div className={`status-dot ${status ? "status-online" : "status-offline"}`} />
                )}
                <span className="info-value" title={value}>{value || "-"}</span>
            </div>
        </div>
    );

    // ✅ BEZPIECZNE POBIERANIE WERSJI PRZEZ PRELOAD
    const electronApi = (window as any).electron;
    const localVersions = electronApi?.getSystemVersions?.() || {};

    return (
        <div className="system-info-container">
            
            <div className="info-grid-scroll">
                <div className="info-grid">
                    
                    <div style={{ gridColumn: "span 2", fontSize: "8px", fontWeight: 900, color: "var(--accent)", opacity: 0.5, letterSpacing: "1.5px", marginTop: "4px", borderBottom: "1px solid var(--border)", paddingBottom: "2px" }}>
                        RDZEŃ SYSTEMU
                    </div>
                    <InfoItem label="WERSJA PLATFORMY" value={info?.platformVersion || "-"} />
                    <InfoItem label="DATA WYDANIA" value={info?.releaseDate || "-"} />

                    <div style={{ gridColumn: "span 2", fontSize: "8px", fontWeight: 900, color: "var(--accent)", opacity: 0.5, letterSpacing: "1.5px", marginTop: "8px", borderBottom: "1px solid var(--border)", paddingBottom: "2px" }}>
                        BACKEND & BAZA DANYCH
                    </div>
                    <InfoItem label="WERSJA API" value={info?.apiVersion || "-"} />
                    <InfoItem label="RUNTIME .NET" value={info?.dotnetVersion || "-"} />
                    <InfoItem label="NAZWA BAZY" value={info?.databaseName || "-"} />
                    <InfoItem label="STATUS USŁUGI" value={info ? "POŁĄCZONO" : "BRAK POŁĄCZENIA"} status={!!info} />

                    <div style={{ gridColumn: "span 2", fontSize: "8px", fontWeight: 900, color: "var(--accent)", opacity: 0.5, letterSpacing: "1.5px", marginTop: "8px", borderBottom: "1px solid var(--border)", paddingBottom: "2px" }}>
                        PLATFORMA KLIENTA
                    </div>
                    <InfoItem label="SYSTEM OPERACYJNY" value={info?.os || "-"} />
                    <InfoItem label="ARCHITEKTURA" value={localVersions.arch?.toUpperCase() || "-"} />
                    <InfoItem label="WERSJA NODE.JS" value={localVersions.node || "-"} />
                    <InfoItem label="WERSJA ELECTRON" value={localVersions.electron || "-"} />

                </div>
            </div>

            <div className="system-info-footer">
                <button className="primary" style={{ width: "100%", height: "32px", fontSize: "11px" }} onClick={() => setShowModules(!showModules)}>
                    <DapnetIcon nameOrSvg="layout" size={14} />
                    LISTA MODUŁÓW SYSTEMOWYCH ({info?.modules?.length || 0})
                </button>
            </div>

            {showModules && (
                <div className="modules-flyout">
                    <div className="ui-panel-header" style={{ height: "34px" }}>
                        <span className="ui-panel-title" style={{ fontSize: "8px" }}>ZAŁADOWANE MODUŁY</span>
                        <button className="btn-close" onClick={() => setShowModules(false)} style={{ width: 24, height: 24 }}>
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
                        </button>
                    </div>
                    <div className="modules-list" style={{ padding: "8px", display: "flex", flexDirection: "column", gap: "4px" }}>
                        {info?.modules && info.modules.length > 0 ? (
                            info.modules.map((m, idx) => (
                                <div key={idx} className="module-item" style={{ padding: "8px 10px", display: "flex", alignItems: "center", gap: 12, background: "rgba(0,0,0,0.15)", borderRadius: "6px" }}>
                                    <DapnetIcon nameOrSvg="layout" size={14} color="var(--accent)" />
                                    <div style={{ display: "flex", alignItems: "baseline", gap: 8, flex: 1 }}>
                                        <span style={{ fontWeight: 600, fontSize: "11px", color: "var(--text-main)" }}>{m.name}</span>
                                        <span style={{ fontSize: "9px", opacity: 0.4, fontFamily: "'JetBrains Mono', monospace", color: "var(--text-muted)" }}>{m.version}</span>
                                    </div>
                                </div>
                            ))
                        ) : (
                            <div style={{ padding: "20px", textAlign: "center", opacity: 0.5, fontSize: "11px" }}>Brak załadowanych modułów</div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
}
