import React, { useState, useEffect } from "react";
import { apiFetch } from "../apiFetch";

export function DebugHud() {
    const [isVisible, setIsVisible] = useState(() => localStorage.getItem("dev_show_hud") === "true");
    const [stats, setStats] = useState({ ram: "0 MB", ping: "0ms", windows: 0 });

    useEffect(() => {
        const handleToggle = (e: any) => setIsVisible(e.detail);
        window.addEventListener("dev-hud-changed", handleToggle);
        return () => window.removeEventListener("dev-hud-changed", handleToggle);
    }, []);

    useEffect(() => {
        if (!isVisible) return;

        const interval = setInterval(async () => {
            const start = Date.now();
            let ping = "err";
            try {
                const res = await apiFetch("/system/status");
                if (res.ok) ping = `${Date.now() - start}ms`;
            } catch { ping = "timeout"; }

            // W Electronie możemy pobrać info o procesie przez IPC, ale na razie zrobimy mock RAM
            // Docelowo dodamy tu wywołanie window.electron.getProcessStats()
            const ram = `${(Math.random() * 50 + 120).toFixed(0)} MB`; 
            
            const winCount = document.querySelectorAll('.window-container').length;

            setStats({ ram, ping, windows: winCount });
        }, 2000);

        return () => clearInterval(interval);
    }, [isVisible]);

    if (!isVisible) return null;

    const itemStyle: React.CSSProperties = { display: "flex", flexDirection: "column", gap: "2px" };
    const labelStyle: React.CSSProperties = { fontSize: "8px", fontWeight: 700, color: "rgba(255,255,255,0.3)", letterSpacing: "0.5px" };
    const valueStyle: React.CSSProperties = { fontSize: "11px", fontWeight: 600, color: "var(--accent)", fontFamily: "'JetBrains Mono', monospace" };

    return (
        <div style={{ 
            position: "fixed", top: "50px", left: "50%", transform: "translateX(-50%)",
            background: "rgba(10, 15, 24, 0.8)", border: "1px solid var(--border)",
            padding: "6px 20px", borderRadius: "20px", zIndex: 999999,
            display: "flex", gap: "24px", backdropFilter: "blur(10px)",
            boxShadow: "0 4px 20px rgba(0,0,0,0.4)", pointerEvents: "none"
        }}>
            <div style={itemStyle}>
                <span style={labelStyle}>PROCESS RAM</span>
                <span style={valueStyle}>{stats.ram}</span>
            </div>
            <div style={{ width: "1px", background: "rgba(255,255,255,0.1)", height: "20px", alignSelf: "center" }} />
            <div style={itemStyle}>
                <span style={labelStyle}>API LATENCY</span>
                <span style={valueStyle}>{stats.ping}</span>
            </div>
            <div style={{ width: "1px", background: "rgba(255,255,255,0.1)", height: "20px", alignSelf: "center" }} />
            <div style={itemStyle}>
                <span style={labelStyle}>ACTIVE WINDOWS</span>
                <span style={valueStyle}>{stats.windows}</span>
            </div>
        </div>
    );
}
