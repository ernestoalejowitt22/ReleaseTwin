import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@clerk/nextjs/server", () => ({
  auth: async () => ({ userId: null, orgId: null, sessionClaims: null }),
}));

import { FLAG_KEYS, FLAG_REGISTRY, WEB_FLAGS, flagDefinition } from "./flags-registry";

describe("flags-registry", () => {
  it("loads and validates flags.json", () => {
    expect(FLAG_REGISTRY.length).toBeGreaterThan(0);
    const smoke = flagDefinition("flag-seam-smoke");
    expect(smoke.type).toBe("boolean");
    expect(smoke.default).toBe(true);
    expect(smoke.surfaces).toContain("web");
  });

  it("FLAG_KEYS is in parity with flags.json (asserted at module load)", () => {
    expect([...FLAG_KEYS].sort()).toEqual(FLAG_REGISTRY.map((f) => f.key).sort());
  });

  it("every default matches its declared type", () => {
    for (const f of FLAG_REGISTRY) {
      const t = typeof f.default;
      expect(t === f.type || (f.type === "object" && t === "object")).toBe(true);
    }
  });

  it("WEB_FLAGS only contains web-surface flags", () => {
    expect(WEB_FLAGS.every((f) => f.surfaces.includes("web"))).toBe(true);
  });
});

describe("getFlag (server, fail-open)", () => {
  const OLD_ENV = process.env;
  beforeEach(() => {
    vi.resetModules();
    process.env = { ...OLD_ENV };
  });
  afterEach(() => {
    process.env = OLD_ENV;
    vi.restoreAllMocks();
  });

  it("returns the registry default", async () => {
    const { getFlag } = await import("./flags");
    expect(await getFlag("flag-seam-smoke")).toBe(true);
  });

  it("honours a FLAG_<KEY> env override", async () => {
    process.env.FLAG_FLAG_SEAM_SMOKE = "false";
    const { getFlag } = await import("./flags");
    expect(await getFlag("flag-seam-smoke")).toBe(false);
  });

  it("ignores an unparseable env override and keeps the default", async () => {
    process.env.FLAG_FLAG_SEAM_SMOKE = "not-a-bool";
    const { getFlag } = await import("./flags");
    expect(await getFlag("flag-seam-smoke")).toBe(true);
  });

  it("fails open to the default when the provider throws", async () => {
    vi.doMock("@openfeature/server-sdk", async (importOriginal) => {
      const actual = await importOriginal<typeof import("@openfeature/server-sdk")>();
      return {
        ...actual,
        OpenFeature: {
          setProviderAndWait: async () => {},
          getClient: () => ({
            getBooleanValue: async () => {
              throw new Error("provider boom");
            },
          }),
        },
      };
    });
    const { getFlag } = await import("./flags");
    expect(await getFlag("flag-seam-smoke")).toBe(true);
    vi.doUnmock("@openfeature/server-sdk");
  });
});
