import React, { useEffect, useState } from "react";
import { apiFetch, getErrorMessage } from "../../apiFetch";
import { useToast } from "../../layout/Shell";
import { AddUserView } from "./AddUserView";
import { EditUserView } from "./EditUserView";
import { Tooltip } from "../../components/Tooltip";
import { useAuth } from "../../layout/Shell";
import { DndContext, closestCenter, useSensor, useSensors, PointerSensor } from '@dnd-kit/core';
import { arrayMove, SortableContext, horizontalListSortingStrategy, useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

interface UserDto { id: string; login: string; isActive: boolean; adUpn?: string; createdAtUtc: string; lastActivityAtUtc?: string | null; roles: string[]; isOnline: boolean; }

const DEFAULT_COLUMNS = [
    { id: 'actions', label: 'AKCJE', width: '120px', sortable: false },
    { id: 'login', label: 'UŻYTKOWNIK', width: '160px', sortable: true },
    { id: 'isActive', label: 'STATUS', width: '100px', sortable: true },
    { id: 'adUpn', label: 'AD UPN / E-MAIL', width: '180px', sortable: true },
    { id: 'roles', label: 'ROLE', width: '180px', sortable: true },
    { id: 'createdAtUtc', label: 'UTWORZONO', width: '150px', sortable: true },
    { id: 'lastActivityAtUtc', label: 'OSTATNIA AKTYWNOŚĆ', width: 'minmax(150px, auto)', sortable: true }
];

function SortableHeader({ column, sortConfig, onSort }: any) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: column.id });
    const style: React.CSSProperties = { transform: CSS.Transform.toString(transform), transition, cursor: isDragging ? "grabbing" : "grab", zIndex: isDragging ? 2 : 1 };
    return (
        <div ref={setNodeRef} style={style} {...attributes} {...listeners} className="dapnet-table-cell">
            <span onClick={() => column.sortable && onSort(column.id)} style={{ cursor: column.sortable ? "pointer" : "grab", display: "flex", alignItems: "center", gap: 4 }}>
                {column.label}
                {sortConfig.key === column.id && <span>{sortConfig.direction === 'asc' ? '▲' : '▼'}</span>}
            </span>
        </div>
    );
}

export function UsersView({ setGlobalBusy, onClose }: { setGlobalBusy: (v: boolean) => void, onClose?: () => void }) {
    const { hasPermission } = useAuth();
    const canWrite = hasPermission("platform.users.write");
    const [users, setUsers] = useState<UserDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState("");
    const [showAddModal, setShowAddModal] = useState(false);
    const [editingUser, setEditingUser] = useState<UserDto | null>(null);
    const [columns, setColumns] = useState(() => {
        const saved = localStorage.getItem("users_view_columns");
        return saved ? JSON.parse(saved) : DEFAULT_COLUMNS;
    });
    const [sortConfig, setSortConfig] = useState<{ key: string, direction: 'asc' | 'desc' }>({ key: 'login', direction: 'asc' });
    const { addToast } = useToast();
    const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }));

    const loadData = async () => {
        setLoading(true);
        try {
            const res = await apiFetch("/admin/users");
            if (res.ok) setUsers(await res.json());
        } catch (e) {} finally { setLoading(false); }
    };

    useEffect(() => { loadData(); }, []);

    const handleSort = (key: string) => setSortConfig(prev => ({ key, direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc' }));
    const handleDragEnd = (event: any) => { const { active, over } = event; if (active.id !== over.id) { setColumns((items: any) => { const oldIndex = items.findIndex((i: any) => i.id === active.id); const newIndex = items.findIndex((i: any) => i.id === over.id); return arrayMove(items, oldIndex, newIndex); }); } };

    const filteredUsers = users.filter(u => u.login.toLowerCase().includes(searchTerm.toLowerCase()));
    const gridTemplate = columns.map((c: any) => c.width).join(" ");

    return (
        <div className="dapnet-view">
            <div className="dapnet-view-header">
                {canWrite && <button className="primary" onClick={() => setShowAddModal(true)}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M12 5v14M5 12h14"/></svg>
                    Dodaj użytkownika
                </button>}
                <input type="text" placeholder="Szukaj..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} style={{ width: "300px" }} />
            </div>

            <div className="dapnet-view-body dapnet-view-body--fill">
                <div className="dapnet-table-container">
                    <div className="dapnet-table-header" style={{ gridTemplateColumns: gridTemplate }}>
                        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
                            <SortableContext items={columns.map((c: any) => c.id)} strategy={horizontalListSortingStrategy}>
                                {columns.map((col: any) => <SortableHeader key={col.id} column={col} sortConfig={sortConfig} onSort={handleSort} />)}
                            </SortableContext>
                        </DndContext>
                    </div>
                    <div style={{ overflowY: "auto", flex: 1 }}>
                        {loading ? <div style={{ padding: 20, textAlign: "center" }}>Ładowanie...</div> : filteredUsers.map((u) => (
                            <div key={u.id} className="dapnet-table-row" style={{ gridTemplateColumns: gridTemplate }}>
                                <div className="dapnet-table-cell" style={{ display: "flex", gap: 8 }}>
                                    <button onClick={() => setEditingUser(u)} style={{ padding: 4, background: "transparent", border: "none", color: "var(--accent)" }}>✏️</button>
                                    <button style={{ padding: 4, background: "transparent", border: "none", color: "var(--danger)" }}>🗑️</button>
                                </div>
                                <div className="dapnet-table-cell" style={{ fontWeight: 600, color: "var(--accent)" }}>{u.login}</div>
                                <div className="dapnet-table-cell">{u.isActive ? "AKTYWNY" : "NIEAKTYWNY"}</div>
                                <div className="dapnet-table-cell">{u.adUpn || "-"}</div>
                                <div className="dapnet-table-cell">{u.roles.join(", ")}</div>
                                <div className="dapnet-table-cell">{new Date(u.createdAtUtc).toLocaleDateString()}</div>
                                <div className="dapnet-table-cell">{u.lastActivityAtUtc ? new Date(u.lastActivityAtUtc).toLocaleString() : "-"}</div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {onClose && <div className="dapnet-view-footer"><button className="btn-danger" onClick={onClose}>Zamknij</button></div>}

            {showAddModal && (
                <div className="modal-backdrop">
                    <div className="modal" style={{ width: "fit-content", padding: 0 }}>
                        <AddUserView onSaved={() => { setShowAddModal(false); loadData(); }} onCancel={() => setShowAddModal(false)} />
                    </div>
                </div>
            )}
            {editingUser && (
                <div className="modal-backdrop">
                    <div className="modal" style={{ width: "fit-content", padding: 0 }}>
                        <EditUserView userData={editingUser} onSaved={() => { setEditingUser(null); loadData(); }} onCancel={() => setEditingUser(null)} />
                    </div>
                </div>
            )}
        </div>
    );
}
