import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The dev server proxies every routed path to the facade on port 5206, so the browser sees the real X-Served-By header.
const facade = 'http://localhost:5206';
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/customers': facade, '/schedules': facade, '/arrears': facade, '/bridge': facade },
  },
});
