/**
 * CENTRALNY REJESTR IKON PLATFORMY DAPNET
 * Zarządza ikonami systemowymi oraz ikonami dynamicznymi z modułów DLL.
 */

// Typ dla definicji ikony (może to być nazwa systemowa lub surowy SVG)
export type IconDefinition = string;

// 1. IKONY SYSTEMOWE (Wbudowane w kod)
const SYSTEM_ICONS: Record<string, string> = {
    "users": "users",
    "admin.users": "users",
    "lock": "lock",
    "admin.roles": "lock",
    "key": "key",
    "receipt": "receipt",
    "ksef": "receipt",
    "file-text": "file-text",
    "ksef.invoices": "file-text",
    "settings": "settings",
    "admin": "settings",
    "briefcase": "briefcase",
    "admin.company": "briefcase",
    "folder": "folder",
    "desktop.folder": "folder",
    "info": "info",
    "admin.system": "info",
    "star": "star",
    "power": "power",
    "menu": "menu",
    "chevron-left": "chevron-left",
    "chevron-right": "chevron-right",
    "grip": "grip",
    "plus": "plus",
    "award": "award",
    "admin.license": "award",
    "shield-check": "shield-check",
    "ksef.auth": "shield-check",
    "trash": "trash",
    "layout": "layout",
    "admin.db-sessions": "admin.db-sessions",
    "dev.logger": "dev.logger",
    "dev.emulator": "dev.emulator"
};

// 2. IKONY DYNAMICZNE (Ładowane z DLL przez API)
const dynamicIcons: Record<string, string> = {};

/**
 * Rejestruje nową ikonę (np. z modułu DLL)
 */
export function registerIcon(name: string, svgOrName: string) {
    dynamicIcons[name] = svgOrName;
    // Opcjonalnie: wysłanie zdarzenia o aktualizacji rejestru
    window.dispatchEvent(new CustomEvent("icon-registry-updated", { detail: { name } }));
}

/**
 * Pobiera definicję ikony dla danego typu/klucza
 */
export function getIconDefinition(name: string): string {
    // Najpierw sprawdzamy ikony dynamiczne (DLL mają priorytet)
    if (dynamicIcons[name]) return dynamicIcons[name];
    
    // Potem ikony systemowe
    if (SYSTEM_ICONS[name]) return SYSTEM_ICONS[name];
    
    // Jeśli nie znaleziono, zwracamy samą nazwę (może to być surowy SVG przekazany bezpośrednio)
    return name;
}

/**
 * Zwraca ikonę dla modułu (główny punkt styku dla Sidebar/Desktop/Taskbar)
 */
export function getModuleIcon(moduleKey: string): string {
    return getIconDefinition(moduleKey);
}
