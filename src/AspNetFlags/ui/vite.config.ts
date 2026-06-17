import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { viteSingleFile } from 'vite-plugin-singlefile';

// One self-contained index.html embedded into the package. No CDN.
export default defineConfig({
  plugins: [react(), viteSingleFile()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: { '/_flags/api': 'http://localhost:5000' },
  },
});
