import React from "react";

export type PanelProps = {
    title?: string;
    right?: React.ReactNode;
    children: React.ReactNode;
    className?: string;
};

function cx(...parts: Array<string | false | null | undefined>) {
    return parts.filter(Boolean).join(" ");
}

export function Panel({ title, right, children, className }: PanelProps) {
    return (
        <div className={cx("ui-panel", className)}>
            {(title || right) ? (
                <div className="ui-panel__header">
                    <div className="ui-panel__title">{title}</div>
                    <div className="ui-panel__right">{right}</div>
                </div>
            ) : null}
            <div className="ui-panel__body">{children}</div>
        </div>
    );
}