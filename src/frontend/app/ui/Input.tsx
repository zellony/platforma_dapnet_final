import React from "react";

export type InputProps = React.InputHTMLAttributes<HTMLInputElement> & {
    label?: string;
    error?: string;
    hint?: string;
    fullWidth?: boolean;
};

function cx(...parts: Array<string | false | null | undefined>) {
    return parts.filter(Boolean).join(" ");
}

export function Input({ label, error, hint, fullWidth = true, className, ...props }: InputProps) {
    return (
        <label className={cx("ui-field", fullWidth && "ui-field--full")}>
            {label ? <div className="ui-field__label">{label}</div> : null}
            <input
                {...props}
                className={cx("ui-input", error && "ui-input--error", className)}
            />
            {error ? (
                <div className="ui-field__error">{error}</div>
            ) : hint ? (
                <div className="ui-field__hint">{hint}</div>
            ) : null}
        </label>
    );
}