import React, { useState, useEffect } from "react";
import { apiFetch } from "../../apiFetch";
import { useToast } from "../../layout/Shell";

export function KsefAuthView({ onClose }: { onClose?: () => void }) {
    const [nip, setNip] = useState("");
    const [token, setToken] = useState("");
    const [env, setEnv] = useState("TEST");
    const [busy, setBusy] = useState(false);
    const [status, setStatus] = useState<any>(null);
    const { addToast } = useToast();

    const loadConfig = async () => {
        try {
            const res = await apiFetch("/ksef/config");
            if (res.ok) {
                const data = await res.json();
                setNip(data.nip || "");
                setToken(data.token || "");
                setEnv(data.environment || "TEST");
                setStatus(data.status);
            }
        } catch (e) { console.error(e); }
    };

    useEffect(() => { loadConfig(); }, []);

    const handleSave = async () => {
        setBusy(true);
        try {
            const res = await apiFetch("/ksef/config", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ nip, token, environment: env })
            });

            if (res.ok) {
                addToast("Konfiguracja KSeF została zapisana", "success");
                loadConfig();
            } else {
                addToast("Błąd podczas zapisywania konfiguracji", "error");
            }
        } catch (e) {
            addToast("Błąd połączenia z serwerem", "error");
        } finally {
            setBusy(false);
        }
    };

    const handleTestConnection = async () => {
        setBusy(true);
        try {
            const res = await apiFetch("/ksef/test-connection", { method: "POST" });
            const data = await res.json();
            if (data.ok) {
                addToast("Połączenie z KSeF nawiązane pomyślnie!", "success");
            } else {
                addToast(`Błąd połączenia: ${data.message}`, "error");
            }
            loadConfig();
        } catch (e) {
            addToast("Błąd komunikacji z bramką KSeF", "error");
        } finally {
            setBusy(false);
        }
    };

    return (
        // ✅ DODANO minWidth i minHeight
        <div style={{ padding: "16px", display: "flex", flexDirection: "column", gap: 16, width: "500px", height: "100%", minWidth: "550px", minHeight: "450px" }}>
            <div className="ui-panel" style={{ padding: "20px" }}>
                <div style={{ fontSize: "10px", fontWeight: 700, color: "var(--text-muted)", marginBottom: 20, letterSpacing: "1px" }}>AUTORYZACJA KSEF</div>
                
                <div className="field">
                    <label>NIP FIRMY</label>
                    <input value={nip} onChange={e => setNip(e.target.value)} placeholder="Wpisz NIP bez kresek..." />
                </div>

                <div className="field">
                    <label>TOKEN AUTORYZACYJNY (Z PORTALU KSEF)</label>
                    <input type="password" value={token} onChange={e => setToken(e.target.value)} placeholder="Wklej wygenerowany token..." />
                </div>

                <div className="field">
                    <label>ŚRODOWISKO</label>
                    <select 
                        value={env} 
                        onChange={e => setEnv(e.target.value)}
                        style={{ width: "100%", background: "var(--bg-main)", border: "1px solid var(--border)", borderRadius: "6px", padding: "8px", color: "var(--text-main)", outline: "none" }}
                    >
                        <option value="TEST">TESTOWE (Demo)</option>
                        <option value="PROD">PRODUKCYJNE</option>
                    </select>
                </div>

                <div style={{ marginTop: 24, display: "flex", gap: 12 }}>
                    <button className="primary" onClick={handleSave} disabled={busy}>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ marginRight: 8, verticalAlign: "middle" }}><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                        Zapisz konfigurację
                    </button>
                    <button onClick={handleTestConnection} disabled={busy || !token}>
                        Sprawdź połączenie
                    </button>
                </div>
            </div>

            {status && (
                <div className="ui-panel" style={{ padding: "16px", borderLeft: `4px solid ${status.authenticated ? "#45a049" : "var(--danger)"}` }}>
                    <div style={{ fontSize: "11px", fontWeight: 600, color: "var(--text-main)" }}>STATUS POŁĄCZENIA</div>
                    <div style={{ fontSize: "13px", marginTop: 8, color: status.authenticated ? "#45a049" : "var(--danger)" }}>
                        {status.authenticated ? "Zautoryzowano poprawnie" : "Brak aktywnej sesji"}
                    </div>
                    {status.lastCheck && (
                        <div style={{ fontSize: "10px", color: "var(--text-muted)", marginTop: 4 }}>
                            Ostatnia weryfikacja: {new Date(status.lastCheck).toLocaleString()}
                        </div>
                    )}
                </div>
            )}

            {/* ✅ PRZYCISK ZAMKNIJ */}
            {onClose && (
                <div style={{ marginTop: "auto", paddingTop: 12, display: "flex", justifyContent: "flex-end" }}>
                    <button className="btn-danger" onClick={onClose}>Zamknij</button>
                </div>
            )}
        </div>
    );
}
