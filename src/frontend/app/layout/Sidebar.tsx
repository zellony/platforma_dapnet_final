import React, { useState, useRef, useMemo, useEffect } from "react";
import { MENU as INITIAL_MENU, SYSTEM_INFO_ITEM } from "../menu";
import { clearAuthToken, apiFetch } from "../apiFetch";
import { useAuth } from "./Shell";
import { DapnetIcon } from "../components/DapnetIcon";
import "./Sidebar.css";

import {
    DndContext,
    closestCenter,
    KeyboardSensor,
    PointerSensor,
    useSensor,
    useSensors,
    DragEndEvent,
    DragOverlay
} from '@dnd-kit/core';
import {
    arrayMove,
    SortableContext,
    sortableKeyboardCoordinates,
    verticalListSortingStrategy,
    useSortable
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

const FAVS_STORAGE_KEY = "dapnet_sidebar_favorites";

interface SidebarProps {
    onSelect: (key: string) => void;
    activeKey?: string | null;
    isCollapsed: boolean;
    onToggle: () => void;
    onQuitRequest?: () => void;
}

function SortableMenuItem({ item, activeKey, onSelect, isFav, onToggleFavorite, isCollapsed, isSub, onContextMenu }: any) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.key });
    const style = { transform: CSS.Transform.toString(transform), transition, opacity: isDragging ? 0.3 : 1, zIndex: isDragging ? 1000 : 1 };

    return (
        <div 
            ref={setNodeRef} 
            style={style} 
            className={`ui-nav-item ${activeKey === item.key ? "ui-nav-item--active" : ""}`} 
            onClick={() => onSelect(item.key)}
            onContextMenu={(e) => onContextMenu(e, item)}
        >
            <div style={{ display: "flex", alignItems: "center", gap: 12, flex: 1 }}>
                <DapnetIcon 
                    nameOrSvg={item.icon} 
                    color={activeKey === item.key ? "var(--accent)" : "var(--text-muted)"} 
                />
                {!isCollapsed && <span style={{ whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{item.label}</span>}
            </div>
            {!isCollapsed && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span onClick={(e) => { e.stopPropagation(); onToggleFavorite(item); }} style={{ fontSize: "14px", cursor: "pointer", color: isFav ? "var(--accent)" : "var(--border)" }}>{isFav ? "★" : "☆"}</span>
                    <div {...attributes} {...listeners} onClick={(e) => e.stopPropagation()} style={{ cursor: 'grab', opacity: 0.2, display: 'flex', alignItems: 'center' }}><DapnetIcon nameOrSvg="grip" size={12} color="var(--text-muted)" /></div>
                </div>
            )}
        </div>
    );
}

function SortableMenuGroup({ group, expanded, toggle, isCollapsed, activeKey, onSelect, favorites, onToggleFavorite, sensors, handleDragEnd, onMenuItemContextMenu, onHoverGroup }: any) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: group.id });
    const style = { transform: CSS.Transform.toString(transform), transition, opacity: isDragging ? 0.3 : 1 };

    return (
        <div id={`sidebar-group-${group.id}`} ref={setNodeRef} style={style} onMouseEnter={() => isCollapsed && onHoverGroup(group, true)} onMouseLeave={() => isCollapsed && onHoverGroup(group, false)}>
            <div className="ui-nav-item ui-nav-item--main" onClick={() => toggle(group.id)} style={{ justifyContent: isCollapsed ? "center" : "flex-start" }}>
                <div style={{ display: "flex", alignItems: "center", gap: 12, flex: isCollapsed ? 0 : 1 }}>
                    <DapnetIcon 
                        nameOrSvg={group.id === "favs" ? "star" : group.icon} 
                        size={16} 
                        color={group.id === "favs" ? "var(--accent)" : "var(--text-muted)"} 
                    />
                    {!isCollapsed && group.label}
                </div>
                {!isCollapsed && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <span style={{ fontSize: "10px", color: "var(--text-muted)", opacity: 0.5 }}>{expanded.includes(group.id) ? "▼" : "▶"}</span>
                        <div {...attributes} {...listeners} onClick={(e) => e.stopPropagation()} style={{ cursor: 'grab', opacity: 0.2, display: 'flex', alignItems: 'center' }}><DapnetIcon nameOrSvg="grip" size={12} color="var(--text-muted)" /></div>
                    </div>
                )}
            </div>
            {expanded.includes(group.id) && !isCollapsed && (
                <div className="ui-nav-sub">
                    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={(e) => handleDragEnd(e, group.id)}>
                        <SortableContext items={group.items.map((i: any) => i.key)} strategy={verticalListSortingStrategy}>
                            {group.items.map((item: any) => (
                                <SortableMenuItem key={item.key} item={item} activeKey={activeKey} onSelect={onSelect} isFav={favorites.find((f: any) => f.key === item.key)} onToggleFavorite={onToggleFavorite} isCollapsed={isCollapsed} isSub={true} onContextMenu={onMenuItemContextMenu} />
                            ))}
                        </SortableContext>
                    </DndContext>
                </div>
            )}
        </div>
    );
}

