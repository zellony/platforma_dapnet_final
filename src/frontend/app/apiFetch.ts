let apiBaseUrl = "";
let onActivity: (() => void) | null = null;

export interface ApiLogEntry {
    id: string;
    timestamp: string;
    method: string;
    path: string;
    status?: number;
    duration?: number;
    requestBody?: any;
    responseBody?: any;
}
const apiLogs: ApiLogEntry[] = [];
export const getApiLogs = () => [...apiLogs];
export const clearApiLogs = () => {
    apiLogs.splice(0, apiLogs.length);
    window.dispatchEvent(new CustomEvent("api-log-cleared"));
};

const SENSITIVE_KEY = /(password|pass|pwd|token|jwt|session|cookie|authorization|auth|secret|apikey|api_key|rsa|private|refresh|access|login|username|email)/i;
const JWT_REGEX = /eyJ[A-Za-z0-9_-]+?\.[A-Za-z0-9_-]+?\.[A-Za-z0-9_-]+/g;
const BEARER_REGEX = /Bearer\s+[A-Za-z0-9\-_.=]+/gi;
const COOKIE_REGEX = /dapnet_session=[^; ]+/gi;

function sanitizeText(value: string) {
    return value
        .replace(BEARER_REGEX, "Bearer [REDACTED]")
        .replace(COOKIE_REGEX, "dapnet_session=[REDACTED]")
        .replace(JWT_REGEX, "[REDACTED_JWT]");
}

function sanitizeValue(value: any): any {
    if (value === null || value === undefined) return value;
    if (typeof value === "string") return sanitizeText(value);
    if (typeof value !== "object") return value;
    if (Array.isArray(value)) return value.map(sanitizeValue);

    const out: Record<string, any> = {};
    for (const [key, val] of Object.entries(value)) {
        if (SENSITIVE_KEY.test(key)) out[key] = "[REDACTED]";
        else out[key] = sanitizeValue(val);
    }
    return out;
}

export function setApiBaseUrl(url: string) { apiBaseUrl = url; }

export function setAuthToken(token: string) { 
    window.dispatchEvent(new CustomEvent("auth-changed"));
}

export async function setStoredLogin(login: string) {
    if (!login) {
        localStorage.removeItem("auth_user_login_encrypted");
        return;
    }
    const encrypted = await (window as any).electron.safeStorage.encrypt(login);
    localStorage.setItem("auth_user_login_encrypted", encrypted);
}

export async function getStoredLogin() {
    const encrypted = localStorage.getItem("auth_user_login_encrypted");
    if (!encrypted) return "";
    return await (window as any).electron.safeStorage.decrypt(encrypted);
}

export function clearAuthToken() { 
    sessionStorage.removeItem("user_info");
    window.dispatchEvent(new CustomEvent("auth-changed"));
}

export function setOnActivity(cb: () => void) { onActivity = cb; }

export async function encryptPassword(password: string): Promise<string> {
    let publicKey = "";
    try {
        const res = await fetch(`${apiBaseUrl}/system/rsa-key`);
        if (!res.ok) {
            throw new Error("BACKEND_UNAVAILABLE");
        }
        const payload = await res.json();
        publicKey = payload?.publicKey ?? "";
        if (!publicKey) {
            throw new Error("BACKEND_INVALID_KEY");
        }
    } catch (e) {
        console.error("RSA key fetch failed", e);
        throw new Error("Brak polaczenia z backendem. Sprobuj ponownie.");
    }

    try {
        const binaryDerString = window.atob(publicKey);
        const binaryDer = new Uint8Array(binaryDerString.length);
        for (let i = 0; i < binaryDerString.length; i++) {
            binaryDer[i] = binaryDerString.charCodeAt(i);
        }

        const cryptoKey = await window.crypto.subtle.importKey(
            "spki",
            binaryDer,
            { name: "RSA-OAEP", hash: "SHA-256" },
            true,
            ["encrypt"]
        );

        const encrypted = await window.crypto.subtle.encrypt(
            { name: "RSA-OAEP" },
            cryptoKey,
            new TextEncoder().encode(password)
        );

        return window.btoa(String.fromCharCode(...new Uint8Array(encrypted)));
    } catch (e) {
        console.error("RSA encryption failed", e);
        throw new Error("Blad szyfrowania danych logowania. Sprobuj ponownie.");
    }
}

export function isReadOnlyMode() {
    const userInfo = JSON.parse(sessionStorage.getItem("user_info") || "{}");
    return userInfo.is_read_only === true;
}

