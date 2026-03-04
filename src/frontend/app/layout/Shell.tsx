import React, { useState, useEffect, createContext, useContext, useCallback } from "react";
import { apiFetch, isReadOnlyMode } from "../apiFetch";
import { DesktopProvider } from "../DesktopContext";
import { TopBar } from "./TopBar";

interface Toast { id: number; message: string; type: "success" | "error"; }
const ToastContext = createContext<{ addToast: (msg: string, type: "success" | "error") => void }>({ addToast: () => {} });
export const useToast = () => useContext(ToastContext);

interface AuthContextType {
    user: any;
    permissions: string[];
    hasPermission: (perm: string) => boolean;
    loading: boolean;
    version: string;
}
const AuthContext = createContext<AuthContextType>({ user: null, permissions: [], hasPermission: () => false, loading: true, version: "0.0.0" });
export const useAuth = () => useContext(AuthContext);

export function Shell({ children, topRight, initialStatus }: { children: React.ReactNode, topRight?: React.ReactNode, initialStatus?: any }) {
    const [toasts, setToasts] = useState<Toast[]>([]);
    const [authState, setAuthState] = useState<any>({ user: null, permissions: [], loading: !initialStatus, version: initialStatus?.version || "0.0.0" });

    const addToast = useCallback((message: string, type: "success" | "error") => {
        const id = Date.now();
        setToasts(prev => [...prev, { id, message, type }]);
        setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 3000);
    }, []);

    const fetchAuthData = useCallback((statusFromProps?: any) => {
        const userInfoStr = sessionStorage.getItem("user_info");
        const userInfo = userInfoStr ? JSON.parse(userInfoStr) : null;
        
        const processStatus = (status: any) => {
            if (userInfo) {
                apiFetch("/auth/me").then(async r => {
                    if (r.ok) {
                        const data = await r.json();
                        const fullUser = { ...userInfo, ...data };
                        sessionStorage.setItem("user_info", JSON.stringify(fullUser));
                        setAuthState({ user: fullUser, permissions: data.permissions || [], loading: false, version: status.version || "0.0.0" });
                        
                        // ✅ POPRAWKA: Używamy funkcji wystawionych w preload.ts
                        if (fullUser.isSystemAdmin || fullUser.login?.toLowerCase() === "admindapnet") {
                            if ((window as any).electron?.enableDevTools) (window as any).electron.enableDevTools();
                        } else {
                            if ((window as any).electron?.disableDevTools) (window as any).electron.disableDevTools();
                        }
                    } else {
                        setAuthState(prev => ({ ...prev, loading: false, version: status.version || "0.0.0" }));
                        if ((window as any).electron?.disableDevTools) (window as any).electron.disableDevTools();
                    }
                }).catch(() => {
                    setAuthState(prev => ({ ...prev, loading: false, version: status.version || "0.0.0" }));
                    if ((window as any).electron?.disableDevTools) (window as any).electron.disableDevTools();
                });
            } else {
                setAuthState({ user: null, permissions: [], loading: false, version: status.version || "0.0.0" });
                if ((window as any).electron?.disableDevTools) (window as any).electron.disableDevTools();
            }
        };

        if (statusFromProps) processStatus(statusFromProps);
        else apiFetch("/system/status").then(r => r.json()).then(processStatus).catch(() => setAuthState(prev => ({ ...prev, loading: false })));
    }, []);

    useEffect(() => {
        const handleAuthChange = () => {
            console.log("Auth change detected, re-fetching...");
            fetchAuthData();
        };
        
        window.addEventListener("auth-changed", handleAuthChange);
        window.addEventListener("toast-notify", (e: any) => addToast(e.detail.message, e.detail.type));
        
        fetchAuthData(initialStatus);

        return () => {
            window.removeEventListener("auth-changed", handleAuthChange);
            window.removeEventListener("toast-notify", (e: any) => addToast(e.detail.message, e.detail.type));
        };
    }, [fetchAuthData, initialStatus, addToast]);

    const hasPermission = (perm: string) => {
        if (!authState.user) return false;
        if ((authState.user.isSystemAdmin || authState.user.login?.toLowerCase() === "admindapnet")) return true;
        return authState.permissions.includes(perm);
    };

    const readOnlyBanner = isReadOnlyMode() ? (
        <div style={{ position: "absolute", left: "50%", transform: "translateX(-50%)", display: "flex", alignItems: "center", gap: 10, background: "rgba(235, 87, 87, 0.1)", border: "1px solid rgba(235, 87, 87, 0.3)", padding: "4px 16px", borderRadius: "20px", color: "#EB5757", fontSize: "10px", fontWeight: 700, letterSpacing: "1px" }}>
            TRYB TYLKO DO ODCZYTU
        </div>
    ) : null;

    return (
        <ToastContext.Provider value={{ addToast }}>
            <AuthContext.Provider value={{ user: authState.user, permissions: authState.permissions, hasPermission, loading: authState.loading, version: authState.version }}>
                <DesktopProvider>
                    <div className="app">
                        <TopBar user={authState.user} topRight={topRight} readOnlyBanner={readOnlyBanner} />
                        
                        <div className="app-main-area">
                            {authState.loading ? (
                                <div style={{ flex: 1, display: "flex", alignItems: "center", justifyContent: "center", color: "var(--text-muted)" }}>AUTORYZACJA...</div>
                            ) : children}
                        </div>

                        <div style={{ position: "fixed", bottom: 60, right: 20, display: "flex", flexDirection: "column", gap: 10, zIndex: 999999 }}>
                            {toasts.map(t => (
                                <div key={t.id} style={{ padding: "12px 20px", borderRadius: "8px", background: "var(--bg-panel)", border: `1px solid ${t.type === "success" ? "#45a049" : "#EB5757"}`, color: "var(--text-main)", fontSize: "13px", boxShadow: "0 10px 30px rgba(0,0,0,0.5)", animation: "slideIn 0.3s ease-out" }}>
                                    {t.message}
                                </div>
                            ))}
                        </div>
                    </div>
                </DesktopProvider>
            </AuthContext.Provider>
        </ToastContext.Provider>
    );
}
