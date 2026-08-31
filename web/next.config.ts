import path from "node:path";
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  turbopack: {
    // Repo root, not web/: src/lib/plans.ts imports the shared plan catalog from
    // ../../../hosted/plans.json (the single source of truth shared with the hosted API),
    // which sits outside web/. Turbopack only resolves modules under `root`.
    root: path.join(__dirname, ".."),
  },
};

export default nextConfig;
