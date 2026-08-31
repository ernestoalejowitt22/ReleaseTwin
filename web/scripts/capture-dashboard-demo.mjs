// SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
// SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
//
// landing-demo-ci-loop: copies the dashboard screenshots produced by
// cypress/e2e/capture-landing-demo.cy.ts into web/public/demo/ for the marketing landing
// page. Run after `npm run capture:dashboard:run` (or as part of `npm run capture:dashboard`).

import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SHOTS = join(HERE, "..", "cypress", "screenshots", "capture-landing-demo.cy.ts");
const OUT = join(HERE, "..", "public", "demo");

const MAP = {
  "runs.png": "dashboard-runs.png",
  "evidence.png": "dashboard-evidence.png",
};

mkdirSync(OUT, { recursive: true });
let copied = 0;
for (const [src, dest] of Object.entries(MAP)) {
  const from = join(SHOTS, src);
  if (!existsSync(from)) {
    console.error(`missing ${from} — did the capture spec run?`);
    continue;
  }
  copyFileSync(from, join(OUT, dest));
  console.log(`wrote ${join(OUT, dest)}`);
  copied += 1;
}
if (copied !== Object.keys(MAP).length) process.exit(1);
