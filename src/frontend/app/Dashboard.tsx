import React, { useState, useEffect, useRef } from "react";
import { useDroppable, useDraggable } from "@dnd-kit/core";
import { useDesktop, DesktopIcon } from "./DesktopContext";
import { DapnetIcon } from "./components/DapnetIcon";
import { CSS } from "@dnd-kit/utilities";
import "./desktop.css";

interface DashboardProps {
    onOpenModule: (key: string) => void;
    onOpenFolder: (folderId: string, label: string) => void;
}

const BASE_GRID_W = 90;
const BASE_GRID_H = 110;

export function Dashboard({ onOpenModule, onOpenFolder }: DashboardProps) {
    const { 
        icons, iconScale, updateScale, addIcon, removeIcons, findFirstFreeSpot,
        selectedIds, setSelectedIds, activeId, renamingId, setRenamingId, newName, setNewName,
        setContextMenu, contextMenu, updateIcons
    } = useDesktop();

    const containerRef = useRef<HTMLDivElement>(null);
    const [selectionRect, setSelectionRect] = useState<{ x1: number, y1: number, x2: number, y2: number } | null>(null);

    const currentGridW = BASE_GRID_W * iconScale;
    const currentGridH = BASE_GRID_H * iconScale;

    const focusDesktop = () => { if (containerRef.current) containerRef.current.focus(); };

    useEffect(() => {
        const handleWheel = (e: WheelEvent) => {
            if (e.ctrlKey) {
                e.preventDefault();
                const delta = e.deltaY > 0 ? -0.1 : 0.1;
                updateScale(Math.min(Math.max(iconScale + delta, 0.5), 2.0));
            }
        };
        window.addEventListener("wheel", handleWheel, { passive: false });
        return () => window.removeEventListener("wheel", handleWheel);
    }, [iconScale, updateScale]);

    useEffect(() => {
        const handleCreateShortcut = (e: any) => {
            const item = e.detail;
            const exists = icons.some(i => i.parentId === null && i.moduleKey === item.key);
            if (exists) return;
            addIcon({ label: item.label, type: 'module', moduleKey: item.key, icon: item.icon, parentId: null });
            setSelectedIds([]);
        };
        window.addEventListener("create-desktop-shortcut", handleCreateShortcut);
        return () => window.removeEventListener("create-desktop-shortcut", handleCreateShortcut);
    }, [addIcon, setSelectedIds, icons]);

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (renamingId) return;
            if (e.key === "Delete" && selectedIds.length > 0) {
                removeIcons(selectedIds);
                setSelectedIds([]);
            }
            if (e.key === "Enter" && selectedIds.length === 1) {
                const icon = icons.find(i => i.id === selectedIds[0]);
                if (icon) icon.type === 'module' ? onOpenModule(icon.moduleKey!) : onOpenFolder(icon.id, icon.label);
            }
        };
        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [selectedIds, icons, renamingId, removeIcons, setSelectedIds, onOpenModule, onOpenFolder]);

    const handleMouseDown = (e: React.MouseEvent) => {
        focusDesktop();
        if (contextMenu) setContextMenu(null);
        if (e.button !== 0 || (e.target as HTMLElement).closest('.desktop-item')) return;
        
        const rect = containerRef.current?.getBoundingClientRect();
        if (!rect) return;
        const startX = e.clientX - rect.left, startY = e.clientY - rect.top;
        setSelectionRect({ x1: startX, y1: startY, x2: startX, y2: startY });
        if (!e.ctrlKey) setSelectedIds([]);

        const onMouseMove = (me: MouseEvent) => {
            const curX = me.clientX - rect.left, curY = me.clientY - rect.top;
            setSelectionRect({ x1: startX, y1: startY, x2: curX, y2: curY });
            const xMin = Math.min(startX, curX), xMax = Math.max(startX, curX), yMin = Math.min(startY, curY), yMax = Math.max(startY, curY);
            const newlySelected = icons.filter(i => !i.parentId).filter(icon => {
                const iconLeft = icon.gridX * currentGridW + 20, iconTop = icon.gridY * currentGridH + 20;
                const iconRight = iconLeft + (80 * iconScale), iconBottom = iconTop + (100 * iconScale);
                return !(iconRight < xMin || iconLeft > xMax || iconBottom < yMin || iconTop > yMax);
            }).map(i => i.id);
            setSelectedIds(prev => me.ctrlKey ? Array.from(new Set([...prev, ...newlySelected])) : newlySelected);
        };
        const onMouseUp = () => { 
            setSelectionRect(null); 
            window.removeEventListener("mousemove", onMouseMove); 
            window.removeEventListener("mouseup", onMouseUp); 
        };
        window.addEventListener("mousemove", onMouseMove); window.addEventListener("mouseup", onMouseUp);
    };

    const { setNodeRef: setDesktopDropRef } = useDroppable({ id: "desktop-bg" });

    return (
        <div 
            ref={(el) => { containerRef.current = el; setDesktopDropRef(el); }} 
            tabIndex={0} 
            className="desktop-container" 
            onMouseDown={handleMouseDown} 
            onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); setSelectedIds([]); setContextMenu({ x: e.clientX, y: e.clientY, targetId: null }); }} 
            style={{ 
                ['--icon-scale' as any]: iconScale,
                width: "100%", // ✅ PRZYWRÓCONO STANDARDOWĄ SZEROKOŚĆ
                height: "100%",
                position: "relative"
            }}
        >
            {selectionRect && <div className="selection-marquee" style={{ left: Math.min(selectionRect.x1, selectionRect.x2), top: Math.min(selectionRect.y1, selectionRect.y2), width: Math.abs(selectionRect.x2 - selectionRect.x1), height: Math.abs(selectionRect.y2 - selectionRect.y1) }} />}
            
            {icons.filter(i => !i.parentId).map(icon => (
                <DraggableIconItem 
                    key={icon.id} 
                    icon={icon} 
                    isSelected={selectedIds.includes(icon.id)} 
                    isDraggingAny={activeId !== null && selectedIds.includes(icon.id)} 
                    isRenaming={renamingId === icon.id} 
                    newName={newName} 
                    onNewNameChange={setNewName} 
                    onRenameFinish={() => { if (renamingId && newName.trim()) updateIcons([{id: renamingId, updates: { label: newName.trim() }}]); setRenamingId(null); focusDesktop(); }} 
                    onSelect={(isCtrl) => { setContextMenu(null); setSelectedIds(prev => isCtrl ? (prev.includes(icon.id) ? prev.filter(x => x !== icon.id) : [...prev, icon.id]) : [icon.id]); setRenamingId(null); focusDesktop(); }} 
                    onDoubleClick={() => icon.type === 'module' ? onOpenModule(icon.moduleKey!) : onOpenFolder(icon.id, icon.label)} 
                    onContextMenu={(e: any) => { e.preventDefault(); e.stopPropagation(); setContextMenu({ x: e.clientX, y: e.clientY, targetId: icon.id }); if (!selectedIds.includes(icon.id)) setSelectedIds([icon.id]); focusDesktop(); }} 
                    gridW={currentGridW}
                    gridH={currentGridH}
                    iconScale={iconScale}
                />
            ))}

            {contextMenu && (
                <div className="desktop-context-menu" style={{ top: contextMenu.y, left: contextMenu.x }} onMouseDown={e => e.stopPropagation()}>
                    <div className="desktop-context-menu-header">{contextMenu.targetId ? "OPCJE ELEMENTU" : "PULPIT"}</div>
                    {contextMenu.targetId ? (
                        <>
                            <div className="desktop-context-menu-item" onClick={() => { const icon = icons.find(i => i.id === contextMenu.targetId); if (icon?.type === 'module') onOpenModule(icon.moduleKey!); else if (icon) onOpenFolder(icon.id, icon.label); setContextMenu(null); }}>
                                <DapnetIcon nameOrSvg="layout" size={14} color="var(--accent)" /> Otwórz
                            </div>
                            <div className="desktop-context-menu-item" onClick={() => { const icon = icons.find(i => i.id === contextMenu.targetId); if (icon) { setRenamingId(icon.id); setNewName(icon.label); } setContextMenu(null); }}>
                                <DapnetIcon nameOrSvg="file-text" size={14} color="var(--accent)" /> Zmień nazwę
                            </div>
                            <div className="desktop-context-menu-separator"></div>
                            <div className="desktop-context-menu-item desktop-context-menu-item--danger" onClick={() => { removeIcons(selectedIds); setSelectedIds([]); setContextMenu(null); }}>
                                <DapnetIcon nameOrSvg="trash" size={14} color="var(--danger)" /> Usuń
                            </div>
                        </>
                    ) : (
                        <>
                            <div className="desktop-context-menu-item" onClick={() => { const id = addIcon({ label: "Nowy folder", type: 'folder', icon: 'folder', parentId: null }); setRenamingId(id); setNewName("Nowy folder"); setContextMenu(null); }}>
                                <DapnetIcon nameOrSvg="folder" size={14} color="var(--accent)" /> Nowy folder
                            </div>
                            <div className="desktop-context-menu-item" onClick={() => { const batch: any[] = []; let cx = 0, cy = 0; icons.filter(i => !i.parentId).forEach(icon => { batch.push({ id: icon.id, updates: { gridX: cx, gridY: cy } }); cy++; if (cy > 7) { cy = 0; cx++; } }); updateIcons(batch); setContextMenu(null); }}>
                                <DapnetIcon nameOrSvg="menu" size={14} color="var(--accent)" /> Uporządkuj ikony
                            </div>
                            <div className="desktop-context-menu-separator"></div>
                            <div className="desktop-context-menu-item" onClick={() => { updateScale(1.0); setContextMenu(null); }}>
                                <DapnetIcon nameOrSvg="layout" size={14} color="var(--accent)" /> Resetuj skalę ikon
                            </div>
                        </>
                    )}
                </div>
            )}
        </div>
    );
}

