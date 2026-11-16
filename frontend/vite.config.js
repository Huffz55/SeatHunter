import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: '/',
  build: {
    outDir: '../src/SeatHunter.Api/wwwroot',
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5179', changeOrigin: true },
      '/notificationHub': { target: 'http://localhost:5179', ws: true },
      '/adminHub': { target: 'http://localhost:5179', ws: true }
    }
  }
});
