import React, { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { 
    DndContext, 
    useSensor, 
    useSensors, 
    PointerSensor, 
    DragEndEvent,
    DragStartEvent,
    DragOverlay,
    pointerWithin
} from "@dnd-kit/core";
import { SidebarLayout } from "../SidebarLayout";
import { MENU, SYSTEM_INFO_ITEM } from "../menu";
import { UsersView } from "../features/Admin/UsersView";
import { RolesView } from "../features/Admin/RolesView";
import { UserPermissionsView } from "../features/Admin/UserPermissionsView";
import { RolePermissionsView } from "../features/Admin/RolePermissionsView";
import { AddUserView } from "../features/Admin/AddUserView";
import { EditUserView } from "../features/Admin/EditUserView";
import { DbSessionsView } from "../features/Admin/DbSessionsView";
import { KsefAuthView } from "../features/Ksef/KsefAuthView";
import { CompanyEditView } from "../features/Admin/CompanyEditView";
import { LicenseView } from "../features/Admin/LicenseView";
import { SystemInfoView } from "../features/Admin/SystemInfoView";
import { DevToolsView } from "../features/Admin/DevToolsView";
import { ApiLoggerView } from "../features/Admin/ApiLoggerView";
import { PermissionEmulatorView } from "../features/Admin/PermissionEmulatorView";
import { FolderView } from "../features/FolderView";
import { WindowContainer } from "../components/WindowContainer";
import { WindowSearch } from "../components/WindowSearch";
import { DebugHud } from "../components/DebugHud";
import { Dashboard } from "../Dashboard";
import { useDesktop } from "../DesktopContext";
import { DapnetIcon } from "../components/DapnetIcon";
import { useAuth } from "./Shell";
import "./SystemInfoPanel.css";

interface WindowInfo {
    id: string;
    title: string;
    type: string;
    zIndex: number;
    isMinimized: boolean;
    isBlocked?: boolean;
    parentId?: string;
    layout?: { x: number, y: number, w?: number | string, h?: number | string };
    data?: any;
}

export function ReadyScreen({ status, setGlobalBusy }: { status: any, setGlobalBusy: (v: boolean) => void }) {
    const { user } = useAuth();
    const isDev = user?.isSystemAdmin || user?.login?.toLowerCase() === "admindapnet";

    const { 
        icons, iconScale, updateIcons, reorderIcons, isSpotOccupied, addIcon, findFirstFreeSpot,
        selectedIds, setSelectedIds, activeId, setActiveId,
        contextMenu, setContextMenu, setRenamingId, setNewName, removeIcons, updateScale
    } = useDesktop();

    const [windows, setWindows] = useState<WindowInfo[]>([]);
    const [activeWindowId, setActiveWindowId] = useState<string | null>(null);
    const [nextZIndex, setNextZIndex] = useState(1000);
    const [showSystemInfo, setShowSystemInfo] = useState(false);
    const [showSearch, setShowSearch] = useState(false);
    const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
    const [showDebugInfo, setShowDebugInfo] = useState(() => localStorage.getItem("dev_show_paths") === "true");

    const systemInfoRef = useRef<HTMLDivElement>(null);
    const workspaceScrollRef = useRef<HTMLDivElement>(null);

    const VIEW_FILES: Record<string, string> = {
        "admin.users": "UsersView.tsx", "admin.roles": "RolesView.tsx", "admin.company": "CompanyEditView.tsx",
        "admin.license": "LicenseView.tsx", "ksef.auth": "KsefAuthView.tsx", "desktop.folder": "FolderView.tsx",
        "system.devtools": "DevToolsView.tsx", "admin.db-sessions": "DbSessionsView.tsx",
        "dev.logger": "ApiLoggerView.tsx", "dev.emulator": "PermissionEmulatorView.tsx"
    };

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            const target = event.target as HTMLElement;
            if (showSystemInfo) {
                const isSystemInfoButtonClick = target.closest('.ui-nav-item') && 
                                              (target.innerText?.includes("Informacje o systemie") || 
                                               target.closest('[title="Informacje o systemie"]'));
                if (systemInfoRef.current && !systemInfoRef.current.contains(target) && !isSystemInfoButtonClick) {
                    setShowSystemInfo(false);
                }
            }
            if (target.classList.contains('desktop-container')) {
                setActiveWindowId(null);
            }
        };
        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, [showSystemInfo]);

    useEffect(() => {
        const handleOpenDevTools = () => {
            if (!isDev) return;
            handleMenuSelect("system.devtools");
        };
        window.addEventListener("open-devtools", handleOpenDevTools);
        return () => window.removeEventListener("open-devtools", handleOpenDevTools);
    }, [windows, nextZIndex, isDev]);

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.ctrlKey && (e.key === "`" || e.key === "~")) { e.preventDefault(); setIsSidebarCollapsed(prev => !prev); }
            if (e.ctrlKey && e.code === "Space") { e.preventDefault(); setShowSearch(prev => !prev); }
            if (e.ctrlKey && e.key.toLowerCase() === "q") {
                if (activeWindowId) { e.preventDefault(); closeSpecificWindow(activeWindowId); }
            } else if (e.key === "Escape") {
                if (showSearch) setShowSearch(false); 
                else if (showSystemInfo) setShowSystemInfo(false); 
            }
        };
        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [showSearch, showSystemInfo, activeWindowId]);

    const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 3 } }));

    const scrollToWindow = useCallback((win: WindowInfo) => {
        if (!workspaceScrollRef.current || !win.layout) return;
        const scrollContainer = workspaceScrollRef.current;
        const winX = win.layout.x;
        const viewportW = scrollContainer.clientWidth;
        const currentScroll = scrollContainer.scrollLeft;
        if (winX < currentScroll || (winX + 100) > (currentScroll + viewportW)) {
            scrollContainer.scrollTo({ left: Math.max(0, winX - 50), behavior: 'smooth' });
        }
    }, []);

    const focusWindow = useCallback((id: string) => {
        setWindows(prev => {
            const win = prev.find(w => w.id === id);
            if (!win) return prev;
            if (id === activeWindowId && !win.isMinimized) {
                const updated = prev.map(w => w.id === id ? { ...w, isMinimized: true } : w);
                const nextActive = [...updated].filter(w => !w.isMinimized).sort((a, b) => b.zIndex - a.zIndex)[0];
                setActiveWindowId(nextActive ? nextActive.id : null);
                return updated;
            }
            const newZ = nextZIndex + 10;
            setNextZIndex(newZ);
            setActiveWindowId(id);
            setTimeout(() => scrollToWindow(win), 50);
            return prev.map(w => w.id === id ? { ...w, isMinimized: false, zIndex: newZ } : w);
        });
    }, [activeWindowId, nextZIndex, scrollToWindow]);

    const minimizeWindow = useCallback((id: string) => {
        setWindows(prev => {
            const updated = prev.map(w => w.id === id ? { ...w, isMinimized: true } : w);
            if (id === activeWindowId) {
                const nextActive = [...updated].filter(w => !w.isMinimized).sort((a, b) => b.zIndex - a.zIndex)[0];
                setActiveWindowId(nextActive ? nextActive.id : null);
            }
            return updated;
        });
    }, [activeWindowId]);

    const forceFocusWindow = useCallback((id: string) => {
        setWindows(prev => {
            const win = prev.find(w => w.id === id);
            if (!win) return prev;
            const newZ = nextZIndex + 10;
            setNextZIndex(newZ);
            setActiveWindowId(id);
            setTimeout(() => scrollToWindow(win), 50);
            return prev.map(w => w.id === id ? { ...w, isMinimized: false, zIndex: newZ } : w);
        });
    }, [nextZIndex, scrollToWindow]);

    const handleLayoutChange = useCallback((id: string, layout: { x: number, y: number, w: number | string, h: number | string }, isFinal: boolean = false) => {
        setWindows(prev => {
            const win = prev.find(w => w.id === id);
            if (win && isFinal) {
                const storageKey = win.type === 'desktop.folder' ? `window_layout_folder_${win.data?.folderId}` : `window_layout_${win.type}`;
                localStorage.setItem(storageKey, JSON.stringify(layout));
            }
            return prev.map(w => w.id === id ? { ...w, layout } : w);
        });
    }, []);

    const handleGlobalLayoutChange = useCallback((action: 'cascade' | 'tile-v' | 'tile-h' | 'grid' | 'reset') => {
        if (action === 'reset') {
            Object.keys(localStorage).forEach(key => { if (key.startsWith('window_layout_')) localStorage.removeItem(key); });
            setWindows(prev => prev.map(w => ({ ...w, layout: { x: 100, y: 50, w: "auto", h: "auto" } })));
            return;
        }

        setWindows(prev => {
            const targets = prev.filter(w => w.type !== 'desktop.folder' && !w.isMinimized);
            if (targets.length === 0) return prev;

            const scrollArea = workspaceScrollRef.current;
            if (!scrollArea) return prev;

            const marginTop = 12;
            const marginBottom = 8;
            const marginLeft = 20;
            const marginRight = 8;

            const areaW = scrollArea.clientWidth - marginLeft - marginRight;
            const areaH = scrollArea.clientHeight - marginTop - marginBottom;

            return prev.map(w => {
                const idx = targets.findIndex(t => t.id === w.id);
                if (idx === -1) return w;

                let layout = { ...w.layout };

                if (action === 'cascade') {
                    layout = { x: marginLeft + (idx * 30), y: marginTop + (idx * 30), w: 800, h: 600 };
                } else if (action === 'tile-v') {
                    const wPerWin = areaW / targets.length;
                    layout = { x: idx * wPerWin + marginLeft, y: marginTop, w: wPerWin - 10, h: areaH };
                } else if (action === 'tile-h') {
                    const hPerWin = areaH / targets.length;
                    layout = { x: marginLeft, y: idx * hPerWin + marginTop, w: areaW, h: hPerWin - 10 };
                } else if (action === 'grid') {
                    const cols = Math.ceil(Math.sqrt(targets.length));
                    const rows = Math.ceil(targets.length / cols);
                    const wPerWin = areaW / cols;
                    const hPerWin = areaH / rows;
                    const col = idx % cols;
                    const row = Math.floor(idx / cols);
                    layout = { x: col * wPerWin + marginLeft, y: row * hPerWin + marginTop, w: wPerWin - 10, h: hPerWin - 10 };
                }

                return { ...w, layout };
            });
        });
    }, []);

    const closeAllModules = useCallback(() => {
        setWindows(prev => prev.filter(w => w.type === 'desktop.folder'));
        setActiveWindowId(null);
    }, []);

    const closeSpecificWindow = useCallback((id: string) => {
        setWindows(prev => {
            const newWindows = prev.filter(w => w.id !== id);
            if (id === activeWindowId) {
                const nextActive = [...newWindows].filter(w => !w.isMinimized).sort((a, b) => b.zIndex - a.zIndex)[0];
                setActiveWindowId(nextActive ? nextActive.id : null);
            }
            return newWindows;
        });
    }, [activeWindowId]);

    const handleMenuSelect = useCallback((key: string) => {
        if (key === SYSTEM_INFO_ITEM.key) { setShowSystemInfo(prev => !prev); return; }
        if (key === "system.devtools" && !isDev) return;
        setWindows(prev => {
            const existing = prev.find(w => w.type === key && w.type !== 'desktop.folder');
            if (existing) { setTimeout(() => forceFocusWindow(existing.id), 0); return prev; }
            const savedLayout = localStorage.getItem(`window_layout_${key}`);
            let layout = savedLayout ? JSON.parse(savedLayout) : { x: 100 + (prev.length * 30), y: 50 + (prev.length * 30), w: "auto", h: "auto" };
            const newId = Math.random().toString(36).substr(2, 9);
            const newWin = { id: newId, type: key, title: key.toUpperCase(), zIndex: nextZIndex + 10, isMinimized: false, layout: layout };
            setActiveWindowId(newId); setNextZIndex(z => z + 10);
            return [...prev, newWin];
        });
    }, [nextZIndex, forceFocusWindow, isDev]);

    const handleOpenFolder = useCallback((folderId: string, label: string) => {
        setWindows(prev => {
            const existing = prev.find(w => w.type === 'desktop.folder' && w.data?.folderId === folderId);
            if (existing) { setTimeout(() => forceFocusWindow(existing.id), 0); return prev; }
            const savedLayout = localStorage.getItem(`window_layout_folder_${folderId}`);
            let layout = savedLayout ? JSON.parse(savedLayout) : { x: 150 + (prev.length * 30), y: 100 + (prev.length * 30), w: 600, h: 400 };
            const newId = Math.random().toString(36).substr(2, 9);
            const newWin = { id: newId, type: 'desktop.folder', title: label.toUpperCase(), zIndex: nextZIndex + 10, isMinimized: false, layout: layout, data: { folderId } };
            setActiveWindowId(newId); setNextZIndex(z => z + 10);
            return [...prev, newWin];
        });
    }, [nextZIndex, forceFocusWindow]);

    const handleDragStart = (event: DragStartEvent) => {
        const id = event.active.id as string;
        setActiveId(id);
        if (!selectedIds.includes(id)) setSelectedIds([id]);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over, delta } = event;
        setActiveId(null);
        if (!active) return;
        const activeIcon = icons.find(i => i.id === active.id);
        if (!activeIcon) return;

        const currentGridW = 90 * iconScale;
        const currentGridH = 110 * iconScale;
        const sidebarW = isSidebarCollapsed ? 50 : 280;
        const maxGridY = Math.floor((window.innerHeight - 44 - 40) / currentGridH);

        if (over && over.id !== active.id) {
            const overIdStr = String(over.id);
            if (overIdStr.startsWith('folder-bg-') || (icons.find(i => i.id === over.id)?.type === 'folder')) {
                const targetFolderId = overIdStr.startsWith('folder-bg-') ? overIdStr.replace('folder-bg-', '') : String(over.id);
                if (targetFolderId !== activeIcon.parentId) {
                    const batch: any[] = [];
                    const occupiedInFolder = new Set(icons.filter(i => i.parentId === targetFolderId).map(i => `${i.gridX},${i.gridY}`));
                    selectedIds.forEach(id => {
                        let found = false;
                        for (let y = 0; y < 20 && !found; y++) {
                            for (let x = 0; x < 10 && !found; x++) {
                                if (!occupiedInFolder.has(`${x},${y}`)) {
                                    batch.push({ id, updates: { parentId: targetFolderId, gridX: x, gridY: y } });
                                    occupiedInFolder.add(`${x},${y}`);
                                    found = true;
                                }
                            }
                        }
                    });
                    updateIcons(batch);
                    setSelectedIds([]);
                    return;
                }
            }

            if (overIdStr === 'desktop-bg' && activeIcon.parentId !== null) {
                const batch: any[] = [];
                const localOccupied = new Set<string>();
                const rect = active.rect.current.translated;
                if (rect) {
                    const dropX = rect.left;
                    const dropY = rect.top;
                    selectedIds.forEach(id => {
                        const icon = icons.find(i => i.id === id);
                        if (icon) {
                            const relX = (icon.gridX - activeIcon.gridX) * currentGridW;
                            const relY = (icon.gridY - activeIcon.gridY) * currentGridH;
                            let targetX = Math.round((dropX + relX - sidebarW - 20) / currentGridW);
                            let targetY = Math.round((dropY + relY - 20) / currentGridH);
                            targetX = Math.max(0, targetX);
                            targetY = Math.max(0, Math.min(maxGridY - 1, targetY));
                            if (isSpotOccupied(targetX, targetY, null, selectedIds) || localOccupied.has(`${targetX},${targetY}`)) {
                                const free = findFirstFreeSpot(null, [...selectedIds, ...Array.from(localOccupied)]);
                                targetX = free.x; targetY = free.y;
                            }
                            localOccupied.add(`${targetX},${targetY}`);
                            batch.push({ id, updates: { parentId: null, gridX: targetX, gridY: targetY } });
                        }
                    });
                    updateIcons(batch);
                    setSelectedIds([]);
                    return;
                }
            }
        }

        if (Math.abs(delta.x) > 5 || Math.abs(delta.y) > 5) {
            const baseGridX = activeIcon.gridX + Math.round(delta.x / currentGridW);
            const baseGridY = activeIcon.gridY + Math.round(delta.y / currentGridH);
            const batch: any[] = [];
            const localOccupied = new Set<string>();
            selectedIds.forEach(id => {
                const icon = icons.find(i => i.id === id);
                if (icon && icon.parentId === activeIcon.parentId) {
                    const relX = icon.gridX - activeIcon.gridX;
                    const relY = icon.gridY - activeIcon.gridY;
                    let targetX = baseGridX + relX;
                    let targetY = baseGridY + relY;
                    targetX = Math.max(0, targetX);
                    targetY = Math.max(0, Math.min(maxGridY - 1, targetY));
                    if (isSpotOccupied(targetX, targetY, icon.parentId, selectedIds) || localOccupied.has(`${targetX},${targetY}`)) {
                        let found = false;
                        for (let radius = 1; radius < 5 && !found; radius++) {
                            for (let dx = -radius; dx <= radius && !found; dx++) {
                                for (let dy = -radius; dy <= radius && !found; dy++) {
                                    const tx = targetX + dx; const ty = targetY + dy;
                                    if (tx >= 0 && ty >= 0 && ty < maxGridY && !isSpotOccupied(tx, ty, icon.parentId, selectedIds) && !localOccupied.has(`${tx},${ty}`)) {
                                        targetX = tx; targetY = ty; found = true;
                                    }
                                }
                            }
                        }
                    }
                    localOccupied.add(`${targetX},${targetY}`);
                    batch.push({ id, updates: { gridX: targetX, gridY: targetY } });
                }
            });
            if (batch.length > 0) updateIcons(batch);
        }
    };

    const activeIcon = icons.find(i => i.id === activeId);

    return (
        <DndContext 
            sensors={sensors} 
            onDragStart={handleDragStart} 
            onDragEnd={handleDragEnd} 
            collisionDetection={pointerWithin}
            autoScroll={{
                threshold: { x: 0.1, y: 0.1 },
                acceleration: 15,
                order: 'descending'
            }}
        >
            <div style={{ flex: 1, width: "100%", display: "flex", flexDirection: "column", position: "relative", minHeight: 0 }}>
                <DebugHud />
                <SidebarLayout 
                    onSelect={handleMenuSelect} activeKey={showSystemInfo ? SYSTEM_INFO_ITEM.key : (windows.find(w => w.id === activeWindowId)?.type || null)}
                    viewTitle="Obszar roboczy" openWindows={windows} activeWindowId={activeWindowId} onFocusWindow={focusWindow}
                    onCloseWindow={closeSpecificWindow}
                    onMinimizeWindow={minimizeWindow}
                    onReorderWindows={setWindows}
                    workspaceScrollRef={workspaceScrollRef}
                    isSidebarCollapsed={isSidebarCollapsed} onToggleSidebar={() => setIsSidebarCollapsed(!isSidebarCollapsed)}
                    onLayoutChange={handleGlobalLayoutChange}
                    onCloseWorkspace={closeAllModules}
                    desktop={<Dashboard onOpenModule={handleMenuSelect} onOpenFolder={handleOpenFolder} />}
                    overlays={
                        <>
                            {showSearch && (
                                <div style={{ position: "absolute", inset: 0, zIndex: 300000, pointerEvents: "auto" }}>
                                    <WindowSearch 
                                        windows={windows} 
                                        onClose={() => setShowSearch(false)} 
                                        onSelect={(idOrKey) => { 
                                            const isWindow = windows.some(w => w.id === idOrKey);
                                            if (isWindow) focusWindow(idOrKey);
                                            else handleMenuSelect(idOrKey);
                                            setShowSearch(false); 
                                        }} 
                                    />
                                </div>
                            )}
                            {showSystemInfo && (
                                <div className="system-info-drawer-overlay">
                                    <div className="system-info-drawer" ref={systemInfoRef}>
                                        <div className="ui-panel-header">
                                            <span className="ui-panel-title">INFORMACJE O SYSTEMIE</span>
                                            <button className="btn-close" onClick={() => setShowSystemInfo(false)}>
                                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
                                            </button>
                                        </div>
                                        <div className="system-info-drawer-content">
                                            <SystemInfoView />
                                        </div>
                                    </div>
                                </div>
                            )}
                        </>
                    }
                >
                    {windows.map((w) => (
                        !w.isMinimized && (
                            <WindowContainer key={w.id} id={w.id} type={w.type} title={w.title} isActive={activeWindowId === w.id} zIndex={w.zIndex} onClose={() => closeSpecificWindow(w.id)} onMinimize={() => focusWindow(w.id)} onFocus={() => focusWindow(w.id)} onLayoutChange={handleLayoutChange} initialX={w.layout?.x} initialY={w.layout?.y} initialW={w.layout?.w} initialH={w.layout?.h} minW={300} debugInfo={showDebugInfo ? VIEW_FILES[w.type] : undefined}>
                                {w.type === "admin.users" && <UsersView setGlobalBusy={setGlobalBusy} onClose={() => closeSpecificWindow(w.id)} />}
                                {w.type === "admin.roles" && <RolesView onClose={() => closeSpecificWindow(w.id)} />}
                                {w.type === "admin.company" && <CompanyEditView onClose={() => closeSpecificWindow(w.id)} />}
                                {w.type === "admin.license" && <LicenseView onClose={() => closeSpecificWindow(w.id)} />}
                                {w.type === "desktop.folder" && <FolderView folderId={w.data.folderId} onOpenModule={handleMenuSelect} onOpenFolder={handleOpenFolder} />}
                                {w.type === "system.devtools" && <DevToolsView />}
                                {w.type === "admin.db-sessions" && <DbSessionsView />}
                            </WindowContainer>
                        )
                    ))}
                </SidebarLayout>

                <DragOverlay dropAnimation={null}>
                    {activeId && activeIcon && (
                        <div style={{ position: 'relative' }}>
                            {selectedIds.map(id => {
                                const icon = icons.find(i => i.id === id);
                                if (!icon) return null;
                                const relX = (icon.gridX - activeIcon.gridX) * (90 * iconScale);
                                const relY = (icon.gridY - activeIcon.gridY) * (110 * iconScale);
                                return (
                                    <div key={id} className="desktop-item" style={{ position: "absolute", left: relX, top: relY, opacity: 0.8, scale: `${iconScale}`, pointerEvents: 'none' }}>
                                        <div className="desktop-item__icon"><DapnetIcon nameOrSvg={icon.icon || icon.type} size={26} /></div>
                                        <div className="desktop-item__label">{icon.label}</div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </DragOverlay>
            </div>
        </DndContext>
    );
}
