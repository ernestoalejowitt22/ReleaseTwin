import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

import { BITBUCKET_PIPELINES_LABEL, BITBUCKET_PIPELINES_SNIPPET } from "./ci-snippets";

// landing-demo-why-and-portability: the Bitbucket Pipelines snippet is rendered on both
// /docs/ci and the landing page's CI-loop demo. It must be one shared constant so the two
// surfaces cannot drift.
describe("ci-snippets", () => {
  const src = (p: string) => readFileSync(join(import.meta.dirname, "..", p), "utf8");

  it("the snippet is a real Pipelines PR gate that writes --summary-json", () => {
    expect(BITBUCKET_PIPELINES_LABEL).toBe("bitbucket-pipelines.yml");
    expect(BITBUCKET_PIPELINES_SNIPPET).toContain("pull-requests:");
    expect(BITBUCKET_PIPELINES_SNIPPET).toContain("--summary-json");
  });

  it("both the docs CI page and the landing page use the shared constant", () => {
    for (const p of ["app/(marketing)/docs/ci/page.tsx", "app/(marketing)/page.tsx"]) {
      expect(src(p)).toContain('from "@/lib/ci-snippets"');
      expect(src(p)).toContain("BITBUCKET_PIPELINES_SNIPPET");
    }
  });

  it("neither page inlines its own copy of the pipelines YAML", () => {
    for (const p of ["app/(marketing)/docs/ci/page.tsx", "app/(marketing)/page.tsx"]) {
      expect(src(p)).not.toContain("BITBUCKET_CLONE_DIR");
    }
  });
});
