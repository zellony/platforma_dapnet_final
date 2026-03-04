export { };

declare global {
    interface Window {
        api: {
            getApiBaseUrl: () => Promise<string>;
            restartBackend: () => Promise<string>;
            getAppVersion: () => Promise<string>;
            quitApp: () => Promise<void>;
            onLogout: (cb: () => void) => void;
        };
    }
}