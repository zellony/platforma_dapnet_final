import React, { useState, useMemo, useRef, useEffect } from "react";
import { useDesktop, DesktopIcon } from "../DesktopContext";
import { useDroppable } from "@dnd-kit/core";
import { DraggableIconItem } from "../Dashboard";
import { DapnetIcon } from "../components/DapnetIcon";

interface FolderViewProps {
    folderId: string;
    onOpenModule: (key: string) => void;
    onOpenFolder: (id: string, label: string) => void;
}

const BASE_GRID_W = 90;
const BASE_GRID_H = 110;

export function FolderView({ folderId: initialFolderId, onOpenModule, onOpenFolder }: FolderViewProps) {
    const { 
        icons, iconScale, updateIcons, removeIcons, 
        selectedIds, setSelectedIds, activeId, renamingId, setRenamingId, newName, setNewName,
        setContextMenu, contextMenu
    } = useDesktop();
    
    const [currentFolderId, setCurrentFolderId] = useState(initialFolderId);
    const [selectionRect, setSelectionRect] = useState<{ x1: number, y1: number, x2: number, y2: number } | null>(null);
    const containerRef = useRef<HTMLDivElement>(null);

    const currentGridW = BASE_GRID_W * iconScale;
    const currentGridH = BASE_GRID_H * iconScale;

    const { setNodeRef: setFolderDropRef } = useDroppable({ id: `folder-bg-${currentFolderId}` });

    const folderIcons = useMemo(() => {
        return icons.filter(i => i.parentId === currentFolderId);
    }, [icons, currentFolderId]);

    const breadcrumbs = useMemo(() => {
        const path: { id: string, label: string }[] = [];
        let currId: string | null = currentFolderId;
        while (currId) {
            const folder = icons.find(i => i.id === currId);
            if (folder) {
                path.unshift({ id: folder.id, label: folder.label });
                if (currId === initialFolderId) break;
                currId = folder.parentId;
            } else break;
        }
        return path;
    }, [icons, currentFolderId, initialFolderId]);

    const goBack = () => {
        const current = icons.find(i => i.id === currentFolderId);
        if (current?.parentId) setCurrentFolderId(current.parentId);
        else if (currentFolderId !== initialFolderId) setCurrentFolderId(initialFolderId);
    };

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (renamingId) return;
            if (e.key === "Delete" && selectedIds.length > 0) {
                removeIcons(selectedIds);
                setSelectedIds([]);
            }
            if (e.key === "Enter" && selectedIds.length === 1) {
                const icon = folderIcons.find(i => i.id === selectedIds[0]);
                if (icon) icon.type === 'module' ? onOpenModule(icon.moduleKey!) : setCurrentFolderId(icon.id);
            }
        };
        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [selectedIds, folderIcons, renamingId, removeIcons, setSelectedIds, onOpenModule]);

    const handleMouseDown = (e: React.MouseEvent) => {
        if (contextMenu) setContextMenu(null);
        if (e.button !== 0 || (e.target as HTMLElement).closest('.desktop-item')) return;
        
        const rect = containerRef.current?.getBoundingClientRect();
        if (!rect) return;
        
        const startX = e.clientX - rect.left;
        const startY = e.clientY - rect.top;

        setSelectionRect({ x1: startX, y1: startY, x2: startX, y2: startY });
        if (!e.ctrlKey) setSelectedIds([]);

        const onMouseMove = (me: MouseEvent) => {
            const curX = me.clientX - rect.left;
            const curY = me.clientY - rect.top;
            setSelectionRect({ x1: startX, y1: startY, x2: curX, y2: curY });

            const xMin = Math.min(startX, curX), xMax = Math.max(startX, curX), yMin = Math.min(startY, curY), yMax = Math.max(startY, curY);
            
            const newlySelected = folderIcons.filter(icon => {
                const iconLeft = icon.gridX * currentGridW + 6, iconTop = icon.gridY * currentGridH + 6;
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

        window.addEventListener("mousemove", onMouseMove);
        window.addEventListener("mouseup", onMouseUp);
    };

    return (
        <div 
            style={{ flex: 1, display: "flex", flexDirection: "column", background: "var(--bg-main)", overflow: "hidden" }}
        >
            {/* ✅ CIEMNIEJSZY PASEK NAWIGACJI DLA KONTRASTU */}
            <div style={{ height: "36px", background: "var(--bg-main)", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", padding: "0 12px", gap: 12 }}>
                <button 
                    onClick={(e) => { e.stopPropagation(); goBack(); }} 
                    disabled={currentFolderId === initialFolderId}
                    className="btn-close"
                    style={{ width: "24px", height: "24px", color: currentFolderId === initialFolderId ? "var(--border)" : "var(--text-muted)" }}
                >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><polyline points="15 18 9 12 15 6"/></svg>
                </button>
                
                <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "10px", color: "var(--text-muted)", userSelect: "none", fontWeight: 600, letterSpacing: "0.5px" }}>
                    <span style={{ opacity: 0.5 }}>PULPIT</span>
                    <span style={{ opacity: 0.3 }}>/</span>
                    {breadcrumbs.map((b, idx) => (
                        <React.Fragment key={b.id}>
                            {idx > 0 && <span style={{ opacity: 0.3 }}>/</span>}
                            <span 
                                onClick={(e) => { e.stopPropagation(); setCurrentFolderId(b.id); }}
                                style={{ 
                                    cursor: "pointer", 
                                    color: idx === breadcrumbs.length - 1 ? "var(--accent)" : "inherit", 
                                    transition: "color 0.2s"
                                }}
                            >
                                {b.label.toUpperCase()}
                            </span>
                        </React.Fragment>
                    ))}
                </div>
            </div>

            <div 
                ref={(el) => { containerRef.current = el; setFolderDropRef(el); }}
                onMouseDown={handleMouseDown}
                onContextMenu={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    setSelectedIds([]);
                    setContextMenu({ x: e.clientX, y: e.clientY, targetId: null, folderId: currentFolderId });
                }}
                style={{ 
                    flex: 1, 
                    overflowY: "auto", 
                    position: "relative"
                }}
            >
                {selectionRect && (
                    <div className="selection-marquee" style={{ 
                        position: "absolute",
                        left: Math.min(selectionRect.x1, selectionRect.x2), 
                        top: Math.min(selectionRect.y1, selectionRect.y2), 
                        width: Math.abs(selectionRect.x2 - selectionRect.x1), 
                        height: Math.abs(selectionRect.y2 - selectionRect.y1),
                        zIndex: 1000
                    }} />
                )}

                {folderIcons.map(icon => (
                    <DraggableIconItem 
                        key={icon.id}
                        icon={icon} 
                        isSelected={selectedIds.includes(icon.id)} 
                        isDraggingAny={activeId !== null && selectedIds.includes(icon.id)}
                        isRenaming={renamingId === icon.id} 
                        newName={newName} 
                        onNewNameChange={setNewName} 
                        onRenameFinish={() => { if (renamingId && newName.trim()) updateIcons([{id: renamingId, updates: { label: newName.trim() }}]); setRenamingId(null); }}
                        onSelect={(ctrl: boolean) => { setContextMenu(null); setSelectedIds(ctrl ? (selectedIds.includes(icon.id) ? selectedIds.filter(x => x !== icon.id) : [...selectedIds, icon.id]) : [icon.id]); setRenamingId(null); }}
                        onDoubleClick={() => icon.type === 'module' ? onOpenModule(icon.moduleKey!) : setCurrentFolderId(icon.id)}
                        onContextMenu={(e: any) => { 
                            e.preventDefault(); 
                            e.stopPropagation(); 
                            if (!selectedIds.includes(icon.id)) setSelectedIds([icon.id]); 
                            setContextMenu({ x: e.clientX, y: e.clientY, targetId: icon.id, folderId: currentFolderId });
                        }}
                        gridW={currentGridW}
                        gridH={currentGridH}
                        iconScale={iconScale}
                        offsetX={6}
                        offsetY={6}
                    />
                ))}
            </div>
        </div>
    );
}
