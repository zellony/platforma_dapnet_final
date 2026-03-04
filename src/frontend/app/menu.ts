/**
 * DEFINICJA MENU GŁÓWNEGO PLATFORMY DAPNET
 * Ikony są rozwiązywane przez IconRegistry na podstawie klucza (key) lub nazwy ikony.
 */

export interface MenuItem {
    key: string;
    label: string;
    icon: string;
    requiredPermission?: string;
}

export interface MenuGroup {
    id: string;
    label: string;
    icon: string;
    items: MenuItem[];
}

export const MENU: MenuGroup[] = [
    {
        id: "admin",
        label: "ADMINISTRACJA",
        icon: "admin", // Mapowane na 'settings' w IconRegistry
        items: [
            { key: "admin.users", label: "Użytkownicy", icon: "admin.users", requiredPermission: "platform.users.read" },
            { key: "admin.roles", label: "Role i uprawnienia", icon: "admin.roles", requiredPermission: "platform.admin" },
            { key: "admin.company", label: "Dane firmy", icon: "admin.company", requiredPermission: "platform.admin" },
            { key: "admin.license", label: "Licencja", icon: "admin.license", requiredPermission: "platform.admin" }
        ]
    },
    {
        id: "ksef",
        label: "KSeF",
        icon: "ksef", // Mapowane na 'receipt' w IconRegistry
        items: [
            { key: "ksef.auth", label: "Autoryzacja KSeF", icon: "ksef.auth", requiredPermission: "ksef.view" },
            { key: "ksef.invoices", label: "Lista faktur", icon: "ksef.invoices", requiredPermission: "ksef.view" }
        ]
    }
];

export const SYSTEM_INFO_ITEM = { 
    key: "admin.system", 
    label: "Informacje o systemie", 
    icon: "admin.system" // Mapowane na 'info' w IconRegistry
};
