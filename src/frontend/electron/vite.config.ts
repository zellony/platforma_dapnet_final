import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import * as path from "path";

const electronNodeModules = path.resolve(__dirname, "node_modules");

export default defineConfig({
    root: "../app",
    plugins: [react()],
    base: "./",
    resolve: {
        alias: {
            react: path.resolve(electronNodeModules, "react"),
            "react/jsx-runtime": path.resolve(electronNodeModules, "react/jsx-runtime.js"),
            "react/jsx-dev-runtime": path.resolve(electronNodeModules, "react/jsx-dev-runtime.js"),
            "react-dom": path.resolve(electronNodeModules, "react-dom"),
            "@dnd-kit/core": path.resolve(electronNodeModules, "@dnd-kit/core"),
            "@dnd-kit/sortable": path.resolve(electronNodeModules, "@dnd-kit/sortable"),
            "@dnd-kit/utilities": path.resolve(electronNodeModules, "@dnd-kit/utilities")
        }
    },
    build: {
        outDir: "../electron/dist/renderer",
        emptyOutDir: true
    },
    server: {
        host: "127.0.0.1",
        port: 5173,
        strictPort: true
    }
});
