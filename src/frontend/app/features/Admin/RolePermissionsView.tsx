import React, { useEffect, useState } from "react";
import { apiFetch } from "../../apiFetch";

interface PermissionDto {
    id: string;
    code: string;
    description?: string | null;
    moduleName?: string | null;
}

export function RolePermissionsView({ roleName }: { roleName: string }) {
    const [permissions, setPermissions] = useState<PermissionDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState("");
    const [expandedModules, setExpandedModules] = useState<string[]>([]);

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            try {
                const [allRes, roleRes] = await Promise.all([
                    apiFetch("/admin/permissions"),
                    apiFetch(`/admin/roles/${roleName}/permissions`)
                ]);
                
                const allPerms: PermissionDto[] = allRes.ok ? await allRes.json() : [];
                const rolePermCodes: string[] = roleRes.ok ? await roleRes.json() : [];

                const finalPerms = allPerms.filter(p => rolePermCodes.includes(p.code));
                setPermissions(finalPerms);
                
                const modules = Array.from(new Set(finalPerms.map(x => x.moduleName || "SYSTEM"))) as string[];
                setExpandedModules(modules);
            } catch (e) { console.error(e); }
            finally { setLoading(false); }
        };
        load();
    }, [roleName]);

    const toggleModule = (mod: string) => {
        setExpandedModules(prev => prev.includes(mod) ? prev.filter(m => m !== mod) : [...prev, mod]);
    };

    const search = searchTerm.toLowerCase();
    const grouped: Record<string, PermissionDto[]> = {};
    permissions.forEach(p => {
        const mod = p.moduleName || "SYSTEM";
        if (p.code.toLowerCase().includes(search) || (p.description && p.description.toLowerCase().includes(search)) || mod.toLowerCase().includes(search)) {
            if (!grouped[mod]) grouped[mod] = [];
            grouped[mod].push(p);
        }
    });

    return (
        <div style={{ padding: "12px", display: "flex", flexDirection: "column", height: "100%" }}>
            <div style={{ marginBottom: 12, fontSize: "13px", color: "var(--text-muted)" }}>
                Uprawnienia przypisane do roli: <b style={{ color: "rgba(86, 204, 242, 0.8)" }}>{roleName}</b>
            </div>

            <div className="ui-panel" style={{ flex: 1, display: "flex", flexDirection: "column" }}>
                <div style={{ padding: "6px 12px", borderBottom: "1px solid var(--bg-soft)" }}>
                    <input 
                        type="text" 
                        placeholder="Szukaj uprawnienia..." 
                        value={searchTerm} 
                        onChange={(e) => setSearchTerm(e.target.value)} 
                        style={{ width: "100%", background: "var(--bg-main)", border: "1px solid var(--border)", borderRadius: "6px", padding: "6px 10px", color: "var(--text-main)", fontSize: "12px", outline: "none" }} 
                    />
                </div>

                <div style={{ flex: 1, overflowY: "auto", padding: "8px" }}>
                    {loading ? (
                        <div style={{ padding: 20, textAlign: "center", opacity: 0.5 }}>Ładowanie...</div>
                    ) : Object.keys(grouped).length === 0 ? (
                        <div style={{ padding: 20, textAlign: "center", opacity: 0.5 }}>Brak uprawnień dla tej roli.</div>
                    ) : Object.keys(grouped).map(modName => (
                        <div key={modName} style={{ marginBottom: 4, border: "1px solid var(--bg-soft)", borderRadius: 6, overflow: "hidden" }}>
                            <div onClick={() => toggleModule(modName)} style={{ background: "rgba(255,255,255,0.02)", padding: "4px 10px", display: "flex", alignItems: "center", cursor: "pointer" }}>
                                <span style={{ fontSize: "9px", fontWeight: 700, color: "var(--text-muted)", flex: 1 }}>{modName}</span>
                                <span style={{ fontSize: "10px", opacity: 0.5 }}>{expandedModules.includes(modName) ? "▼" : "▶"}</span>
                            </div>
                            {expandedModules.includes(modName) && (
                                <div style={{ background: "var(--bg-main)" }}>
                                    {grouped[modName].map((p) => (
                                        <div key={p.id} style={{ padding: "4px 10px", gap: 10, borderBottom: "1px solid var(--bg-soft)", display: "flex", alignItems: "center" }}>
                                            <div style={{ width: 14, height: 14, borderRadius: 3, border: "1px solid rgba(86, 204, 242, 0.4)", background: "rgba(86, 204, 242, 0.1)", display: "flex", alignItems: "center", justifyContent: "center" }}>
                                                <div style={{ width: 6, height: 6, borderRadius: 1, background: "rgba(86, 204, 242, 0.8)" }} />
                                            </div>
                                            <span style={{ fontSize: 12, color: "var(--text-main)" }}>{p.description || p.code}</span>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
