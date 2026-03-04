import React, { useState, useEffect } from "react";
import iconImg from "../assets/icon.png";
import "./TopBar.css";

interface TopBarProps {
    user: any;
    topRight?: React.ReactNode;
    readOnlyBanner?: React.ReactNode;
}

const HudItem = ({ children, label, center = false }: { children: React.ReactNode, label?: string, center?: boolean }) => (
    <div className="hud-item">
        <div className="hud-bracket hud-bracket--tl"></div>
        <div className="hud-bracket hud-bracket--tr"></div>
        <div className="hud-bracket hud-bracket--bl"></div>
        <div className="hud-bracket hud-bracket--br"></div>
        {label && <div style={{ fontSize: "8px", fontWeight: 700, color: "rgba(143, 160, 178, 0.4)", marginBottom: 2, letterSpacing: "0.5px", textAlign: center ? "center" : "left" }}>{label}</div>}
        {children}
    </div>
);

const SystemClock = () => {
    const [now, setNow] = useState(new Date());
    useEffect(() => {
        const timer = setInterval(() => setNow(new Date()), 1000);
        return () => clearInterval(timer);
    }, []);

    const formatTime = (date: Date) => date.toLocaleTimeString("pl-PL", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
    const formatDate = (date: Date) => {
        const dayName = date.toLocaleDateString("pl-PL", { weekday: 'short' });
        const formattedDate = date.toLocaleDateString("pl-PL", { day: "2-digit", month: "2-digit", year: "numeric" });
        return `${dayName.toUpperCase()}, ${formattedDate}`;
    };

    return (
        <>
            <HudItem label="CZAS SYSTEMOWY" center={true}>
                <div style={{ fontSize: "14px", color: "var(--text-main)", fontWeight: 600, fontFamily: "'JetBrains Mono', monospace", opacity: 0.8, textAlign: "center", minWidth: "80px" }}>
                    {formatTime(now)}
                </div>
            </HudItem>
            <HudItem label="DATA">
                <div style={{ fontSize: "10px", color: "var(--text-main)", fontWeight: 600, letterSpacing: "0.5px", opacity: 0.8 }}>
                    {formatDate(now)}
                </div>
            </HudItem>
        </>
    );
};

export function TopBar({ user, topRight, readOnlyBanner }: TopBarProps) {
    const isDev = user?.isSystemAdmin || user?.login?.toLowerCase() === "admindapnet";

    return (
        <div className="topbar-container">
            <div className="topbar-brand">
                <img src={iconImg} alt="DAPNET" style={{ width: "38px", height: "38px", opacity: 0.7 }} />
                <div className="topbar-brand-text">
                    <div className="topbar-brand-sub">Platforma</div>
                    <div className="topbar-brand-main">DAPNET</div>
                </div>
            </div>

            <div className="topbar-separator"></div>

            <div className="topbar-hud">
                <SystemClock />
            </div>

            {readOnlyBanner}

            <div className="topbar-right">
                {isDev && (
                    <div className="dev-tools-icon" onClick={() => window.dispatchEvent(new CustomEvent("open-devtools"))}>
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--accent)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.7a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.7z"/>
                        </svg>
                    </div>
                )}
                {topRight}
            </div>
        </div>
    );
}
