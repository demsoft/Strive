/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'url';
import { defineConfig, Plugin } from 'vite';

// The ASP.NET host (UseReactDevelopmentServer) starts `yarn start` and waits for the
// create-react-app startup line before it begins proxying to the dev server.
const announceForAspNetHost = (): Plugin => ({
   name: 'announce-for-aspnet-host',
   configureServer(server) {
      server.httpServer?.once('listening', () => console.log('Starting the development server...'));
   },
});

export default defineConfig({
   plugins: [react(), announceForAspNetHost()],
   resolve: {
      alias: { src: fileURLToPath(new URL('./src', import.meta.url)) },
   },
   server: { port: 3000, strictPort: true },
   build: {
      // ASP.NET serves the SPA from Client/build (see Startup.cs and WebSPA.csproj)
      outDir: 'build',
      sourcemap: false,
      chunkSizeWarningLimit: 4000,
   },
   test: {
      environment: 'jsdom',
      globals: true,
      include: ['src/**/*.test.ts?(x)'],
      setupFiles: ['./src/setupTests.ts'],
   },
});
