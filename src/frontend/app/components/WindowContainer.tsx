import React, { useState, useRef, useEffect } from "react";

interface WindowContainerProps {
    id: string;
    type: string;
    title: string;
    children: React.ReactNode;
    onClose: () => void;
    onMinimize: () => void;
    onFocus: () => void;
    onLayoutChange?: (id: string, layout: { x: number, y: number, w: number | string, h: number | string }, isFinal?: boolean) => void;
    isActive: boolean;
    isBlocked?: boolean;
    zIndex: number;
    initialX?: number;
    initialY?: number;
    initialW?: number | string;
    initialH?: number | string;
    minW?: number;
    minH?: number;
    debugInfo?: string;
}

const EDGE = 60;          // px - strefa przy krawędzi
const MAX_SPEED = 25;     // px per frame

const clamp01 = (v: number) => Math.max(0, Math.min(1, v));

export function WindowContainer({ 
    id, type, title, children, onClose, onMinimize, onFocus, onLayoutChange, 
    isActive, isBlocked, zIndex, initialX = 50, initialY = 50, initialW, initialH,
    minW = 300, minH = 200, debugInfo
}: WindowContainerProps) {
    const [pos, setPos] = useState({ x: initialX, y: initialY });
    const [size, setSize] = useState<{w: number | string, h: number | string}>({ 
        w: initialW || "auto", 
        h: initialH || "auto" 
    });
    const [isMaximized, setIsMaximized] = useState(false);
    
    const winRef = useRef<HTMLDivElement>(null);
    const rafRef = useRef<number | null>(null);
    
    // ✅ STAN DRAGOWANIA Z KOMPENSACJĄ SCROLLA
    const dragRef = useRef({
        active: false,
        offsetX: 0,
        offsetY: 0,
        scrollVX: 0,
        lastPointerX: 0,
        lastPointerY: 0
    });

    useEffect(() => {
        if (isMaximized || dragRef.current.active) return;
        setPos({ x: initialX, y: initialY });
        setSize({ w: initialW || "auto", h: initialH || "auto" });
    }, [initialX, initialY, initialW, initialH, isMaximized]);

    function startRaf() {
        if (rafRef.current) return;
        const tick = () => {
            const d = dragRef.current;
            const scrollContainer = winRef.current?.closest('.workspace-scroll-area') as HTMLElement;
            
            if (!scrollContainer || !d.active) {
                rafRef.current = null;
                return;
            }

            // 1. Wykonaj scroll kontenera
            if (d.scrollVX !== 0) {
                scrollContainer.scrollLeft += d.scrollVX;
            }

            // 2. Oblicz nową pozycję okna w "Workspace Coords" (clientX + scrollLeft)
            // To automatycznie kompensuje ruch scrolla
            const rect = scrollContainer.getBoundingClientRect();
            const pointerXInWorkspace = (d.lastPointerX - rect.left) + scrollContainer.scrollLeft;
            const pointerYInWorkspace = (d.lastPointerY - rect.top) + scrollContainer.scrollTop;

            const newX = pointerXInWorkspace - d.offsetX;
            const newY = pointerYInWorkspace - d.offsetY;

            setPos({ x: newX, y: newY });
            onLayoutChange?.(id, { x: newX, y: newY, w: winRef.current?.offsetWidth || 300, h: winRef.current?.offsetHeight || 200 }, false);

            rafRef.current = requestAnimationFrame(tick);
        };
        rafRef.current = requestAnimationFrame(tick);
    }

    function stopRaf() {
        if (rafRef.current) cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
    }

    function updateAutoScroll(pointerClientX: number) {
        const scrollContainer = winRef.current?.closest('.workspace-scroll-area') as HTMLElement;
        if (!scrollContainer) return;

        const r = scrollContainer.getBoundingClientRect();
        const rightDist = r.right - pointerClientX;
        const leftDist = pointerClientX - r.left;

        // Mapowanie prędkości (im bliżej krawędzi, tym szybciej)
        // Clamp 0..1 także wtedy, gdy kursor "wyjedzie" poza kontener (dist < 0),
        // żeby scroll nie zachowywał się skokowo.
        let vx = 0;
        if (rightDist < EDGE) {
            const t = clamp01((EDGE - rightDist) / EDGE);
            vx = t * MAX_SPEED;
        } else if (leftDist < EDGE) {
            const t = clamp01((EDGE - leftDist) / EDGE);
            vx = -t * MAX_SPEED;
        }

        // Nie próbuj scrollować dalej niż się da (eliminuje "dobijanie" do 0/max i skoki)
        const maxScrollLeft = Math.max(0, scrollContainer.scrollWidth - scrollContainer.clientWidth);
        if ((vx > 0 && scrollContainer.scrollLeft >= maxScrollLeft) || (vx < 0 && scrollContainer.scrollLeft <= 0)) {
            vx = 0;
        }

        dragRef.current.scrollVX = vx;

        if (vx !== 0) startRaf();
        else stopRaf();
    }

    const onPointerDown = (e: React.PointerEvent) => {
        if (!isActive) onFocus();
        if (isBlocked || isMaximized) return;

        // ✅ POINTER CAPTURE - trzymamy drag nawet poza oknem
        (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);

        const scrollContainer = winRef.current?.closest('.workspace-scroll-area') as HTMLElement;
        if (!scrollContainer) return;

        const rect = scrollContainer.getBoundingClientRect();
        
        // ✅ POZYCJA WSKAŹNIKA W WORKSPACE (clientX + scrollLeft)
        const pointerX = (e.clientX - rect.left) + scrollContainer.scrollLeft;
        const pointerY = (e.clientY - rect.top) + scrollContainer.scrollTop;

        dragRef.current.active = true;
        dragRef.current.offsetX = pointerX - pos.x;
        dragRef.current.offsetY = pointerY - pos.y;
        dragRef.current.lastPointerX = e.clientX;
        dragRef.current.lastPointerY = e.clientY;

        // RAF uruchamiamy tylko gdy auto-scroll ma działać.
        // Normalne przesuwanie okna robimy w onPointerMove.
        updateAutoScroll(e.clientX);
    };

    const onPointerMove = (e: React.PointerEvent) => {
        if (!dragRef.current.active) return;

        dragRef.current.lastPointerX = e.clientX;
        dragRef.current.lastPointerY = e.clientY;

        // ✅ Normalny drag (bez czekania na RAF) — płynne przesuwanie okna
        const scrollContainer = winRef.current?.closest('.workspace-scroll-area') as HTMLElement;
        if (scrollContainer) {
            const rect = scrollContainer.getBoundingClientRect();
            const pointerXInWorkspace = (e.clientX - rect.left) + scrollContainer.scrollLeft;
            const pointerYInWorkspace = (e.clientY - rect.top) + scrollContainer.scrollTop;

            const newX = pointerXInWorkspace - dragRef.current.offsetX;
            const newY = pointerYInWorkspace - dragRef.current.offsetY;

            setPos({ x: newX, y: newY });
            onLayoutChange?.(
                id,
                { x: newX, y: newY, w: winRef.current?.offsetWidth || 300, h: winRef.current?.offsetHeight || 200 },
                false
            );
        }

        updateAutoScroll(e.clientX);
    };

    const onPointerUp = () => {
        if (dragRef.current.active) {
            onLayoutChange?.(id, { x: pos.x, y: pos.y, w: winRef.current?.offsetWidth || 300, h: winRef.current?.offsetHeight || 200 }, true);
        }
        dragRef.current.active = false;
        dragRef.current.scrollVX = 0;
        stopRaf();
    };

    const toggleMaximize = (e: React.MouseEvent) => {
        e.stopPropagation();
        setIsMaximized(!isMaximized);
        onFocus();
    };

    const winStyle: React.CSSProperties = isMaximized ? {
        position: "absolute", inset: 0, zIndex: zIndex,
        display: "flex", flexDirection: "column", background: "var(--bg-panel)",
        borderRadius: "0", border: "none", boxShadow: "none",
        transition: "all 0.2s ease-out",
        pointerEvents: "auto"
    } : {
        position: "absolute", left: pos.x, top: pos.y, 
        width: size.w, height: size.h, 
        zIndex: zIndex, display: "inline-flex", flexDirection: "column", background: "var(--bg-panel)",
        border: `1px solid ${isActive ? "var(--accent)" : "var(--border)"}`,
        borderRadius: "12px", 
        boxShadow: isActive 
            ? "0 30px 70px rgba(0,0,0,0.5), 0 0 20px rgba(74, 144, 226, 0.05)" 
            : "0 10px 30px rgba(0,0,0,0.3)",
        pointerEvents: "auto",
        minWidth: `${minW}px`, minHeight: `${minH}px`,
        maxWidth: "95vw", maxHeight: "90vh", overflow: "hidden",
        touchAction: "none", // Blokada gestów systemowych
        transition: dragRef.current.active ? "none" : "all 0.2s ease-out"
    };

    return (
        <div ref={winRef} onPointerDown={() => !isActive && onFocus()} style={winStyle} className="window-container">
            {isBlocked && <div style={{ position: "absolute", inset: 0, background: "rgba(0,0,0,0.1)", zIndex: 9999, cursor: "not-allowed" }} />}
            
            <div 
                className="ui-panel-header"
                onPointerDown={onPointerDown}
                onPointerMove={onPointerMove}
                onPointerUp={onPointerUp}
                onPointerCancel={onPointerUp}
                onDoubleClick={toggleMaximize} 
                style={{ 
                    cursor: (isBlocked || isMaximized) ? "default" : "move", 
                    background: isActive ? "var(--bg-soft)" : "rgba(0,0,0,0.2)",
                    borderBottom: `1px solid ${isActive ? "var(--accent)" : "var(--border)"}`,
                    opacity: isActive ? 1 : 0.7
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 10, flex: 1, overflow: "hidden" }}>
                    {!isMaximized && (
                        <button onPointerDown={(e) => e.stopPropagation()} onClick={() => setPos({ x: initialX, y: initialY })} className="btn-win-action" style={{ width: 24, height: 24, opacity: 0.4 }} disabled={isBlocked}>
                            <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><path d="M4 14h6v6M20 10h-6V4M14 10l7-7M10 14l-7 7"/></svg>
                        </button>
                    )}
                    <span className="ui-panel-title" style={{ color: isActive ? "var(--accent)" : "var(--text-muted)" }}>
                        {title.toUpperCase()}
                    </span>
                </div>

                <div style={{ display: "flex", gap: 4 }}>
                    <button onPointerDown={(e) => e.stopPropagation()} onClick={(e) => { e.stopPropagation(); onMinimize(); }} className="btn-win-action"><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><line x1="5" y1="12" x2="19" y2="12"/></svg></button>
                    <button onPointerDown={(e) => e.stopPropagation()} onClick={toggleMaximize} className="btn-win-action">
                        {isMaximized ? (
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><rect x="5" y="5" width="14" height="14" rx="1"/><path d="M9 5V3a1 1 0 0 1 1-1h11a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1h-2"/></svg>
                        ) : (
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><rect x="3" y="3" width="18" height="18" rx="2"/></svg>
                        )}
                    </button>
                    <button onPointerDown={(e) => e.stopPropagation()} onClick={(e) => { e.stopPropagation(); onClose(); }} className="btn-close" style={{ color: isActive ? "var(--danger)" : "inherit" }} disabled={isBlocked}><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button>
                </div>
            </div>

            <div style={{ flex: 1, overflow: "hidden", display: "flex", flexDirection: "column", pointerEvents: "auto" }}>
                <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: "100%", minHeight: "100%" }}>{children}</div>
            </div>
        </div>
    );
}
