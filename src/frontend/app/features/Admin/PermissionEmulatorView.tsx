import React, { useState, useEffect } from "react";
import { apiFetch, safeJson } from "../../apiFetch";
import { useAuth } from "../../layout/Shell";

export function PermissionEmulatorView() {
    const { permissions: currentPermissions, user } = useAuth();
    const [allPermissions, setAllPermissions] = useState<string[]>([]);
    const [emulatedPermissions, setEmulatedPermissions] = useState<string[]>(currentPermissions);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        async function loadAll() {
            try {
                const res = await apiFetch("/admin/permissions"); // Zakładam, że masz taki endpoint
                if (res.ok) {
                    const data = await safeJson(res);
                    // Jeśli data to lista obiektów, wyciągamy kody
                    const codes = Array.isArray(data) ? data.map((p: any) => p.code || p) : [];
                    setAllPermissions(codes.sort());
                } else {
                    // Fallback: jeśli nie ma endpointu, używamy obecnych + kilka standardowych
                    const defaults = ["platform.admin", "platform.users.read", "platform.users.write", "ksef.view", "ksef.manage"];
                    const combined = Array.from(new Set([...currentPermissions, ...defaults]));
                    setAllPermissions(combined.sort());
                }
            } catch {
                setAllPermissions(currentPermissions.sort());
            } finally {
                setLoading(false);
            }
        }
        loadAll();
    }, []);

    const togglePermission = (code: string) => {
        const newPerms = emulatedPermissions.includes(code)
            ? emulatedPermissions.filter(p => p !== code)
            : [...emulatedPermissions, code];
        setEmulatedPermissions(newPerms);
    };

    const applyChanges = () => {
        // ✅ NADPISUJEMY UPRAWNIENIA W SESSION STORAGE
        const userInfo = JSON.parse(sessionStorage.getItem("user_info") || "{}");
        // Uwaga: musimy upewnić się, że Shell.tsx to obsłuży. 
        // Najprościej będzie dodać pole 'emulated_permissions' do userInfo.
        userInfo.emulated_permissions = emulatedPermissions;
        sessionStorage.setItem("user_info", JSON.stringify(userInfo));
        
        // Powiadamiamy system o zmianie
        window.dispatchEvent(new CustomEvent("auth-changed"));
        window.dispatchEvent(new CustomEvent("toast-notify", { detail: { message: "Uprawnienia zostały zemulowane!", type: "success" } }));
    };

    const resetToOriginal = () => {
        const userInfo = JSON.parse(sessionStorage.getItem("user_info") || "{}");
        delete userInfo.emulated_permissions;
        sessionStorage.setItem("user_info", JSON.stringify(userInfo));
        
        window.dispatchEvent(new CustomEvent("auth-changed"));
        window.location.reload(); // Najbezpieczniejszy reset
    };

    if (loading) return <div style={{ padding: "20px", color: "var(--text-muted)" }}>Ładowanie listy uprawnień...</div>;

    return (
        <div style={{ display: "flex", flexDirection: "column", height: "100%", background: "var(--bg-main)", color: "var(--text-main)" }}>
            <div style={{ padding: "16px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                    <h3 style={{ margin: 0, fontSize: "14px", color: "var(--accent)" }}>EMULATOR UPRAWNIEŃ</h3>
                    <p style={{ margin: "4px 0 0 0", fontSize: "10px", color: "var(--text-muted)" }}>
                        Zmieniasz uprawnienia tylko dla obecnej sesji przeglądarki.
                    </p>
                </div>
                <div style={{ display: "flex", gap: "8px" }}>
                    <button onClick={resetToOriginal} className="secondary" style={{ padding: "6px 12px", fontSize: "11px" }}>RESET</button>
                    <button onClick={applyChanges} className="primary" style={{ padding: "6px 12px", fontSize: "11px" }}>ZASTOSUJ</button>
                </div>
            </div>

            <div style={{ flex: 1, overflowY: "auto", padding: "16px" }}>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px" }}>
                    {allPermissions.map(code => (
                        <div 
                            key={code} 
                            onClick={() => togglePermission(code)}
                            style={{ 
                                padding: "10px", 
                                background: emulatedPermissions.includes(code) ? "rgba(86, 204, 242, 0.1)" : "rgba(0,0,0,0.2)",
                                border: `1px solid ${emulatedPermissions.includes(code) ? "var(--accent)" : "var(--border)"}`,
                                borderRadius: "6px",
                                cursor: "pointer",
                                fontSize: "11px",
                                display: "flex",
                                alignItems: "center",
                                gap: "10px",
                                transition: "all 0.2s"
                            }}
                        >
                            <div style={{ 
                                width: "12px", height: "12px", borderRadius: "2px", 
                                border: "1px solid var(--text-muted)",
                                background: emulatedPermissions.includes(code) ? "var(--accent)" : "transparent"
                            }} />
                            <span style={{ color: emulatedPermissions.includes(code) ? "var(--text-main)" : "var(--text-muted)" }}>{code}</span>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