export function Sidebar({ onSelect, activeKey, isCollapsed, onToggle, onQuitRequest }: SidebarProps) {
    const { hasPermission, loading: authLoading } = useAuth();
    const [expanded, setExpanded] = useState<string[]>([]);
    
    const [favorites, setFavorites] = useState<any[]>(() => {
        const stored = localStorage.getItem(FAVS_STORAGE_KEY);
        if (stored) {
            try { return JSON.parse(stored); } catch (e) { return []; }
        }
        return [];
    });

    const [menuGroups, setMenuGroups] = useState<any[]>([]);
    const [contextMenu, setContextMenu] = useState<any>(null);
    const [hoveredGroup, setHoveredGroup] = useState<any>(null);
    const hideTimeoutRef = useRef<any>(null);

    useEffect(() => {
        localStorage.setItem(FAVS_STORAGE_KEY, JSON.stringify(favorites));
    }, [favorites]);

    const filteredMenu = useMemo(() => {
        return INITIAL_MENU.map(group => ({
            ...group,
            items: group.items.filter(item => !item.requiredPermission || hasPermission(item.requiredPermission))
        })).filter(group => group.items.length > 0);
    }, [hasPermission]);

    const filteredFavorites = useMemo(() => {
        return favorites.filter(fav => {
            for (const group of INITIAL_MENU) {
                const item = group.items.find(i => i.key === fav.key);
                if (item) return !item.requiredPermission || hasPermission(item.requiredPermission);
            }
            return true;
        });
    }, [favorites, hasPermission]);

    useEffect(() => { if (!authLoading) setMenuGroups(filteredMenu); }, [filteredMenu, authLoading]);

    const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));
    const toggle = (id: string) => { if (isCollapsed) return; setExpanded(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]); };
    
    const toggleFavorite = (item: any) => { 
        setFavorites(prev => { 
            const exists = prev.find(f => f.key === item.key); 
            if (exists) return prev.filter(f => f.key !== item.key); 
            return [...prev, { key: item.key, label: item.label, icon: item.icon }]; 
        }); 
    };
    
    const handleDragEnd = (event: DragEndEvent, groupId: string) => {
        const { active, over } = event; if (!over || active.id === over.id) return;
        let newGroups = [...menuGroups];
        if (groupId === "ROOT") newGroups = arrayMove(menuGroups, menuGroups.findIndex(x => x.id === active.id), menuGroups.findIndex(x => x.id === over.id));
        else if (groupId === "favs") { 
            setFavorites((items) => arrayMove(items, items.findIndex(x => x.key === active.id), items.findIndex(x => x.key === over.id))); 
            return; 
        }
        else newGroups = menuGroups.map(g => g.id === groupId ? { ...g, items: arrayMove(g.items, g.items.findIndex(x => x.key === active.id), g.items.findIndex(x => x.key === over.id)) } : g);
        setMenuGroups(newGroups);
    };

    const handleMenuItemContextMenu = (e: React.MouseEvent, item: any) => {
        e.preventDefault();
        setContextMenu({ x: e.clientX, y: e.clientY, item });
    };

    const handleHoverGroup = (group: any, isEntering: boolean) => {
        if (hideTimeoutRef.current) clearTimeout(hideTimeoutRef.current);
        if (isEntering) {
            const el = document.getElementById(`sidebar-group-${group.id}`);
            if (el) {
                const rect = el.getBoundingClientRect();
                setHoveredGroup({ group, y: rect.top });
            }
        } else {
            hideTimeoutRef.current = setTimeout(() => { setHoveredGroup(null); }, 150);
        }
    };

    const handleFlyoutEnter = () => { if (hideTimeoutRef.current) clearTimeout(hideTimeoutRef.current); };
    const handleFlyoutLeave = () => { hideTimeoutRef.current = setTimeout(() => { setHoveredGroup(null); }, 150); };

    useEffect(() => {
        const handleClick = () => setContextMenu(null);
        window.addEventListener("click", handleClick);
        return () => window.removeEventListener("click", handleClick);
    }, []);

    return (
        <div className="sidebar-container">
            <div className="sidebar-header" style={{ justifyContent: isCollapsed ? "center" : "flex-start", padding: isCollapsed ? "0" : "0 16px" }}>
                {!isCollapsed && (
                    <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                        <DapnetIcon nameOrSvg="menu" size={14} color="var(--text-muted)" />
                        <div className="sidebar-header-title">NAWIGACJA</div>
                    </div>
                )}
                <button onClick={onToggle} style={{ background: "transparent", border: "none", cursor: "pointer", marginLeft: isCollapsed ? "0" : "auto", display: "flex", alignItems: "center", justifyContent: "center" }}>
                    <DapnetIcon nameOrSvg={isCollapsed ? "chevron-right" : "chevron-left"} size={16} color="var(--text-muted)" />
                </button>
            </div>
            
            <div className="sidebar-menu-area">
                {filteredFavorites.length > 0 && (
                    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={(e) => handleDragEnd(e, "favs")}>
                        <SortableContext items={filteredFavorites.map(f => f.key)} strategy={verticalListSortingStrategy}>
                            <SortableMenuGroup group={{ id: "favs", label: "ULUBIONE", icon: "star", items: filteredFavorites }} expanded={expanded} toggle={toggle} isCollapsed={isCollapsed} activeKey={activeKey} onSelect={onSelect} favorites={filteredFavorites} onToggleFavorite={toggleFavorite} sensors={sensors} handleDragEnd={handleDragEnd} onMenuItemContextMenu={handleMenuItemContextMenu} onHoverGroup={handleHoverGroup} />
                        </SortableContext>
                    </DndContext>
                )}
                <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={(e) => handleDragEnd(e, "ROOT")}>
                    <SortableContext items={menuGroups.map(g => g.id)} strategy={verticalListSortingStrategy}>
                        {menuGroups.map(group => (
                            <SortableMenuGroup key={group.id} group={group} expanded={expanded} toggle={toggle} isCollapsed={isCollapsed} activeKey={activeKey} onSelect={onSelect} favorites={filteredFavorites} onToggleFavorite={toggleFavorite} sensors={sensors} handleDragEnd={handleDragEnd} onMenuItemContextMenu={handleMenuItemContextMenu} onHoverGroup={handleHoverGroup} />
                        ))}
                    </SortableContext>
                </DndContext>
            </div>

            <div className="sidebar-footer">
                <div 
                    className={`ui-nav-item ${activeKey === SYSTEM_INFO_ITEM.key ? "ui-nav-item--active" : ""}`} 
                    onClick={() => onSelect(SYSTEM_INFO_ITEM.key)} 
                    onContextMenu={(e) => handleMenuItemContextMenu(e, SYSTEM_INFO_ITEM)} 
                    style={{ justifyContent: isCollapsed ? "center" : "flex-start" }}
                >
                    <DapnetIcon 
                        nameOrSvg={SYSTEM_INFO_ITEM.icon} 
                        size={16} 
                        color={activeKey === SYSTEM_INFO_ITEM.key ? "var(--accent)" : "var(--text-muted)"} 
                    />
                    {!isCollapsed && <span style={{ fontWeight: 600 }}>{SYSTEM_INFO_ITEM.label}</span>}
                </div>
                
                {/* ✅ DOSTOSOWANIE WYMIARÓW PRZYCISKU W SIDEBARZE */}
                <button 
                    className="secondary danger" 
                    onClick={() => onQuitRequest?.()} 
                    style={{ 
                        width: "100%", 
                        height: "34px", 
                        padding: isCollapsed ? "0" : "0 12px",
                        justifyContent: isCollapsed ? "center" : "center",
                        fontSize: "11px"
                    }}
                >
                    <DapnetIcon nameOrSvg="power" size={14} color="var(--danger)" />
                    {!isCollapsed && "Zamknij aplikację"}
                </button>
            </div>

            {isCollapsed && hoveredGroup && (
                <div className="sidebar-flyout" style={{ top: hoveredGroup.y, left: "54px" }} onMouseEnter={handleFlyoutEnter} onMouseLeave={handleFlyoutLeave}>
                    <div className="sidebar-flyout-header">{hoveredGroup.group.label}</div>
                    {hoveredGroup.group.items.map((item: any) => (
                        <div key={item.key} className={`ui-nav-item ${activeKey === item.key ? "ui-nav-item--active" : ""}`} onClick={() => { onSelect(item.key); setHoveredGroup(null); }} onContextMenu={(e) => handleMenuItemContextMenu(e, item)}>
                            <DapnetIcon nameOrSvg={item.icon} size={14} color={activeKey === item.key ? "var(--accent)" : "var(--text-muted)"} />
                            <span>{item.label}</span>
                        </div>
                    ))}
                </div>
            )}

            {contextMenu && (
                <div className="sidebar-context-menu" style={{ top: contextMenu.y, left: contextMenu.x }}>
                    <div className="sidebar-context-menu-header">OPCJE: {contextMenu.item.label}</div>
                    <div className="sidebar-context-menu-item" onClick={() => { window.dispatchEvent(new CustomEvent("create-desktop-shortcut", { detail: contextMenu.item })); setContextMenu(null); }}>
                        <DapnetIcon nameOrSvg="plus" size={14} color="var(--accent)" />
                        Utwórz skrót na pulpicie
                    </div>
                </div>
            )}
        </div>
    );
}
