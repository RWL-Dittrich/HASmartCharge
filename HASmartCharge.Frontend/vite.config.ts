import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: parseInt(process.env.VITE_PORT ?? '5173'),
    proxy: {
      '/api': {
        target: process.env.VITE_BACKEND_URL ?? 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
