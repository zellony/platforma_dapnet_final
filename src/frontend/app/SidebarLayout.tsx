import React, { useState, useEffect, useRef, forwardRef, useMemo } from "react";
import { Sidebar } from "./layout/Sidebar";
import { DapnetIcon } from "./components/DapnetIcon";
import { 
    DndContext, 
    closestCenter, 
    KeyboardSensor, 
    PointerSensor, 
    useSensor, 
    useSensors,
    DragEndEvent,
    DragStartEvent,
    DragOverlay,
    defaultDropAnimationSideEffects
} from "@dnd-kit/core";
import {
    arrayMove,
    SortableContext,
    sortableKeyboardCoordinates,
    horizontalListSortingStrategy,
    useSortable
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useDesktop } from "./DesktopContext";
import "./Taskbar.css";

interface SidebarLayoutProps {
    children: React.ReactNode;
    desktop?: React.ReactNode;
    overlays?: React.ReactNode;
    onSelect: (key: string) => void;
    activeKey?: string | null;
    viewTitle: string;
    openWindows?: any[];
    activeWindowId?: string | null;
    onFocusWindow?: (id: string) => void;
    onCloseWindow?: (id: string) => void;
    onMinimizeWindow?: (id: string) => void;
    onLayoutChange?: (type: 'cascade' | 'tile-v' | 'tile-h' | 'grid' | 'reset') => void;
    isSidebarCollapsed?: boolean;
    onToggleSidebar?: () => void;
    onAddShortcut?: (item: any) => void;
    onCloseWorkspace?: () => void;
    onQuitRequest?: () => void;
    onReorderWindows?: (newWindows: any[]) => void;
    workspaceScrollRef?: React.RefObject<HTMLDivElement>;
}

const TaskbarItem = forwardRef(({ win, activeWindowId, taskbarMode, isDragging, style, ...props }: any, ref: any) => {
    return (
        <div 
            ref={ref}
            className={`taskbar-item ${activeWindowId === win.id ? 'active' : ''} ${isDragging ? 'dragging' : ''}`}
            style={{
                minWidth: taskbarMode === 'icons' ? "40px" : "140px",
                maxWidth: taskbarMode === 'icons' ? "40px" : "220px",
                padding: taskbarMode === 'icons' ? "0" : "0 12px",
                justifyContent: taskbarMode === 'icons' ? "center" : "flex-start",
                gap: 10,
                opacity: isDragging ? 0.3 : 1,
                userSelect: "none",
                cursor: "pointer",
                ...style
            }}
            title={win.title}
            {...props}
        >
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0, pointerEvents: "none" }}>
                <DapnetIcon nameOrSvg={win.type} size={18} color="currentColor" />
            </div>
            {taskbarMode === 'full' && (
                <span style={{ fontSize: "12px", color: "inherit", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", fontWeight: activeWindowId === win.id ? 600 : 400, letterSpacing: "0.3px", pointerEvents: "none" }}>
                    {win.title}
                </span>
            )}
            {activeWindowId === win.id && <div className="taskbar-indicator"></div>}
        </div>
    );
});

function SortableTaskbarItem({ win, activeWindowId, taskbarMode, onFocusWindow, onContextMenu }: any) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: win.id });
    const style = { transform: CSS.Translate.toString(transform), transition };
    return (
        <TaskbarItem 
            ref={setNodeRef} win={win} activeWindowId={activeWindowId} taskbarMode={taskbarMode} isDragging={isDragging} style={style}
            onClick={() => onFocusWindow?.(win.id)} onContextMenu={(e: any) => onContextMenu(e, win)} {...attributes} {...listeners}
        />
    );
}

