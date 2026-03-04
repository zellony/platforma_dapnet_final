import React, { useState, useEffect, useRef } from "react";
import { apiFetch, setStoredLogin, getStoredLogin, encryptPassword } from "../../apiFetch";
import iconImg from "../../assets/icon.png";

type LoginScreenProps = {
    busy: boolean;
    setBusy: (v: boolean) => void;
    onLoggedIn: () => void;
};

export function LoginScreen({ busy, setBusy, onLoggedIn }: LoginScreenProps) {
    const [login, setLogin] = useState("");
    const [rememberMe, setRememberMe] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [showForceLogin, setShowForceLogin] = useState(false);

    const loginRef = useRef<HTMLInputElement>(null);
    const passwordRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        async function loadLogin() {
            localStorage.removeItem("platform_remembered_login");
            const stored = await getStoredLogin();
            if (stored) {
                setLogin(stored);
                setRememberMe(true);
                passwordRef.current?.focus();
            } else {
                loginRef.current?.focus();
            }
        }
        loadLogin();
    }, []);

    async function doLogin(force: boolean = false) {
        const password = passwordRef.current?.value || "";
        if (!login || !password) return;

        setBusy(true);
        setError(null);
        setShowForceLogin(false);

        try {
            const encryptedPassword = await encryptPassword(password);
            if (passwordRef.current) passwordRef.current.value = "";

            let machineName = "Unknown";
            let ipAddress = "127.0.0.1";

            try {
                if ((window as any).electron) {
                    machineName = await (window as any).electron.getMachineName();
                    ipAddress = await (window as any).electron.getLocalIp();
                }
            } catch (e) { console.warn(e); }

            const res = await apiFetch("/auth/login", {
                method: "POST",
                body: JSON.stringify({ login, password: encryptedPassword, machineName, ipAddress, force })
            });

            if (res.ok) {
                const data = await res.json();
                
                if (rememberMe) {
                    await setStoredLogin(login);
                } else {
                    await setStoredLogin("");
                }

                sessionStorage.setItem("user_info", JSON.stringify({
                    login: data.login,
                    userId: data.userId,
                    is_read_only: data.is_read_only,
                    expiresAt: data.expiresAt ?? null
                }));

                window.dispatchEvent(new CustomEvent("auth-changed"));
                onLoggedIn();
            } else if (res.status === 409) {
                setShowForceLogin(true);
            } else {
                const data = await res.json();
                setError(data.message || "Niepoprawny login lub hasło");
            }
        } catch (e) {
            setError(e instanceof Error ? e.message : "Błąd połączenia z serwerem");
        } finally {
            setBusy(false);
        }
    }

    if (showForceLogin) {
        return (
            <div className="setup-container">
                <div className="setup-box" style={{ textAlign: "center" }}>
                    <div style={{ fontSize: "40px", marginBottom: "16px" }}>??</div>
                    <h3 className="setup-title" style={{ marginBottom: "12px" }}>Użytkownik już zalogowany</h3>
                    <p style={{ color: "var(--text-muted)", fontSize: "14px", marginBottom: "24px", lineHeight: "1.5" }}>
                        Twoje konto jest obecnie używane na innym urządzeniu.<br/>
                        Czy chcesz wylogować tamtą sesję i zalogować się tutaj?
                    </p>
                    <div style={{ display: "flex", gap: "12px" }}>
                        <button className="primary" onClick={() => doLogin(true)} disabled={busy}>
                            {busy ? "Logowanie..." : "Tak, wyloguj"}
                        </button>
                        <button className="secondary" onClick={() => setShowForceLogin(false)} disabled={busy}>
                            Anuluj
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="setup-container">
            <div className="setup-box">
                <div className="setup-header">
                    <img src={iconImg} alt="DAPNET" style={{ width: "48px", height: "48px", marginBottom: "12px", opacity: 0.6 }} />
                    <h2 className="setup-title">LOGOWANIE</h2>
                    <div className="setup-subtitle">PLATFORMA DAPNET</div>
                </div>

                {error && (
                    <div style={{ background: "rgba(235, 87, 87, 0.1)", border: "1px solid rgba(235, 87, 87, 0.2)", color: "var(--danger)", padding: "12px", borderRadius: "8px", fontSize: "12px", marginBottom: "20px", textAlign: "center", fontWeight: 600 }}>
                        {error}
                    </div>
                )}

                <div className="setup-field">
                    <label className="setup-label">LOGIN UŻYTKOWNIKA</label>
                    <input 
                        ref={loginRef}
                        className="setup-input"
                        value={login} 
                        onChange={e => setLogin(e.target.value)} 
                        disabled={busy} 
                        placeholder="Wprowadź login..."
                    />
                </div>

                <div className="setup-field">
                    <label className="setup-label">HASŁO DOSTĘPU</label>
                    <input 
                        ref={passwordRef}
                        className="setup-input"
                        type="password" 
                        disabled={busy} 
                        placeholder="********"
                        onKeyDown={e => e.key === "Enter" && doLogin()} 
                    />
                </div>

                <div style={{ marginBottom: "30px" }}>
                    {/* ? NOWY GLOBALNY CHECKBOX */}
                    <label className={`dapnet-checkbox ${rememberMe ? 'dapnet-checkbox--active' : ''}`}>
                        <div className="dapnet-checkbox-box">
                            <div className="dapnet-checkbox-mark"></div>
                        </div>
                        <span>Zapamiętaj mnie na tym urządzeniu</span>
                        <input 
                            type="checkbox" 
                            checked={rememberMe} 
                            onChange={e => setRememberMe(e.target.checked)} 
                            style={{ display: "none" }} 
                        />
                    </label>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                    <button 
                        className="primary" 
                        onClick={() => doLogin()} 
                        disabled={busy || !login}
                    >
                        {busy ? "AUTORYZACJA..." : "ZALOGUJ DO SYSTEMU"}
                    </button>
                    <button 
                        className="secondary danger" 
                        onClick={() => (window as any).electron.quitApp()}
                        style={{ border: "none", background: "transparent", fontSize: "10px", width: "auto", alignSelf: "center", opacity: 0.6 }}
                    >
                        ZAMKNIJ APLIKACJĘ
                    </button>
                </div>
            </div>
        </div>
    );
}

