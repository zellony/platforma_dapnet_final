import React, { useState, useEffect } from "react";
import { apiFetch } from "../../apiFetch";
import { useAuth } from "../../layout/Shell"; // ✅ IMPORT HOOKA

interface UserDto {
    id: string;
    login: string;
    isActive: boolean;
    externalUserId?: string | null;
    adUpn?: string | null;
    roles: string[];
}

export function EditUserView({ userData, onSaved, onCancel }: { userData: UserDto, onSaved?: () => void, onCancel?: () => void }) {
    const { hasPermission } = useAuth();
    const canWrite = hasPermission("platform.users.write"); // ✅ SPRAWDZANIE UPRAWNIEŃ

    const [password, setPassword] = useState("");
    const [adUpn, setAdUpn] = useState(userData.adUpn || "");
    const [externalId, setExternalId] = useState(userData.externalUserId || "");
    const [selectedRoles, setSelectedRoles] = useState<string[]>(userData.roles || []);
    const [roles, setRoles] = useState<{id: string, name: string}[]>([]);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    useEffect(() => {
        apiFetch("/admin/roles").then(r => r.json()).then(setRoles).catch(console.error);
    }, []);

    const handleSave = async () => {
        if (!canWrite) return; // ✅ DODATKOWA BLOKADA
        setBusy(true);
        setError(null);
        setSuccess(false);
        try {
            const res = await apiFetch(`/admin/users/${userData.id}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    login: userData.login,
                    password: password || null,
                    adUpn: adUpn, 
                    externalUserId: externalId,
                    roles: selectedRoles,
                    isActive: userData.isActive
                })
            });

            if (res.ok) {
                setSuccess(true);
                window.dispatchEvent(new CustomEvent("user-updated"));
                setTimeout(() => {
                    if (onSaved) onSaved();
                }, 400);
            } else {
                const err = await res.json();
                setError(err.message || "Błąd podczas aktualizacji użytkownika.");
            }
        } catch (e) {
            setError("Błąd połączenia z serwerem.");
        } finally {
            setBusy(false);
        }
    };

    const toggleRole = (name: string) => {
        if (!canWrite) return; // ✅ BLOKADA ZMIANY RÓL
        setSelectedRoles(prev => prev.includes(name) ? prev.filter(x => x !== name) : [...prev, name]);
    };

    const CustomCheckbox = ({ checked }: { checked: boolean }) => (
        <div style={{
            width: 14, height: 14, borderRadius: 3,
            border: `1px solid ${checked ? "rgba(86, 204, 242, 0.4)" : "var(--border)"}`,
            background: checked ? "rgba(86, 204, 242, 0.1)" : "var(--bg-main)",
            display: "flex", alignItems: "center", justifyContent: "center",
            transition: "all 0.2s", pointerEvents: "none"
        }}>
            {checked && <div style={{ width: 6, height: 6, borderRadius: 1, background: "rgba(86, 204, 242, 0.8)" }} />}
        </div>
    );

    return (
        <div style={{ padding: "16px", display: "flex", flexDirection: "column", gap: 16, width: "100%", minWidth: "550px" }}>
            {!canWrite && (
                <div style={{ padding: "10px", background: "rgba(245, 158, 11, 0.1)", border: "1px solid rgba(245, 158, 11, 0.2)", borderRadius: 8, color: "#f59e0b", fontSize: "13px" }}>
                    ℹ️ Tryb podglądu. Nie masz uprawnień do edycji danych użytkownika.
                </div>
            )}

            {error && (
                <div style={{ padding: "10px", background: "rgba(235, 87, 87, 0.1)", border: "1px solid rgba(235, 87, 87, 0.2)", borderRadius: 8, color: "var(--danger)", fontSize: "13px" }}>
                    ⚠️ {error}
                </div>
            )}

            <div style={{ display: "flex", gap: 16 }}>
                <div className="ui-panel" style={{ padding: "12px", flex: 1 }}>
                    <div style={{ fontSize: "10px", fontWeight: 700, color: "var(--text-muted)", marginBottom: 12, letterSpacing: "0.5px" }}>DANE LOGOWANIA</div>
                    <div className="field">
                        <label>Login (nieedytowalny)</label>
                        <input value={userData.login} disabled style={{ opacity: 0.6, width: "100%" }} />
                    </div>
                    <div className="field">
                        <label>Zmień hasło (opcjonalnie)</label>
                        <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder={canWrite ? "Zostaw puste, aby nie zmieniać" : "Brak uprawnień"} style={{ width: "100%" }} readOnly={!canWrite} />
                    </div>
                </div>

                <div className="ui-panel" style={{ padding: "12px", flex: 1 }}>
                    <div style={{ fontSize: "10px", fontWeight: 700, color: "var(--text-muted)", marginBottom: 12, letterSpacing: "0.5px" }}>DANE DODATKOWE</div>
                    <div className="field">
                        <label>AD UPN / E-mail</label>
                        <input value={adUpn} onChange={e => setAdUpn(e.target.value)} placeholder="user@domain.com" style={{ width: "100%" }} readOnly={!canWrite} />
                    </div>
                    <div className="field">
                        <label>External ID</label>
                        <input value={externalId} onChange={e => setExternalId(e.target.value)} placeholder="ID z systemu zewnętrznego" style={{ width: "100%" }} readOnly={!canWrite} />
                    </div>
                </div>
            </div>

            <div className="ui-panel" style={{ padding: "12px", width: "300px" }}>
                <div style={{ fontSize: "10px", fontWeight: 700, color: "var(--text-muted)", marginBottom: 12, letterSpacing: "0.5px" }}>PRZYPISANE ROLE</div>
                <div style={{ display: "flex", flexDirection: "column", gap: 1, maxHeight: "150px", overflowY: "auto", border: "1px solid var(--bg-soft)", borderRadius: 6, background: "var(--bg-main)" }}>
                    {roles.map((r) => {
                        const isChecked = selectedRoles.includes(r.name);
                        return (
                            <div 
                                key={r.id} 
                                onClick={() => toggleRole(r.name)}
                                style={{ 
                                    display: "flex", alignItems: "center", padding: "4px 12px", gap: 12, 
                                    cursor: canWrite ? "pointer" : "default", borderBottom: "1px solid var(--bg-soft)",
                                    background: isChecked ? "rgba(255,255,255,0.01)" : "transparent",
                                    opacity: canWrite ? 1 : 0.7
                                }}
                            >
                                <CustomCheckbox checked={isChecked} />
                                <span style={{ fontSize: 12, color: isChecked ? "var(--text-main)" : "var(--text-muted)" }}>{r.name}</span>
                            </div>
                        );
                    })}
                </div>
            </div>

            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, width: "100%", marginTop: 8 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    {/* ✅ PRZYCISK ZAPISU - UKRYTY DLA BRAKU UPRAWNIEŃ */}
                    {canWrite && (
                        <button 
                            className="primary" 
                            onClick={handleSave} 
                            disabled={busy || success} 
                            style={{ 
                                display: "flex", alignItems: "center", gap: 8, padding: "6px 20px", fontSize: "12px",
                                color: "rgba(86, 204, 242, 0.7)"
                            }}
                        >
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                            {busy ? "Zapisywanie..." : "Zapisz zmiany"}
                        </button>
                    )}
                    {success && <span style={{ color: "#45a049", fontSize: "12px", fontWeight: 600 }}>✔ Zapisano</span>}
                </div>

                {onCancel && (
                    <button 
                        onClick={onCancel}
                        style={{
                            background: "rgba(235, 87, 87, 0.1)",
                            border: "1px solid rgba(235, 87, 87, 0.3)",
                            color: "#eb5757",
                            padding: "6px 16px",
                            borderRadius: "4px",
                            cursor: "pointer",
                            fontWeight: 600,
                            fontSize: "12px",
                            transition: "all 0.2s"
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.background = "rgba(235, 87, 87, 0.2)";
                            e.currentTarget.style.borderColor = "#eb5757";
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.background = "rgba(235, 87, 87, 0.1)";
                            e.currentTarget.style.borderColor = "rgba(235, 87, 87, 0.3)";
                        }}
                    >
                        {canWrite ? "Anuluj" : "Zamknij"}
                    </button>
                )}
            </div>
        </div>
    );
}
