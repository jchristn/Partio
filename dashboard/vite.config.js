import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{js,jsx}']
  },
  server: {
    port: 8401,
    proxy: {
      '/v1.0': {
        target: 'http://localhost:8400',
        changeOrigin: true
      }
    }
  }
});
