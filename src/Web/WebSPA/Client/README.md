# Strive frontend

React + TypeScript single page app, built with [Vite](https://vitejs.dev/). Requires Node 20.19+ (the Docker build uses Node 22).

The page loads `/config.js`, which is generated at runtime by the ASP.NET host (`src/Web/WebSPA`). For that reason, run the app through the host (`dotnet run` in `src/Web/WebSPA`); it starts `yarn start` for you and proxies to it. Running `yarn start` alone serves the UI without the injected configuration.

## Scripts

| Script | Description |
| --- | --- |
| `yarn start` | Vite dev server on port 3000 with hot module replacement |
| `yarn build` | Production build to `build/` (served by the ASP.NET host) |
| `yarn typecheck` | `tsc --noEmit` (Vite does not type check) |
| `yarn test` | Run the Vitest tests once (`yarn test:watch` to watch) |
| `yarn preview` | Serve the production build locally |
