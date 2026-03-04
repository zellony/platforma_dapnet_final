import React, { useEffect, useState } from "react";
import { apiFetch } from "../../apiFetch";

export function CompanyEditView({ onClose }: { onClose?: () => void }) {
    const [fullName, setFullName] = useState("");
    const [shortName, setShortName] = useState("");
    const [nip, setNip] = useState("");
    const [address, setAddress] = useState("");
    const [city, setCity] = useState("");
    const [postalCode, setPostalCode] = useState("");
    const [email, setEmail] = useState("");
    const [phone, setPhone] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    useEffect(() => { loadData(); }, []);

    const loadData = async () => {
        setBusy(true);
        try {
            const res = await apiFetch("/company");
            if (res.ok) {
                const data = await res.json();
                setFullName(data.fullName || "");
                setShortName(data.shortName || "");
                setNip(data.nip || "");
                setAddress(data.address || "");
                setCity(data.city || "");
                setPostalCode(data.postalCode || "");
                setEmail(data.email || "");
                setPhone(data.phoneNumber || "");
            }
        } catch (e) { setError("Nie udało się pobrać danych firmy."); }
        finally { setBusy(false); }
    };

    const handleSave = async () => {
        if (!fullName || !nip) { setError("NAZWA FIRMY I NIP SĄ WYMAGANE."); return; }
        setBusy(true); setError(""); setSuccess("");
        try {
            const res = await apiFetch("/company", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ fullName, shortName, nip, address, city, postalCode, email, phoneNumber: phone })
            });
            if (res.ok) { setSuccess("DANE FIRMY ZOSTAŁY ZAKTUALIZOWANE."); }
            else { const err = await res.text(); setError("BŁĄD ZAPISU: " + err); }
        } catch (e) { setError("BŁĄD POŁĄCZENIA Z SERWEREM."); }
        finally { setBusy(false); }
    };

    const labelStyle: React.CSSProperties = { fontSize: "9px", fontWeight: 700, color: "rgba(143, 160, 178, 0.5)", marginBottom: "4px", display: "block", letterSpacing: "0.5px" };
    const inputStyle: React.CSSProperties = { width: "100%", background: "rgba(0,0,0,0.2)", border: "1px solid var(--border)", borderRadius: "6px", color: "rgba(184, 196, 208, 0.8)", padding: "8px 10px", fontSize: "12px", outline: "none" };

    return (
        <div className="view-container">
            <div className="view-body">
                <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: "800px" }}>
                    <div className="ui-panel" style={{ padding: "20px", background: "rgba(0,0,0,0.1)" }}>
                        <div style={{ fontSize: "10px", fontWeight: 700, color: "var(--accent)", marginBottom: 16, letterSpacing: "1px", opacity: 0.6 }}>DANE PODSTAWOWE</div>
                        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                            <div className="field">
                                <label style={labelStyle}>PEŁNA NAZWA FIRMY *</label>
                                <input value={fullName} onChange={e => setFullName(e.target.value)} disabled={busy} style={inputStyle} />
                            </div>
                            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                                <div className="field">
                                    <label style={labelStyle}>NAZWA SKRÓCONA</label>
                                    <input value={shortName} onChange={e => setShortName(e.target.value)} disabled={busy} style={inputStyle} />
                                </div>
                                <div className="field">
                                    <label style={labelStyle}>NIP *</label>
                                    <input value={nip} onChange={e => setNip(e.target.value)} disabled={busy} style={inputStyle} />
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className="ui-panel" style={{ padding: "20px", background: "rgba(0,0,0,0.1)" }}>
                        <div style={{ fontSize: "10px", fontWeight: 700, color: "var(--accent)", marginBottom: 16, letterSpacing: "1px", opacity: 0.6 }}>ADRES I KONTAKT</div>
                        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                            <div className="field">
                                <label style={labelStyle}>ADRES (ULICA I NUMER)</label>
                                <input value={address} onChange={e => setAddress(e.target.value)} disabled={busy} style={inputStyle} />
                            </div>
                            <div style={{ display: "grid", gridTemplateColumns: "100px 1fr", gap: 12 }}>
                                <div className="field">
                                    <label style={labelStyle}>KOD POCZTOWY</label>
                                    <input value={postalCode} onChange={e => setPostalCode(e.target.value)} disabled={busy} style={inputStyle} />
                                </div>
                                <div className="field">
                                    <label style={labelStyle}>MIASTO</label>
                                    <input value={city} onChange={e => setCity(e.target.value)} disabled={busy} style={inputStyle} />
                                </div>
                            </div>
                            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                                <div className="field">
                                    <label style={labelStyle}>E-MAIL</label>
                                    <input value={email} onChange={e => setEmail(e.target.value)} disabled={busy} style={inputStyle} />
                                </div>
                                <div className="field">
                                    <label style={labelStyle}>TELEFON</label>
                                    <input value={phone} onChange={e => setPhone(e.target.value)} disabled={busy} style={inputStyle} />
                                </div>
                            </div>
                        </div>
                    </div>

                    {error && <div style={{ padding: "10px", background: "rgba(235, 87, 87, 0.1)", border: "1px solid rgba(235, 87, 87, 0.2)", borderRadius: 8, color: "rgba(235, 87, 87, 0.8)", fontSize: "11px", textAlign: "center", fontWeight: 600 }}>{error}</div>}
                </div>
            </div>

            <div className="view-footer">
                <div style={{ display: "flex", alignItems: "center", gap: 12, flex: 1 }}>
                    <button className="primary" onClick={handleSave} disabled={busy} style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 20px", fontSize: "12px", fontWeight: 700, background: "rgba(43, 74, 111, 0.6)", border: "1px solid rgba(86, 204, 242, 0.2)", color: "rgba(86, 204, 242, 0.8)", borderRadius: 8, cursor: "pointer" }}>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                        {busy ? "ZAPISYWANIE..." : "ZAPISZ ZMIANY"}
                    </button>
                    {success && <span style={{ color: "rgba(74, 222, 128, 0.7)", fontSize: "11px", fontWeight: 700 }}>✔ ZAPISANO</span>}
                </div>
                {onClose && <button onClick={onClose} style={{ padding: "10px 20px", fontSize: "12px", fontWeight: 600, borderRadius: 8, cursor: "pointer", background: "rgba(235, 87, 87, 0.05)", border: "1px solid rgba(235, 87, 87, 0.2)", color: "rgba(235, 87, 87, 0.7)" }}>ZAMKNIJ</button>}
            </div>
        </div>
    );
}
