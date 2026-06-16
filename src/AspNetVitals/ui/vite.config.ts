import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { viteSingleFile } from 'vite-plugin-singlefile';

export default defineConfig({
  plugins: [react(), viteSingleFile()],
  build: { outDir: '../wwwroot', emptyOutDir: true },
  server: { proxy: { '/_vitals/api': 'http://localhost:5000' } },
});
