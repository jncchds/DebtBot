import { readFileSync } from 'node:fs'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vitest/config'

// The API, run with `dotnet run` (launchSettings "http" profile) or docker compose.
const backend = process.env.DEBTBOT_BACKEND ?? 'http://localhost:5098'
const { version } = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')) as { version: string }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  define: {
    __APP_VERSION__: JSON.stringify(version),
  },
  build: {
    // The API serves the UI from wwwroot (gitignored; the Dockerfile's node stage fills it).
    outDir: '../wwwroot',
    emptyOutDir: true,
    sourcemap: false,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': backend,
      '/swagger': backend,
    },
  },
  test: {
    environment: 'node',
  },
})