export function DraggableIconItem({ icon, isSelected, isDraggingAny, isRenaming, newName, onNewNameChange, onRenameFinish, onSelect, onDoubleClick, onContextMenu, gridW, gridH, iconScale, offsetX = 20, offsetY = 20 }: any) {
    const { attributes, listeners, setNodeRef: setDragRef, transform, isDragging } = useDraggable({ id: icon.id });
    const { setNodeRef: setDropRef, isOver } = useDroppable({ id: icon.id, disabled: isDraggingAny });
    
    const style: React.CSSProperties = { 
        left: gridW * icon.gridX + offsetX, 
        top: gridH * icon.gridY + offsetY, 
        transform: transform ? `translate3d(${transform.x}px, ${transform.y}px, 0)` : undefined,
        opacity: isDraggingAny ? 0 : 1, 
        background: isOver ? 'rgba(74, 144, 226, 0.1)' : 'transparent',
        zIndex: isDragging ? 1000 : (isSelected ? 10 : 1),
        transition: isDragging ? 'none' : 'background 0.2s, border-color 0.2s',
        transformOrigin: 'top left'
    };

    const inputRef = useRef<HTMLInputElement>(null);
    useEffect(() => { if (isRenaming && inputRef.current) { inputRef.current.focus(); inputRef.current.select(); } }, [isRenaming]);
    const setRefs = (el: HTMLDivElement) => { setDragRef(el); setDropRef(el); };

    return (
        <div ref={setRefs} style={style} className={`desktop-item ${isSelected ? 'desktop-item--selected' : ''}`} {...attributes} {...listeners} onClick={(e) => { e.stopPropagation(); onSelect(e.ctrlKey); }} onDoubleClick={(e) => { e.stopPropagation(); onDoubleClick(); }} onContextMenu={onContextMenu}>
            <div className="desktop-item__icon"><DapnetIcon nameOrSvg={icon.icon || icon.type} size={26} /></div>
            {isRenaming ? <input ref={inputRef} className="desktop-item__input" value={newName} onChange={e => onNewNameChange(e.target.value)} onBlur={onRenameFinish} onKeyDown={e => e.key === 'Enter' && (e.stopPropagation() || onRenameFinish())} onClick={e => e.stopPropagation()} /> : <div className="desktop-item__label">{icon.label}</div>}
        </div>
    );
}
