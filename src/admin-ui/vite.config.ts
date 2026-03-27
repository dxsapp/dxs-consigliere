import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],

  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },

  // In dev, proxy /api to the ASP.NET backend.
  // Override the target via VITE_API_ORIGIN env variable if needed.
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: process.env.VITE_API_ORIGIN ?? "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },

  build: {
    // Output to wwwroot so ASP.NET static files can serve the bundle.
    outDir: "../Dxs.Consigliere/wwwroot",
    emptyOutDir: true,
  },
});