export async function safeJson(res: Response) {
    try {
        const text = await res.text();
        return text ? JSON.parse(text) : {};
    } catch {
        return null; // Zwracamy null zamiast pustego obiektu przy bĹ‚Ä™dzie parsowania
    }
}

export async function getErrorMessage(res: Response): Promise<string> {
    try {
        // Klonujemy odpowiedĹş, bo body moĹĽna odczytaÄ‡ tylko raz
        const resClone = res.clone();
        const text = await resClone.text();
        
        try {
            // PrĂłbujemy sparsowaÄ‡ jako JSON
            const data = JSON.parse(text);
            return data.message || data.error || text || "WystÄ…piĹ‚ nieoczekiwany bĹ‚Ä…d.";
        } catch {
            // JeĹ›li to nie JSON, zwracamy surowy tekst (o ile nie jest pusty)
            if (text && text.length < 200) return text;
        }

        // Standardowe fallbacki dla kodĂłw HTTP
        if (res.status === 403) return "Brak uprawnieĹ„ do wykonania tej operacji (Tryb tylko do odczytu).";
        if (res.status === 401) return "Sesja wygasĹ‚a. Zaloguj siÄ™ ponownie.";
        if (res.status === 400) return "NieprawidĹ‚owe ĹĽÄ…danie.";
        
        return `BĹ‚Ä…d serwera (${res.status})`;
    } catch {
        return "WystÄ…piĹ‚ nieoczekiwany bĹ‚Ä…d komunikacji.";
    }
}

export async function apiFetch(path: string, options: RequestInit = {}) {
    if (onActivity) onActivity();
    const url = `${apiBaseUrl}${path}`;
    const startTime = Date.now();
    const logId = Math.random().toString(36).substr(2, 9);

    const headers: Record<string, string> = {
        "Content-Type": "application/json",
        ...((options.headers as Record<string, string>) || {})
    };
    const contentType = headers["Content-Type"] || headers["content-type"] || "";

    const isStartupQuery = path === "/system/status" || path === "/auth/me" || path === "/company/status";
    const isAuthQuery = path.startsWith("/auth");

    if (!isStartupQuery) {
        let parsedRequestBody: any = null;
        if (options.body !== undefined && options.body !== null) {
            if (typeof options.body === "string") {
                if (contentType.includes("application/json")) {
                    try { parsedRequestBody = JSON.parse(options.body); }
                    catch { parsedRequestBody = "[Invalid JSON]"; }
                } else {
                    parsedRequestBody = options.body.length > 2000 ? `${options.body.slice(0, 2000)}â€¦(truncated)` : options.body;
                }
            } else if (options.body instanceof FormData) {
                parsedRequestBody = "[FormData]";
            } else if (options.body instanceof URLSearchParams) {
                parsedRequestBody = options.body.toString();
            } else {
                parsedRequestBody = "[Non-JSON Body]";
            }
        }
        const logEntry: ApiLogEntry = {
            id: logId,
            timestamp: new Date().toLocaleTimeString(),
            method: options.method || 'GET',
            path,
            requestBody: isAuthQuery ? "[REDACTED]" : sanitizeValue(parsedRequestBody)
        };
        apiLogs.unshift(logEntry);
        if (apiLogs.length > 50) apiLogs.pop();
        window.dispatchEvent(new CustomEvent("api-log-added"));
    }

    const res = await fetch(url, { 
        ...options, 
        headers,
        credentials: 'include' 
    });

    if (!isStartupQuery) {
        const duration = Date.now() - startTime;
        const entry = apiLogs.find(l => l.id === logId);
        if (entry) {
            entry.status = res.status;
            entry.duration = duration;
            res.clone().text().then(text => {
                const resContentType = res.headers.get("content-type") || "";
                if (!text) { entry.responseBody = text; window.dispatchEvent(new CustomEvent("api-log-updated")); return; }
                if (resContentType.includes("application/json")) {
                    try {
                        const parsed = JSON.parse(text);
                        entry.responseBody = isAuthQuery ? "[REDACTED]" : sanitizeValue(parsed);
                    } catch { entry.responseBody = "[Invalid JSON]"; }
                } else {
                    const raw = text.length > 2000 ? `${text.slice(0, 2000)}â€¦(truncated)` : text;
                    entry.responseBody = isAuthQuery ? "[REDACTED]" : sanitizeText(raw);
                }
                window.dispatchEvent(new CustomEvent("api-log-updated"));
            }).catch(() => {});
        }
    }

    if (res.status === 401 && !path.includes("/auth/login") && !path.includes("/system/status")) {
        clearAuthToken();
        window.location.reload();
    }

    return res;
}

