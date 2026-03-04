import React, { useEffect, useState } from "react";
import { apiFetch } from "../../apiFetch";

interface LicenseStatus {
    id?: string; isActive: boolean; message: string; expiresAt?: string; nip?: string; daysLeft?: number; modules?: string[]; seats?: number; updateUntil?: string;
}

export function LicenseView({ onClose }: { onClose?: () => void }) {
    const [status, setStatus] = useState<LicenseStatus | null>(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    useEffect(() => { loadStatus(); }, []);

    const loadStatus = async () => {
        try {
            const res = await apiFetch("/license/status");
            if (res.ok) setStatus(await res.json());
        } catch (e) {}
    };

    const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;
        setBusy(true); setError(""); setSuccess("");
        const reader = new FileReader();
        reader.onload = async (ev) => {
            const content = ev.target?.result as string;
            try {
                const res = await apiFetch("/license/upload", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ licenseContent: content })
                });
                if (res.ok) { setSuccess("Licencja została pomyślnie wgrana."); loadStatus(); }
                else { const err = await res.text(); setError(err || "Błąd podczas wgrywania licencji."); }
            } catch (ex) { setError("Błąd połączenia."); }
            finally { setBusy(false); }
        };
        reader.readAsText(file);
    };

    const InfoRow = ({ label, value, color, isId }: { label: string, value: any, color?: string, isId?: boolean }) => (
        <div style={{ display: "flex", justifyContent: "space-between", padding: "8px 0", borderBottom: "1px solid var(--bg-soft)" }}>
            <span style={{ fontSize: "11px", color: "rgba(143, 160, 178, 0.5)", fontWeight: 500 }}>{label}</span>
            <span style={{ fontSize: isId ? "10px" : "12px", fontWeight: 600, color: color || "rgba(184, 196, 208, 0.7)", fontFamily: isId ? "'JetBrains Mono', monospace" : "inherit" }}>{value || "-"}</span>
        </div>
    );

    return (
        <div className="view-container">
            <div className="view-body">
                <div style={{ display: "flex", gap: 20, flexWrap: "wrap" }}>
                    <div style={{ flex: "1 1 500px", display: "flex", flexDirection: "column", gap: 20 }}>
                        <div className="ui-panel" style={{ padding: "20px" }}>
                            <div style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.4)", marginBottom: 20, letterSpacing: "1px" }}>STATUS LICENCJI</div>
                            <div style={{ padding: "16px", borderRadius: "10px", background: status?.isActive ? "rgba(74, 222, 128, 0.02)" : "rgba(235, 87, 87, 0.02)", border: `1px solid ${status?.isActive ? "rgba(74, 222, 128, 0.1)" : "rgba(235, 87, 87, 0.1)"}`, textAlign: "center", marginBottom: 20 }}>
                                <div style={{ fontSize: "18px", fontWeight: 800, color: status?.isActive ? "rgba(74, 222, 128, 0.6)" : "rgba(235, 87, 87, 0.6)", letterSpacing: "1px" }}>{status?.message?.toUpperCase() || "POBIERANIE..."}</div>
                            </div>
                            <InfoRow label="IDENTYFIKATOR" value={status?.id} isId={true} />
                            <InfoRow label="NIP PODMIOTU" value={status?.nip} />
                            <InfoRow label="TERMIN WAŻNOŚCI" value={status?.expiresAt ? new Date(status.expiresAt).toLocaleDateString() : null} />
                            <InfoRow label="POZOSTAŁO DNI" value={status?.daysLeft} color={status?.daysLeft && status.daysLeft < 30 ? "rgba(245, 158, 11, 0.6)" : undefined} />
                            <InfoRow label="LIMIT STANOWISK" value={status?.seats} />
                            <InfoRow label="WSPARCIE TECHNICZNE" value={status?.updateUntil ? new Date(status.updateUntil).toLocaleDateString() : null} />
                        </div>

                        <div className="ui-panel" style={{ padding: "20px" }}>
                            <div style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.4)", marginBottom: 12, letterSpacing: "1px" }}>AKTUALIZACJA</div>
                            <p style={{ fontSize: "12px", color: "rgba(143, 160, 178, 0.6)", marginBottom: 20, lineHeight: "1.5" }}>Wgraj nowy plik licencji (<code>.lic</code>), aby przedłużyć subskrypcję.</p>
                            <label className="primary" style={{ display: "inline-flex", alignItems: "center", gap: 10, padding: "10px 24px", borderRadius: "8px", cursor: busy ? "wait" : "pointer", fontSize: "13px", fontWeight: 600, background: "rgba(43, 74, 111, 0.4)", border: "1px solid rgba(86, 204, 242, 0.15)", color: "rgba(86, 204, 242, 0.6)" }}>
                                <input type="file" accept=".lic" onChange={handleFileUpload} disabled={busy} style={{ display: "none" }} />
                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" i1="3" x2="12" y2="15"/></svg>
                                {busy ? "PRZETWARZANIE..." : "WGRAJ PLIK LICENCJI (.LIC)"}
                            </label>
                            {error && <div style={{ marginTop: 16, color: "rgba(235, 87, 87, 0.6)", fontSize: "12px" }}>⚠️ {error}</div>}
                            {success && <div style={{ marginTop: 16, color: "rgba(74, 222, 128, 0.6)", fontSize: "12px" }}>✔ {success}</div>}
                        </div>
                    </div>

                    <div className="ui-panel" style={{ width: "260px", padding: "20px", flexShrink: 0 }}>
                        <div style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.4)", marginBottom: 20, letterSpacing: "1px" }}>AKTYWNE MODUŁY</div>
                        {status?.modules?.map(m => (
                            <div key={m} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 0", fontSize: "11px", color: "rgba(184, 196, 208, 0.6)", borderBottom: "1px solid rgba(255,255,255,0.02)" }}>
                                <div style={{ width: 4, height: 4, borderRadius: "50%", background: "rgba(86, 204, 242, 0.2)" }}></div>
                                {m.toUpperCase()}
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            <div className="view-footer">
                <button onClick={onClose} style={{ padding: "10px 30px", fontSize: "12px", fontWeight: 600, borderRadius: 8, cursor: "pointer", background: "rgba(235, 87, 87, 0.05)", border: "1px solid rgba(235, 87, 87, 0.2)", color: "rgba(235, 87, 87, 0.7)" }}>ZAMKNIJ</button>
            </div>
        </div>
    );
}
