import React, { useState, useEffect } from "react";
import { apiFetch } from "../../apiFetch";
import { useAuth } from "../../layout/Shell";

export function AddUserView({ onSaved, onCancel }: { onSaved?: () => void, onCancel?: () => void }) {
    const { hasPermission } = useAuth();
    const canWrite = hasPermission("platform.users.write");

    const [login, setLogin] = useState("");
    const [password, setPassword] = useState("");
    const [adUpn, setAdUpn] = useState("");
    const [externalId, setExternalId] = useState("");
    const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
    const [roles, setRoles] = useState<{id: string, name: string}[]>([]);
    const [roleSearch, setRoleSearch] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    useEffect(() => {
        apiFetch("/admin/roles").then(r => r.json()).then(setRoles).catch(console.error);
    }, []);

    const handleSave = async () => {
        if (!canWrite) return;
        if (!login || !password) {
            setError("Login i hasło są wymagane.");
            return;
        }
        setBusy(true);
        setError(null);
        setSuccess(false);
        try {
            const res = await apiFetch("/admin/users", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    login, password, adUpn, externalUserId: externalId,
                    roles: selectedRoles, isActive: true
                })
            });

            if (res.ok) {
                setSuccess(true);
                window.dispatchEvent(new CustomEvent("user-updated"));
                setTimeout(() => { if (onSaved) onSaved(); }, 400);
            } else {
                const err = await res.json();
                setError(err.message || "Błąd podczas dodawania użytkownika.");
            }
        } catch (e) {
            setError("Błąd połączenia z serwerem.");
        } finally {
            setBusy(false);
        }
    };

    const toggleRole = (name: string) => {
        if (!canWrite) return;
        setSelectedRoles(prev => prev.includes(name) ? prev.filter(x => x !== name) : [...prev, name]);
    };

    const filteredRoles = roles.filter(r => r.name.toLowerCase().includes(roleSearch.toLowerCase()));

    const CustomCheckbox = ({ checked }: { checked: boolean }) => (
        <div style={{
            width: 14, height: 14, borderRadius: 3,
            border: `1px solid ${checked ? "rgba(86, 204, 242, 0.3)" : "rgba(143, 160, 178, 0.2)"}`,
            background: checked ? "rgba(86, 204, 242, 0.05)" : "rgba(0,0,0,0.2)",
            display: "flex", alignItems: "center", justifyContent: "center",
            transition: "all 0.2s"
        }}>
            {checked && <div style={{ width: 6, height: 6, borderRadius: 1, background: "rgba(86, 204, 242, 0.5)" }} />}
        </div>
    );

    const labelStyle: React.CSSProperties = { fontSize: "9px", fontWeight: 700, color: "rgba(143, 160, 178, 0.5)", marginBottom: "6px", display: "block", letterSpacing: "0.5px" };
    const inputStyle: React.CSSProperties = { width: "100%", background: "rgba(0,0,0,0.2)", border: "1px solid var(--border)", borderRadius: "6px", color: "rgba(184, 196, 208, 0.8)", padding: "10px", fontSize: "13px", outline: "none" };

    return (
        <div style={{ padding: "20px", display: "flex", flexDirection: "column", gap: 20, width: "100%", maxWidth: "650px" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }}>
                <div className="ui-panel" style={{ padding: "16px" }}>
                    <div style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.4)", marginBottom: 20, letterSpacing: "1px" }}>DANE LOGOWANIA</div>
                    <div className="field" style={{ marginBottom: 12 }}>
                        <label style={labelStyle}>LOGIN UŻYTKOWNIKA *</label>
                        <input autoFocus value={login} onChange={e => setLogin(e.target.value)} placeholder="Wprowadź login..." style={inputStyle} readOnly={!canWrite} />
                    </div>
                    <div className="field">
                        <label style={labelStyle}>HASŁO DOSTĘPU *</label>
                        <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Wprowadź hasło..." style={inputStyle} readOnly={!canWrite} />
                    </div>
                </div>

                <div className="ui-panel" style={{ padding: "16px" }}>
                    <div style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.4)", marginBottom: 20, letterSpacing: "1px" }}>DANE DODATKOWE</div>
                    <div className="field" style={{ marginBottom: 12 }}>
                        <label style={labelStyle}>AD UPN / E-MAIL</label>
                        <input value={adUpn} onChange={e => setAdUpn(e.target.value)} placeholder="użytkownik@domena.pl" style={inputStyle} readOnly={!canWrite} />
                    </div>
                    <div className="field">
                        <label style={labelStyle}>IDENTYFIKATOR ZEWNĘTRZNY</label>
                        <input value={externalId} onChange={e => setExternalId(e.target.value)} placeholder="ID z innego systemu" style={inputStyle} readOnly={!canWrite} />
                    </div>
                </div>
            </div>

            <div className="ui-panel" style={{ padding: "16px", width: "320px" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                    <div style={{ fontSize: "10px", fontWeight: 700, color: "rgba(86, 204, 242, 0.4)", letterSpacing: "1px" }}>ROLE</div>
                    <input 
                        value={roleSearch} 
                        onChange={e => setRoleSearch(e.target.value)} 
                        placeholder="Szukaj..." 
                        style={{ ...inputStyle, width: "120px", padding: "3px 8px", fontSize: "10px" }} 
                    />
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 1, maxHeight: "120px", overflowY: "auto", padding: "2px", background: "rgba(0,0,0,0.1)", borderRadius: 6, border: "1px solid var(--bg-soft)" }}>
                    {filteredRoles.map((r) => {
                        const isChecked = selectedRoles.includes(r.name);
                        return (
                            <div 
                                key={r.id} 
                                onClick={() => toggleRole(r.name)}
                                style={{ 
                                    display: "flex", alignItems: "center", padding: "6px 10px", gap: 10, 
                                    cursor: canWrite ? "pointer" : "default", borderBottom: "1px solid rgba(255,255,255,0.02)",
                                    background: isChecked ? "rgba(86, 204, 242, 0.03)" : "transparent",
                                    transition: "all 0.2s"
                                }}
                            >
                                <CustomCheckbox checked={isChecked} />
                                <span style={{ fontSize: "11px", color: isChecked ? "rgba(184, 196, 208, 0.9)" : "rgba(143, 160, 178, 0.6)" }}>{r.name}</span>
                            </div>
                        );
                    })}
                </div>
            </div>

            {error && (
                <div style={{ padding: "12px", background: "rgba(235, 87, 87, 0.05)", border: "1px solid rgba(235, 87, 87, 0.15)", borderRadius: 8, color: "rgba(235, 87, 87, 0.8)", fontSize: "12px", textAlign: "center" }}>
                    ⚠️ {error}
                </div>
            )}

            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "10px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    {canWrite && (
                        <button 
                            className="primary" 
                            onClick={handleSave} 
                            disabled={busy || success} 
                            style={{ 
                                display: "flex", alignItems: "center", gap: 10, padding: "10px 24px", fontSize: "13px", fontWeight: 600,
                                // ✅ STONOWANY KOLOR BAZOWY I HOVER
                                background: "rgba(43, 74, 111, 0.6)",
                                border: "1px solid rgba(86, 204, 242, 0.2)",
                                color: "rgba(86, 204, 242, 0.8)",
                                transition: "all 0.2s"
                            }}
                            onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(43, 74, 111, 0.8)"; e.currentTarget.style.borderColor = "rgba(86, 204, 242, 0.4)"; }}
                            onMouseLeave={(e) => { e.currentTarget.style.background = "rgba(43, 74, 111, 0.6)"; e.currentTarget.style.borderColor = "rgba(86, 204, 242, 0.2)"; }}
                        >
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><line x1="20" y1="8" x2="20" y2="14"/><line x1="17" y1="11" x2="23" y2="11"/></svg>
                            {busy ? "PRZETWARZANIE..." : "DODAJ UŻYTKOWNIKA"}
                        </button>
                    )}
                    {success && <span style={{ color: "rgba(74, 222, 128, 0.8)", fontSize: "12px", fontWeight: 600 }}>✔ Gotowe</span>}
                </div>

                {onCancel && (
                    <button 
                        onClick={onCancel}
                        style={{
                            background: "rgba(235, 87, 87, 0.05)",
                            border: "1px solid rgba(235, 87, 87, 0.15)",
                            color: "rgba(235, 87, 87, 0.7)",
                            padding: "10px 24px",
                            borderRadius: "8px",
                            cursor: "pointer",
                            fontWeight: 600,
                            fontSize: "12px",
                            transition: "all 0.2s"
                        }}
                        onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(235, 87, 87, 0.15)"; e.currentTarget.style.borderColor = "rgba(235, 87, 87, 0.4)"; }}
                        onMouseLeave={(e) => { e.currentTarget.style.background = "rgba(235, 87, 87, 0.05)"; e.currentTarget.style.borderColor = "rgba(235, 87, 87, 0.15)"; }}
                    >
                        {canWrite ? "ANULUJ" : "ZAMKNIJ"}
                    </button>
                )}
            </div>
        </div>
    );
}
