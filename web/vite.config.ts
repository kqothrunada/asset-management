import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const target = process.env.API_URL ?? "http://localhost:5007";
const proxy = { target, changeOrigin: true };

export default defineConfig({
    plugins: [react()],
    server: { port: 5173, proxy: { "/api": proxy, "/rpc": proxy, "/soap": proxy } },
});