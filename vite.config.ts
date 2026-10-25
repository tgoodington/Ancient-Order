import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    // Phaser is lazy-loaded as its own chunk (~1.4 MB minified); the default 500 kB limit would always warn.
    chunkSizeWarningLimit: 1600,
  },
  server: {
    port: 5173,
    open: true,
  },
});
