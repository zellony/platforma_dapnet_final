import React, { useState } from "react";

interface TooltipProps {
    text: string;
    children: React.ReactNode;
}

export function Tooltip({ text, children }: TooltipProps) {
    const [isVisible, setIsVisible] = useState(false);
    const [coords, setCoords] = useState({ x: 0, y: 0 });

    const handleMouseEnter = (e: React.MouseEvent) => {
        const rect = e.currentTarget.getBoundingClientRect();
        setCoords({
            x: rect.left + rect.width / 2,
            y: rect.top - 8 // 8px nad elementem
        });
        setIsVisible(true);
    };

    return (
        <>
            <div 
                onMouseEnter={handleMouseEnter} 
                onMouseLeave={() => setIsVisible(false)}
                style={{ display: "inline-flex" }}
            >
                {children}
            </div>
            {isVisible && (
                <div style={{
                    position: "fixed",
                    top: coords.y,
                    left: coords.x,
                    transform: "translate(-50%, -100%)",
                    background: "rgba(30, 41, 59, 0.95)", // Ciemny granat/szary
                    color: "rgba(255, 255, 255, 0.7)", // Bardziej stonowana biel
                    padding: "6px 10px",
                    borderRadius: "6px",
                    fontSize: "11px",
                    fontWeight: 500,
                    pointerEvents: "none",
                    whiteSpace: "nowrap",
                    zIndex: 10000,
                    border: "1px solid rgba(255, 255, 255, 0.1)",
                    boxShadow: "0 4px 15px rgba(0,0,0,0.4)",
                    backdropFilter: "blur(4px)"
                }}>
                    {text}
                    {/* Strzałka w dół */}
                    <div style={{
                        position: "absolute",
                        bottom: "-5px",
                        left: "50%",
                        transform: "translateX(-50%)",
                        width: 0,
                        height: 0,
                        borderLeft: "5px solid transparent",
                        borderRight: "5px solid transparent",
                        borderTop: "5px solid rgba(30, 41, 59, 0.95)" // Dopasowane do tła
                    }} />
                </div>
            )}
        </>
    );
}