export function SidebarLayout(props: SidebarLayoutProps) {
    const { 
        children, desktop, overlays, onSelect, activeKey, openWindows = [], activeWindowId,
        onFocusWindow, onCloseWindow, onMinimizeWindow, onLayoutChange, isSidebarCollapsed, onToggleSidebar, onAddShortcut, onCloseWorkspace, onQuitRequest,
        onReorderWindows, workspaceScrollRef
    } = props;

    const { icons, iconScale } = useDesktop();

    const [showLayoutMenu, setShowLayoutMenu] = useState(false);
    const [showCloseAllConfirm, setShowCloseAllConfirm] = useState(false);
    const [showQuitConfirm, setShowQuitConfirm] = useState(false);
    const [taskbarMode, setTaskbarMode] = useState<'full' | 'icons'>(() => (localStorage.getItem("taskbar_mode") as any) || 'full');
    const [taskbarContextMenu, setTaskbarContextMenu] = useState<{ x: number, y: number, win: any } | null>(null);
    
    const [activeDragId, setActiveDragId] = useState<string | null>(null);
    const [folderContextMenu, setFolderContextMenu] = useState<{ x: number, y: number } | null>(null);
    const [showFolderPreview, setShowFolderPreview] = useState(false);
    const [previewPos, setPreviewPos] = useState({ x: 0 });
    const previewTimeoutRef = useRef<any>(null);
    const folderTileRef = useRef<HTMLDivElement>(null);

    // ✅ STABILNY I NATURALNY ROZMIAR OBSZARU ROBOCZEGO
    const virtualWidth = useMemo(() => {
        const PADDING = 200; // Mniejszy padding dla naturalnego efektu
        let maxRight = 0;
        
        openWindows.forEach(win => {
            if (win.layout && !win.isMinimized) {
                const x = win.layout.x || 0;
                const w = typeof win.layout.w === 'number' ? win.layout.w : 800;
                if (x + w > maxRight) maxRight = x + w;
            }
        });

        const currentGridW = 90 * iconScale;
        icons.filter(i => i.parentId === null).forEach(icon => {
            const right = (icon.gridX * currentGridW) + currentGridW;
            if (right > maxRight) maxRight = right;
        });

        // Jeśli obiekty mieszczą się w ekranie, nie pokazujemy suwaka
        if (maxRight < window.innerWidth - 20) return "100%";

        // Zwracamy maxRight + mały zapas
        return maxRight + PADDING;
    }, [openWindows, icons, iconScale]);

    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 3 } }),
        useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
    );

    useEffect(() => {
        const handleClick = () => {
            setShowLayoutMenu(false);
            setTaskbarContextMenu(null);
            setFolderContextMenu(null);
        };
        window.addEventListener("click", handleClick);
        return () => window.removeEventListener("click", handleClick);
    }, []);

    const toggleTaskbarMode = () => {
        const newMode = taskbarMode === 'full' ? 'icons' : 'full';
        setTaskbarMode(newMode);
        localStorage.setItem("taskbar_mode", newMode);
    };

    const handleTaskbarContextMenu = (e: React.MouseEvent, win: any) => {
        e.preventDefault();
        e.stopPropagation();
        setTaskbarContextMenu({ x: e.clientX, y: e.clientY, win });
    };

    const modules = openWindows.filter(w => w.type !== 'desktop.folder');
    const folders = openWindows.filter(w => w.type === 'desktop.folder');
    const isAnyFolderActive = folders.some(f => f.id === activeWindowId);

    const handleFolderMouseEnter = () => {
        if (folders.length === 0) return;
        if (previewTimeoutRef.current) clearTimeout(previewTimeoutRef.current);
        if (folderTileRef.current) {
            const rect = folderTileRef.current.getBoundingClientRect();
            setPreviewPos({ x: rect.left });
        }
        setShowFolderPreview(true);
    };

    const handleFolderMouseLeave = () => {
        previewTimeoutRef.current = setTimeout(() => setShowFolderPreview(false), 300);
    };

    const handleDragStart = (event: DragStartEvent) => {
        setActiveDragId(event.active.id as string);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        setActiveDragId(null);
        if (over && active.id !== over.id) {
            const oldIndex = openWindows.findIndex(w => w.id === active.id);
            const newIndex = openWindows.findIndex(w => w.id === over.id);
            if (onReorderWindows) {
                onReorderWindows(arrayMove(openWindows, oldIndex, newIndex));
            }
        }
    };

    const activeDragWin = openWindows.find(w => w.id === activeDragId);

    return (
        <div className="app-content" style={{ height: "100vh", display: "flex", width: "100vw", overflow: "hidden" }}>
            <div style={{ width: isSidebarCollapsed ? "50px" : "280px", height: "100%", transition: "width 0.2s ease-out", zIndex: 1000, flexShrink: 0 }}>
                <Sidebar onSelect={onSelect} activeKey={activeKey} isCollapsed={isSidebarCollapsed} onToggle={onToggleSidebar || (() => {})} onAddShortcut={onAddShortcut} onQuitRequest={onQuitRequest} />
            </div>
            
            <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0, height: "100%", position: "relative" }}>
                
                <div ref={workspaceScrollRef} className="workspace-scroll-area" style={{ flex: 1, position: "relative", overflowX: "auto", overflowY: "hidden" }}>
                    <div style={{ width: virtualWidth, height: "100%", position: "absolute", top: 0, left: 0, pointerEvents: "none" }}>
                        <div style={{ position: "absolute", inset: 0, zIndex: 0, pointerEvents: "auto" }}>{desktop}</div>
                        <div style={{ position: "absolute", inset: 0, zIndex: 10, pointerEvents: "none" }}>{children}</div>
                        <div style={{ position: "absolute", inset: 0, zIndex: 100, pointerEvents: "none" }}>{overlays}</div>
                    </div>
                    
                    <div style={{ width: virtualWidth, height: "1px", pointerEvents: "none" }} />
                </div>

                <div className="taskbar" style={{ userSelect: "none" }}>
                    <div style={{ position: "relative", height: "100%", display: "flex", alignItems: "center" }}>
                        <div onClick={(e) => { e.stopPropagation(); setShowLayoutMenu(!showLayoutMenu); }} className={`taskbar-settings-btn ${showLayoutMenu ? 'active' : ''}`}>
                            <DapnetIcon nameOrSvg="layout" size={20} color={showLayoutMenu ? "var(--accent)" : "rgba(184, 196, 208, 0.8)"} />
                        </div>
                        {showLayoutMenu && (
                            <div className="taskbar-menu" onClick={(e) => e.stopPropagation()}>
                                <div className="taskbar-menu-header">ZARZĄDZANIE MODUŁAMI</div>
                                <div className="taskbar-menu-item" onClick={() => { onLayoutChange?.('cascade'); setShowLayoutMenu(false); }}>Kaskada</div>
                                <div className="taskbar-menu-item" onClick={() => { onLayoutChange?.('tile-v'); setShowLayoutMenu(false); }}>Sąsiadująco pionowo</div>
                                <div className="taskbar-menu-item" onClick={() => { onLayoutChange?.('tile-h'); setShowLayoutMenu(false); }}>Sąsiadująco poziomo</div>
                                <div className="taskbar-menu-item" onClick={() => { onLayoutChange?.('grid'); setShowLayoutMenu(false); }}>Siatka modułów</div>
                                <div className="taskbar-menu-separator"></div>
                                <div className="taskbar-menu-header">AKCJE GRUPOWE</div>
                                <div className="taskbar-menu-item taskbar-menu-item--danger" onClick={() => { setShowCloseAllConfirm(true); setShowLayoutMenu(false); }}>
                                    <DapnetIcon nameOrSvg="trash" size={14} />Zamknij wszystkie moduły
                                </div>
                                <div className="taskbar-menu-separator"></div>
                                <div className="taskbar-menu-header">USTAWIENIA PASKA</div>
                                <div className="taskbar-menu-item" onClick={toggleTaskbarMode}>Tryb: {taskbarMode === 'full' ? 'Ikona + Tekst' : 'Tylko ikony'}</div>
                                <div className="taskbar-menu-separator"></div>
                                <div className="taskbar-menu-item" style={{ color: "var(--accent)" }} onClick={() => { onLayoutChange?.('reset'); setShowLayoutMenu(false); }}>Przywróć standardowe rozmiary</div>
                            </div>
                        )}
                    </div>
                    <div style={{ width: "1px", height: "24px", background: "rgba(255,255,255,0.15)", margin: "0 12px" }}></div>
                    
                    <div style={{ display: "flex", alignItems: "center", gap: 6, flex: 1, overflowX: "auto", height: "100%" }}>
                        {folders.length > 0 && (
                            <div 
                                ref={folderTileRef}
                                onMouseEnter={handleFolderMouseEnter}
                                onMouseLeave={handleFolderMouseLeave}
                                onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); setFolderContextMenu({ x: e.clientX, y: e.clientY }); }}
                                onClick={() => {
                                    const lastFolder = folders[folders.length - 1];
                                    if (lastFolder) onFocusWindow?.(lastFolder.id);
                                }}
                                className={`taskbar-item ${isAnyFolderActive ? 'active' : ''}`}
                                style={{ 
                                    minWidth: taskbarMode === 'icons' ? "40px" : "140px", 
                                    maxWidth: taskbarMode === 'icons' ? "40px" : "220px", 
                                    padding: taskbarMode === 'icons' ? "0" : "0 12px", 
                                    justifyContent: taskbarMode === 'icons' ? "center" : "flex-start", 
                                    gap: 10,
                                    position: "relative",
                                    cursor: "pointer",
                                    userSelect: "none"
                                }}
                            >
                                <div style={{ display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0, position: "relative" }}>
                                    <DapnetIcon nameOrSvg="folder" size={18} color="currentColor" />
                                    {taskbarMode === 'icons' && folders.length > 1 && (
                                        <div style={{
                                            position: "absolute", top: -6, right: -8, background: "var(--accent)", color: "#FFF", fontSize: "8px", fontWeight: 800, width: "14px", height: "14px", borderRadius: "50%", display: "flex", alignItems: "center", justifyContent: "center", boxShadow: "0 2px 4px rgba(0,0,0,0.3)", border: "1px solid var(--bg-panel)"
                                        }}>{folders.length}</div>
                                    )}
                                </div>
                                {taskbarMode === 'full' && (
                                    <span style={{ fontSize: "12px", color: "inherit", fontWeight: isAnyFolderActive ? 600 : 400 }}>
                                        FOLDERY ({folders.length})
                                    </span>
                                )}
                                {isAnyFolderActive && <div className="taskbar-indicator"></div>}
                            </div>
                        )}

                        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
                            <SortableContext items={modules.map(m => m.id)} strategy={horizontalListSortingStrategy}>
                                <div style={{ display: "flex", alignItems: "center", gap: 6, height: "100%" }}>
                                    {modules.map((win) => (
                                        <SortableTaskbarItem 
                                            key={win.id} 
                                            win={win} 
                                            activeWindowId={activeWindowId} 
                                            taskbarMode={taskbarMode} 
                                            onFocusWindow={onFocusWindow} 
                                            onContextMenu={handleTaskbarContextMenu} 
                                        />
                                    ))}
                                </div>
                            </SortableContext>

                            <DragOverlay dropAnimation={{
                                sideEffects: defaultDropAnimationSideEffects({
                                    styles: { active: { opacity: '0.3' } }
                                })
                            }}>
                                {activeDragId && activeDragWin ? (
                                    <TaskbarItem 
                                        win={activeDragWin} 
                                        activeWindowId={activeWindowId} 
                                        taskbarMode={taskbarMode} 
                                        style={{ cursor: 'grabbing', background: 'var(--bg-panel)', borderRadius: '6px', border: '1px solid var(--accent)', boxShadow: '0 10px 30px rgba(0,0,0,0.5)', opacity: 1 }}
                                    />
                                ) : null}
                            </DragOverlay>
                        </DndContext>
                    </div>
                </div>
            </div>
        </div>
    );
}
