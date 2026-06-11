import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { viteSingleFile } from 'vite-plugin-singlefile';

// Builds the whole dashboard into one self-contained index.html that gets
// embedded in the NuGet package. No CDN, no separate asset requests.
export default defineConfig({
  plugins: [react(), viteSingleFile()],
  build: {
    outDir: '../src/AspNetDebugDashboard/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      // point at a locally running sample app during development
      '/_debug/api': 'http://localhost:5000',
    },
  },
});
