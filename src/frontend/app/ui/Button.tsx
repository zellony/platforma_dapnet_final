import React from "react";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
type ButtonSize = "sm" | "md" | "lg";

export type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: ButtonVariant;
    size?: ButtonSize;
    fullWidth?: boolean;
};

function cx(...parts: Array<string | false | null | undefined>) {
    return parts.filter(Boolean).join(" ");
}

export function Button({
    variant = "primary",
    size = "md",
    fullWidth = false,
    className,
    ...props
}: ButtonProps) {
    return (
        <button
            {...props}
            className={cx(
                "ui-btn",
                `ui-btn--${variant}`,
                `ui-btn--${size}`,
                fullWidth && "ui-btn--full",
                className
            )}
        />
    );
}