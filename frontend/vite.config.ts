import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: 'http://localhost:5172', changeOrigin: true },
    },
  },
  build: {
    outDir: '../backend/GoogleHealthLens.Api/wwwroot',
    emptyOutDir: true,
  },
})
