import React, { createContext, useContext, useState, useEffect, useCallback } from "react";

export interface DesktopIcon {
    id: string;
    label: string;
    type: 'module' | 'folder';
    moduleKey?: string;
    icon?: string;
    parentId: string | null;
    gridX: number;
    gridY: number;
}

interface DesktopContextType {
    icons: DesktopIcon[];
    iconScale: number;
    updateScale: (scale: number) => void;
    addIcon: (icon: Omit<DesktopIcon, "id" | "gridX" | "gridY">) => string;
    updateIcons: (updates: {id: string, updates: Partial<DesktopIcon>}[]) => void;
    removeIcons: (ids: string[]) => void;
    findFirstFreeSpot: (parentId: string | null) => { x: number, y: number };
    isSpotOccupied: (x: number, y: number, parentId: string | null, excludeIds?: string[]) => boolean;
    selectedIds: string[];
    setSelectedIds: (ids: string[]) => void;
    activeId: string | null;
    setActiveId: (id: string | null) => void;
    renamingId: string | null;
    setRenamingId: (id: string | null) => void;
    newName: string;
    setNewName: (name: string) => void;
    contextMenu: any;
    setContextMenu: (state: any) => void;
}

const DesktopContext = createContext<DesktopContextType | null>(null);

export const useDesktop = () => {
    const context = useContext(DesktopContext);
    if (!context) throw new Error("useDesktop must be used within a DesktopProvider");
    return context;
};

export const DesktopProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [icons, setIcons] = useState<DesktopIcon[]>(() => {
        const saved = localStorage.getItem("desktop_icons_FINAL_v1");
        if (saved) return JSON.parse(saved);
        
        // ✅ DOMYŚLNE IKONY: Używamy kluczy modułów jako nazw ikon
        return [
            { id: "1", label: "Użytkownicy", type: "module", moduleKey: "admin.users", icon: "admin.users", parentId: null, gridX: 0, gridY: 0 },
            { id: "2", label: "KSeF Faktury", type: "module", moduleKey: "ksef.invoices", icon: "ksef.invoices", parentId: null, gridX: 0, gridY: 1 },
            { id: "3", label: "Moje Dokumenty", type: "folder", icon: "folder", parentId: null, gridX: 0, gridY: 2 }
        ];
    });

    const [iconScale, setIconScale] = useState(() => parseFloat(localStorage.getItem("desktop_icon_scale") || "1.0"));
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [activeId, setActiveId] = useState<string | null>(null);
    const [renamingId, setRenamingId] = useState<string | null>(null);
    const [newName, setNewName] = useState("");
    const [contextMenu, setContextMenu] = useState<any>(null);

    useEffect(() => { localStorage.setItem("desktop_icons_FINAL_v1", JSON.stringify(icons)); }, [icons]);

    const updateScale = useCallback((newScale: number) => {
        const scale = Math.min(Math.max(0.5, 2.0), newScale);
        setIconScale(scale);
        localStorage.setItem("desktop_icon_scale", scale.toString());
        document.documentElement.style.setProperty('--icon-scale', scale.toString());
    }, []);

    const isSpotOccupied = useCallback((x: number, y: number, parentId: string | null, excludeIds: string[] = []) => {
        return icons.some(i => i.gridX === x && i.gridY === y && i.parentId === parentId && !excludeIds.includes(i.id));
    }, [icons]);

    const findFirstFreeSpot = useCallback((parentId: string | null) => {
        const currentIcons = icons.filter(i => i.parentId === parentId);
        for (let x = 0; x < 20; x++) {
            for (let y = 0; y < 8; y++) {
                if (!currentIcons.some(i => i.gridX === x && i.gridY === y)) return { x, y };
            }
        }
        return { x: 0, y: 0 };
    }, [icons]);

    const addIcon = useCallback((iconData: Omit<DesktopIcon, "id" | "gridX" | "gridY">) => {
        const id = Math.random().toString(36).substr(2, 9);
        setIcons(prev => {
            const currentIcons = prev.filter(i => i.parentId === iconData.parentId);
            let spot = { x: 0, y: 0 };
            let found = false;
            for (let x = 0; x < 20 && !found; x++) {
                for (let y = 0; y < 8 && !found; y++) {
                    if (!currentIcons.some(i => i.gridX === x && i.gridY === y)) {
                        spot = { x, y };
                        found = true;
                    }
                }
            }
            return [...prev, { ...iconData, id, gridX: spot.x, gridY: spot.y }];
        });
        return id;
    }, []);

    const updateIcons = useCallback((batch: {id: string, updates: Partial<DesktopIcon>}[]) => {
        setIcons(prev => {
            const newIcons = [...prev];
            batch.forEach(item => {
                const idx = newIcons.findIndex(i => i.id === item.id);
                if (idx !== -1) newIcons[idx] = { ...newIcons[idx], ...item.updates };
            });
            return newIcons;
        });
    }, []);

    const removeIcons = useCallback((idsToRemove: string[]) => {
        setIcons(prev => prev.filter(i => !idsToRemove.includes(i.id)));
    }, []);

    const reorderIcons = useCallback((activeId: string, overId: string) => {
        setIcons(prev => {
            const oldIndex = prev.findIndex(i => i.id === activeId);
            const newIndex = prev.findIndex(i => i.id === overId);
            if (oldIndex !== -1 && newIndex !== -1) {
                const newArray = [...prev];
                const [removed] = newArray.splice(oldIndex, 1);
                newArray.splice(newIndex, 0, removed);
                return newArray;
            }
            return prev;
        });
    }, []);

    return (
        <DesktopContext.Provider value={{ 
            icons, iconScale, updateScale, addIcon, updateIcons, removeIcons, reorderIcons, findFirstFreeSpot, isSpotOccupied,
            selectedIds, setSelectedIds, activeId, setActiveId, renamingId, setRenamingId, newName, setNewName,
            contextMenu, setContextMenu, updateIcon: (id, updates) => updateIcons([{id, updates}])
        }}>
            {children}
        </DesktopContext.Provider>
    );
};
