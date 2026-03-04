import React, { useState } from "react";
import { apiFetch, getErrorMessage } from "../../apiFetch";
import iconImg from "../../assets/icon.png";

interface CompanySetupViewProps {
    onDone: () => void;
}

export function CompanySetupView({ onDone }: CompanySetupViewProps) {
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

    const handleSave = async () => {
        if (!fullName || !nip) {
            setError("NAZWA FIRMY I NIP S• WYMAGANE.");
            return;
        }
        setBusy(true);
        setError("");
        try {
            const res = await apiFetch("/company", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    fullName, shortName, nip, address, city, postalCode, email, phoneNumber: phone
                })
            });

            if (res.ok) {
                onDone();
            } else {
                const msg = await getErrorMessage(res);
                setError("B£•D ZAPISU: " + msg);
            }
        } catch (e) {
            setError("B£•D PO£•CZENIA Z SERWEREM.");
        } finally {
            setBusy(false);
        }
    };

    const handleCancel = () => {
        if ((window as any).electron) {
            (window as any).electron.quitApp();
        }
    };

    const btnStyle: React.CSSProperties = { width: "180px", padding: "10px 0", fontSize: "11px" };

    return (
        <div className="setup-container">
            <div className="setup-box" style={{ width: "520px" }}>
                <div className="setup-header">
                    <img src={iconImg} alt="DAPNET" style={{ width: "48px", height: "48px", marginBottom: "12px", opacity: 0.6 }} />
                    <h2 className="setup-title">DANE FIRMY</h2>
                    <div className="setup-subtitle">KONFIGURACJA W£AåCICIELA LICENCJI</div>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    <div className="setup-field">
                        <label className="setup-label">PE£NA NAZWA FIRMY *</label>
                        <input autoFocus className="setup-input" value={fullName} onChange={e => setFullName(e.target.value)} placeholder="np. PrzedsiÍbiorstwo Handlowe XYZ Sp. z o.o." disabled={busy} />
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
                        <div className="setup-field">
                            <label className="setup-label">NAZWA SKR”CONA</label>
                            <input className="setup-input" value={shortName} onChange={e => setShortName(e.target.value)} placeholder="np. PH XYZ" disabled={busy} />
                        </div>
                        <div className="setup-field">
                            <label className="setup-label">NIP *</label>
                            <input className="setup-input" value={nip} onChange={e => setNip(e.target.value)} placeholder="np. 1234567890" disabled={busy} />
                        </div>
                    </div>

                    <div className="setup-field">
                        <label className="setup-label">ADRES (ULICA I NUMER)</label>
                        <input className="setup-input" value={address} onChange={e => setAddress(e.target.value)} placeholder="np. ul. Przemys≥owa 10/4" disabled={busy} />
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "120px 1fr", gap: 16 }}>
                        <div className="setup-field">
                            <label className="setup-label">KOD POCZTOWY</label>
                            <input className="setup-input" value={postalCode} onChange={e => setPostalCode(e.target.value)} placeholder="00-000" disabled={busy} />
                        </div>
                        <div className="setup-field">
                            <label className="setup-label">MIASTO</label>
                            <input className="setup-input" value={city} onChange={e => setCity(e.target.value)} placeholder="np. Warszawa" disabled={busy} />
                        </div>
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
                        <div className="setup-field">
                            <label className="setup-label">E-MAIL</label>
                            <input className="setup-input" value={email} onChange={e => setEmail(e.target.value)} placeholder="biuro@firma.pl" disabled={busy} />
                        </div>
                        <div className="setup-field">
                            <label className="setup-label">TELEFON</label>
                            <input className="setup-input" value={phone} onChange={e => setPhone(e.target.value)} placeholder="+48 123 456 789" disabled={busy} />
                        </div>
                    </div>

                    {error && (
                        <div style={{ marginTop: 10, padding: "10px", borderRadius: 8, background: "rgba(235, 87, 87, 0.05)", border: "1px solid rgba(235, 87, 87, 0.2)", color: "var(--danger)", fontSize: "11px", textAlign: "center", fontWeight: 600 }}>
                            {error}
                        </div>
                    )}

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 20 }}>
                        <button 
                            className="secondary danger"
                            onClick={handleCancel} 
                            disabled={busy} 
                            style={{ width: "auto", border: "none", background: "transparent", fontSize: "10px", opacity: 0.6 }}
                        >
                            ANULUJ I WYJDè
                        </button>
                        
                        <button 
                            className="primary" 
                            onClick={handleSave} 
                            disabled={busy} 
                            style={btnStyle}
                        >
                            ZAPISZ I KONTYNUUJ
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
