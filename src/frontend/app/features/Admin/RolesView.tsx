import React, { useEffect, useState } from "react";
import { apiFetch, getErrorMessage } from "../../apiFetch";
import { useToast } from "../../layout/Shell";

export function RolesView({ onClose }: { onClose?: () => void }) {
    const [roles, setRoles] = useState<any[]>([]);
    const [permissions, setPermissions] = useState<any[]>([]);
    const [selectedRole, setSelectedRole] = useState("");
    const [rolePermissions, setRolePermissions] = useState<string[]>([]);
    const [showAddRole, setShowAddRole] = useState(false);
    const [newRoleName, setNewRoleName] = useState("");
    const [permSearchTerm, setPermSearchTerm] = useState("");
    const { addToast } = useToast();

    const loadRoles = async () => {
        const r = await apiFetch("/admin/roles");
        if (r.ok) { const data = await r.json(); setRoles(data); if (data.length && !selectedRole) setSelectedRole(data[0].id); }
    };

    const loadPermissions = async () => {
        const p = await apiFetch("/admin/permissions");
        if (p.ok) setPermissions(await p.json());
    };

    useEffect(() => { loadRoles(); loadPermissions(); }, []);

    useEffect(() => {
        const role = roles.find(r => r.id === selectedRole);
        if (role) apiFetch(`/admin/roles/${role.name}/permissions`).then(r => r.ok ? r.json() : []).then(setRolePermissions);
    }, [selectedRole, roles]);

    const handleAddRole = async () => {
        if (!newRoleName.trim()) return;
        const res = await apiFetch("/admin/roles", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: newRoleName }) });
        if (res.ok) { addToast("Rola dodana", "success"); setShowAddRole(false); setNewRoleName(""); loadRoles(); }
    };

    const grouped: Record<string, any[]> = {};
    permissions.forEach(p => {
        const mod = p.moduleName || "SYSTEM";
        if (p.code.toLowerCase().includes(permSearchTerm.toLowerCase())) {
            if (!grouped[mod]) grouped[mod] = []; grouped[mod].push(p);
        }
    });

    return (
        <div className="dapnet-view">
            <div className="dapnet-view-header">
                <button className="primary" onClick={() => setShowAddRole(true)}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M12 5v14M5 12h14"/></svg>
                    DODAJ NOWĄ ROLĘ
                </button>
            </div>

            <div className="dapnet-view-body" style={{ display: "flex", flexDirection: "row", gap: 16 }}>
                <div className="ui-panel" style={{ width: 250, flexShrink: 0 }}>
                    <div className="ui-panel__header">LISTA RÓL</div>
                    <div style={{ flex: 1, overflowY: "auto" }}>
                        {roles.map(r => (
                            <div key={r.id} className={"ui-nav-item " + (selectedRole === r.id ? "ui-nav-item--active" : "")} onClick={() => setSelectedRole(r.id)} style={{ border: "none", borderRadius: 0, borderBottom: "1px solid rgba(255,255,255,0.02)", height: 36 }}>
                                {r.name}
                            </div>
                        ))}
                    </div>
                </div>

                <div className="ui-panel" style={{ flex: 1 }}>
                    <div className="ui-panel__header">UPRAWNIENIA</div>
                    <div style={{ padding: 12, borderBottom: "1px solid var(--bg-soft)" }}>
                        <input type="text" placeholder="Filtruj uprawnienia..." value={permSearchTerm} onChange={e => setPermSearchTerm(e.target.value)} style={{ width: "100%" }} />
                    </div>
                    <div style={{ flex: 1, overflowY: "auto", padding: 16 }}>
                        {Object.keys(grouped).map(mod => (
                            <div key={mod} style={{ marginBottom: 20 }}>
                                <div style={{ fontSize: "10px", fontWeight: 800, color: "var(--accent)", marginBottom: 10, letterSpacing: "1px" }}>{mod.toUpperCase()}</div>
                                {grouped[mod].map(p => (
                                    <label key={p.id} style={{ display: "flex", alignItems: "center", gap: 12, padding: "6px 0", cursor: "pointer" }}>
                                        <input type="checkbox" checked={rolePermissions.includes(p.code)} onChange={() => {}} />
                                        <span style={{ fontSize: "13px", color: "var(--text-main)" }}>{p.description || p.code}</span>
                                    </label>
                                ))}
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {onClose && <div className="dapnet-view-footer"><button className="btn-danger" onClick={onClose}>ZAMKNIJ</button></div>}

            {showAddRole && (
                <div className="modal-backdrop">
                    <div className="modal">
                        <h3>Nowa rola</h3>
                        <input value={newRoleName} onChange={e => setNewRoleName(e.target.value)} placeholder="Nazwa roli..." style={{ width: "100%", marginBottom: 20 }} />
                        <div style={{ display: "flex", gap: 12, justifyContent: "flex-end" }}>
                            <button onClick={() => setShowAddRole(false)}>Anuluj</button>
                            <button className="primary" onClick={handleAddRole}>Dodaj</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
