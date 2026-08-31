import path from "node:path";
import { defineConfig } from "vitest/config";

const dir = import.meta.dirname;

// add-feature-flag-seam: first unit-test setup for web/. Kept minimal — no vite React plugin
// (peer-conflicts with next 16's toolchain); the default JSX runtime covers the few .tsx tests.
// `server-only` is stubbed so server modules can be imported under test.
export default defineConfig({
  resolve: {
    alias: {
      "@": path.join(dir, "src"),
      "server-only": path.join(dir, "src/test/stubs/server-only.ts"),
    },
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    globals: true,
  },
});
