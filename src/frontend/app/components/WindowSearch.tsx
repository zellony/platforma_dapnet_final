import React, { useState, useEffect, useRef, useMemo } from "react";
import { DapnetIcon } from "./DapnetIcon";
import { MENU } from "../menu";
import { useAuth } from "../layout/Shell";

interface WindowSearchProps {
    windows: any[];
    onSelect: (id: string) => void;
    onClose: () => void;
}

export function WindowSearch({ windows, onSelect, onClose }: WindowSearchProps) {
    const { hasPermission } = useAuth();
    const [query, setSearchQuery] = useState("");
    const [selectedIndex, setSelectedIndex] = useState(0);
    const inputRef = useRef<HTMLInputElement>(null);
    const resultsRef = useRef<HTMLDivElement>(null);

    const allAvailableModules = useMemo(() => {
        const list: any[] = [];
        MENU.forEach(group => {
            group.items.forEach(item => {
                if (!item.requiredPermission || hasPermission(item.requiredPermission)) {
                    list.push({
                        id: `module-${item.key}`,
                        key: item.key,
                        title: item.label,
                        type: item.key,
                        isModule: true
                    });
                }
            });
        });
        return list;
    }, [hasPermission]);

    const filteredWindows = windows.filter(w => w.title.toLowerCase().includes(query.toLowerCase()));
    const filteredModules = allAvailableModules.filter(m => 
        m.title.toLowerCase().includes(query.toLowerCase()) && 
        !windows.some(w => w.type === m.key)
    );

    const results = [
        ...filteredWindows.map(w => ({ ...w, section: "OTWARTE OKNA" })),
        ...filteredModules.map(m => ({ ...m, section: "DOSTĘPNE MODUŁY" }))
    ];

    useEffect(() => {
        inputRef.current?.focus();
    }, []);

    useEffect(() => {
        if (resultsRef.current && results.length > 0) {
            const selectedElement = resultsRef.current.querySelector(`[data-index="${selectedIndex}"]`) as HTMLElement;
            if (selectedElement) {
                selectedElement.scrollIntoView({ behavior: "smooth", block: "nearest" });
            }
        }
    }, [selectedIndex, results]);

    const handleKeyDown = (e: React.KeyboardEvent) => {
        // ✅ BLOKUJEMY PROPAGACJĘ DO PULPITU
        if (["ArrowDown", "ArrowUp", "Enter", "Escape"].includes(e.key)) {
            e.stopPropagation();
        }

        if (e.key === "ArrowDown") {
            e.preventDefault();
            setSelectedIndex(prev => (prev + 1) % results.length);
        } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setSelectedIndex(prev => (prev - 1 + results.length) % results.length);
        } else if (e.key === "Enter" && results[selectedIndex]) {
            e.preventDefault();
            const item = results[selectedIndex];
            onSelect(item.isModule ? item.key : item.id);
        } else if (e.key === "Escape") {
            e.preventDefault();
            onClose();
        }
    };

    return (
        <div 
            style={{
                position: "fixed", inset: 0, zIndex: 300000,
                display: "flex", alignItems: "flex-start", justifyContent: "center",
                paddingTop: "15vh", background: "rgba(0,0,0,0.6)", backdropFilter: "blur(4px)",
                animation: "fadeIn 0.2s ease-out"
            }} 
            onMouseDown={(e) => {
                if (e.target === e.currentTarget) onClose();
            }}
            /* ✅ DODATKOWA BLOKADA NA POZIOMIE KONTENERA */
            onKeyDown={(e) => e.stopPropagation()}
        >
            <div 
                style={{ 
                    width: "550px", 
                    background: "var(--bg-panel)", 
                    border: "1px solid var(--border)", 
                    borderRadius: "12px", 
                    boxShadow: "0 20px 50px rgba(0,0,0,0.8)", 
                    overflow: "hidden",
                    animation: "scaleIn 0.2s cubic-bezier(0.175, 0.885, 0.32, 1.275)"
                }}
                onMouseDown={e => e.stopPropagation()}
            >
                <div style={{ padding: "12px 16px", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: 10 }}>
                    <DapnetIcon nameOrSvg="search" size={18} color="var(--text-muted)" />
                    <input 
                        ref={inputRef}
                        value={query}
                        onChange={e => { setSearchQuery(e.target.value); setSelectedIndex(0); }}
                        onKeyDown={handleKeyDown}
                        placeholder="Szukaj okna lub modułu..."
                        style={{ 
                            flex: 1, 
                            background: "transparent", 
                            border: "none", 
                            color: "var(--text-main)", 
                            fontSize: "16px", 
                            outline: "none",
                            fontFamily: "inherit"
                        }}
                    />
                </div>
                <div ref={resultsRef} style={{ maxHeight: "400px", overflowY: "auto", padding: "8px 0" }}>
                    {results.map((item, i) => {
                        const showSectionHeader = i === 0 || results[i-1].section !== item.section;
                        return (
                            <React.Fragment key={item.id}>
                                {showSectionHeader && (
                                    <div style={{ padding: "12px 16px 4px", fontSize: "9px", fontWeight: 800, color: "var(--accent)", opacity: 0.6, letterSpacing: "1px" }}>
                                        {item.section}
                                    </div>
                                )}
                                <div 
                                    data-index={i}
                                    onClick={(e) => { 
                                        e.preventDefault();
                                        e.stopPropagation();
                                        onSelect(item.isModule ? item.key : item.id);
                                    }}
                                    onMouseEnter={() => setSelectedIndex(i)}
                                    style={{
                                        padding: "10px 16px", 
                                        cursor: "pointer", 
                                        display: "flex", 
                                        alignItems: "center", 
                                        gap: 12,
                                        background: i === selectedIndex ? "rgba(74, 144, 226, 0.08)" : "transparent",
                                        borderLeft: i === selectedIndex ? "3px solid var(--accent)" : "3px solid transparent",
                                        transition: "all 0.15s ease"
                                    }}
                                >
                                    <DapnetIcon nameOrSvg={item.type === 'desktop.folder' ? 'folder' : item.type} size={16} color={i === selectedIndex ? "var(--accent)" : "var(--text-muted)"} />
                                    <span style={{ flex: 1, fontSize: "13px", color: i === selectedIndex ? "var(--text-main)" : "var(--text-muted)", fontWeight: i === selectedIndex ? 600 : 400 }}>
                                        {item.title}
                                    </span>
                                    {item.isMinimized && <span style={{ fontSize: "9px", opacity: 0.5, color: "var(--text-muted)", border: "1px solid var(--border)", padding: "2px 6px", borderRadius: "4px" }}>ZMINIMALIZOWANE</span>}
                                    {item.isModule && <span style={{ fontSize: "9px", opacity: 0.4, color: "var(--text-muted)" }}>URUCHOM</span>}
                                </div>
                            </React.Fragment>
                        );
                    })}
                    {results.length === 0 && <div style={{ padding: "20px", textAlign: "center", opacity: 0.5, color: "var(--text-muted)", fontSize: "12px" }}>Nie znaleziono pasujących elementów</div>}
                </div>
            </div>
        </div>
    );
}
