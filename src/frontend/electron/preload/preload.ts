import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electron", {
    getApiBaseUrl: async (): Promise<string> => {
        return await ipcRenderer.invoke("getApiBaseUrl");
    },
    restartBackend: async (): Promise<string> => {
        return await ipcRenderer.invoke("restartBackend");
    },
    getAppVersion: async (): Promise<string> => {
        return await ipcRenderer.invoke("getAppVersion");
    },
    getMachineName: async (): Promise<string> => {
        return await ipcRenderer.invoke("getMachineName");
    },
    getLocalIp: async (): Promise<string> => {
        return await ipcRenderer.invoke("getLocalIp");
    },
    setSessionToken: (token: string | null) => {
        ipcRenderer.send("set-session-token", token);
    },
    quitApp: async (): Promise<void> => {
        await ipcRenderer.invoke("quitApp");
    },
    safeStorage: {
        encrypt: (text: string) => ipcRenderer.invoke("safe-storage-encrypt", text),
        decrypt: (encrypted: string) => ipcRenderer.invoke("safe-storage-decrypt", encrypted)
    },
    enableDevTools: () => ipcRenderer.send("enable-devtools"),
    disableDevTools: () => ipcRenderer.send("disable-devtools"),
    saveDiagnosticLogs: (content: string) => ipcRenderer.invoke("save-diagnostic-logs", content),
    getStartupContext: async (): Promise<{ isDbServiceMode: boolean; isWindowsAdmin: boolean; isPackaged: boolean; }> => {
        return await ipcRenderer.invoke("getStartupContext");
    },
    launchDbServiceModeAsAdmin: async (): Promise<boolean> => {
        return await ipcRenderer.invoke("launchDbServiceModeAsAdmin");
    },
    getSystemVersions: () => ({
        node: process.versions.node,
        electron: process.versions.electron,
        arch: process.arch,
        platform: process.platform
    })
});
