import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',  // listen on all interfaces
    port: 5173,
    strictPort: true,
    watch: {
      usePolling: true,      // required on older macOS & Docker
      interval: 100,         // tune if CPU high
    },
    hmr: {
      protocol: 'ws',
      host: 'localhost',
      port: 5173,
    }
  }
})
