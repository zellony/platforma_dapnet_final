import React, { useState, useEffect } from "react";
import { apiFetch, getErrorMessage } from "../../apiFetch";
import iconImg from "../../assets/icon.png";

const REMEMBERED_LOGIN_KEY = "platform_remembered_login";

export function CreateFirstAdminView({ onDone }: { onDone: () => void }) {
    const [login, setLogin] = useState("");
    const [password, setPassword] = useState("");
    const [confirmPassword, setPasswordConfirm] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // ? AUTOMATYCZNE CZYSZCZENIE B£ D”W
    useEffect(() => {
        if (error) {
            const timer = setTimeout(() => setError(null), 5000);
            return () => clearTimeout(timer);
        }
    }, [error]);

    const handleCreate = async () => {
        if (!login || !password) {
            setError("LOGIN I HAS£O S• WYMAGANE.");
            return;
        }
        if (password !== confirmPassword) {
            setError("HAS£A NIE S• IDENTYCZNE.");
            return;
        }
        
        setBusy(true);
        setError(null);
        try {
            const res = await apiFetch("/admin/users/create-first-admin", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ login, password })
            });

            if (res.ok) {
                localStorage.setItem(REMEMBERED_LOGIN_KEY, login);
                onDone();
            } else {
                const msg = await getErrorMessage(res);
                setError(msg);
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
            <div className="setup-box" style={{ width: "450px" }}>
                <div className="setup-header">
                    <div style={{ width: "64px", height: "64px", background: "rgba(86, 204, 242, 0.05)", borderRadius: "16px", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 16px", border: "1px solid rgba(86, 204, 242, 0.1)" }}>
                        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="var(--accent)" strokeWidth="2"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><polyline points="17 11 19 13 23 9"/></svg>
                    </div>
                    <h2 className="setup-title">KONTO ADMINISTRATORA</h2>
                    <div className="setup-subtitle">UTW”RZ PIERWSZE KONTO SYSTEMOWE</div>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    <div className="setup-field">
                        <label className="setup-label">LOGIN ADMINISTRATORA</label>
                        <input autoFocus className="setup-input" value={login} onChange={e => setLogin(e.target.value)} placeholder="np. admin" disabled={busy} />
                    </div>

                    <div className="setup-field">
                        <label className="setup-label">HAS£O DOST PU</label>
                        <input className="setup-input" type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Wpisz has≥o..." disabled={busy} />
                    </div>

                    <div className="setup-field">
                        <label className="setup-label">POWT”RZ HAS£O</label>
                        <input className="setup-input" type="password" value={confirmPassword} onChange={e => setPasswordConfirm(e.target.value)} placeholder="PowtÛrz has≥o..." disabled={busy} onKeyDown={e => e.key === "Enter" && handleCreate()} />
                    </div>

                    {error && (
                        <div style={{ 
                            marginTop: 10, marginBottom: 15, padding: "10px", borderRadius: 8, 
                            background: "rgba(235, 87, 87, 0.05)", border: "1px solid rgba(235, 87, 87, 0.2)", 
                            color: "var(--danger)", fontSize: "11px", textAlign: "center", fontWeight: 600 
                        }}>
                            ? {error}
                        </div>
                    )}

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 15 }}>
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
                            onClick={handleCreate} 
                            disabled={busy} 
                            style={btnStyle}
                        >
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ marginRight: 8 }}><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                            {busy ? "TWORZENIE..." : "UTW”RZ KONTO"}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
