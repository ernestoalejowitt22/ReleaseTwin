/**
 * add-feature-flag-seam: the marketing site, the hosted API, and the CLI share ONE feature-flag
 * registry, `flags.json` at the repo root. This module is the typed view `web/` reads from — the
 * same cross-root import pattern `plans.ts` uses for `hosted/plans.json`.
 *
 * `validateRegistry()` runs at module load: a malformed or shape-drifted `flags.json` throws here,
 * which fails `next build`, so a bad registry can never ship. Because every flag key `web/` reads is
 * a member of `FlagKey` (derived from this file), a typo at a call site is a TypeScript error —
 * that is the web-side drift check.
 *
 * NOT LaunchDarkly's product-adapter config — this gates ReleaseTwin itself.
 */
import rawRegistry from "../../../flags.json";

export type FlagType = "boolean" | "string" | "number" | "object";

export type FlagValue = boolean | string | number | Record<string, unknown>;

export type FlagSurface = "web" | "hosted" | "cli";

export interface FlagDefinition {
  key: string;
  type: FlagType;
  default: FlagValue;
  description: string;
  surfaces: FlagSurface[];
  owner: string;
}

const FLAG_TYPES: readonly FlagType[] = ["boolean", "string", "number", "object"];
const FLAG_SURFACES: readonly FlagSurface[] = ["web", "hosted", "cli"];
const KEY_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

function typeMatches(type: FlagType, value: unknown): boolean {
  switch (type) {
    case "boolean":
      return typeof value === "boolean";
    case "string":
      return typeof value === "string";
    case "number":
      return typeof value === "number";
    case "object":
      return typeof value === "object" && value !== null && !Array.isArray(value);
  }
}

function validateRegistry(input: unknown): FlagDefinition[] {
  if (typeof input !== "object" || input === null || !Array.isArray((input as { flags?: unknown }).flags)) {
    throw new Error("flags.json: missing `flags` array");
  }
  const flags = (input as { flags: unknown[] }).flags;
  const seen = new Set<string>();
  const result: FlagDefinition[] = [];

  for (const entry of flags as Array<Record<string, unknown>>) {
    const key = entry.key;
    if (typeof key !== "string" || !KEY_PATTERN.test(key)) {
      throw new Error(`flags.json: flag key '${String(key)}' must be kebab-case`);
    }
    if (seen.has(key)) {
      throw new Error(`flags.json: duplicate flag key '${key}'`);
    }
    seen.add(key);

    const type = entry.type;
    if (typeof type !== "string" || !FLAG_TYPES.includes(type as FlagType)) {
      throw new Error(`flags.json: flag '${key}' has an invalid type '${String(type)}'`);
    }
    if (!typeMatches(type as FlagType, entry.default)) {
      throw new Error(`flags.json: flag '${key}' default does not match declared type '${type}'`);
    }
    if (typeof entry.description !== "string" || entry.description.length === 0) {
      throw new Error(`flags.json: flag '${key}' is missing a description`);
    }
    if (typeof entry.owner !== "string" || entry.owner.length === 0) {
      throw new Error(`flags.json: flag '${key}' is missing an owner`);
    }
    if (
      !Array.isArray(entry.surfaces) ||
      entry.surfaces.length === 0 ||
      (entry.surfaces as unknown[]).some((s) => !FLAG_SURFACES.includes(s as FlagSurface))
    ) {
      throw new Error(`flags.json: flag '${key}' has an invalid surfaces list`);
    }

    result.push({
      key,
      type: type as FlagType,
      default: entry.default as FlagValue,
      description: entry.description,
      surfaces: entry.surfaces as FlagSurface[],
      owner: entry.owner,
    });
  }

  if (result.length === 0) {
    throw new Error("flags.json: at least one flag must be defined");
  }
  return result;
}

export const FLAG_REGISTRY: readonly FlagDefinition[] = validateRegistry(rawRegistry);

const BY_KEY = new Map(FLAG_REGISTRY.map((f) => [f.key, f]));

/**
 * Every flag key, as a literal tuple. JSON module imports widen to `string`, so — like `plans.ts`'s
 * `ENTITLEMENT_KEYS` — this list is maintained by hand and `assertRegistryParity()` (below, runs at
 * module load) throws if it disagrees with `flags.json`. Referencing a key not in this union is a
 * TypeScript error at the call site.
 */
export const FLAG_KEYS = ["flag-seam-smoke", "run-notifications"] as const;

export type FlagKey = (typeof FLAG_KEYS)[number];

function assertRegistryParity(): void {
  const registryKeys = FLAG_REGISTRY.map((f) => f.key).sort();
  const declaredKeys = [...FLAG_KEYS].sort();
  const missing = registryKeys.filter((k) => !declaredKeys.includes(k as FlagKey));
  const orphaned = declaredKeys.filter((k) => !registryKeys.includes(k));
  if (missing.length || orphaned.length) {
    throw new Error(
      `flags.json is out of sync with FLAG_KEYS in flags-registry.ts — ` +
        `missing from FLAG_KEYS: [${missing.join(", ")}]; not in flags.json: [${orphaned.join(", ")}]`,
    );
  }
}

assertRegistryParity();

export function flagDefinition(key: FlagKey): FlagDefinition {
  const def = BY_KEY.get(key);
  if (!def) {
    throw new Error(`flags.json: no flag '${key}'`);
  }
  return def;
}

/** Flags this surface participates in. */
export const WEB_FLAGS: readonly FlagDefinition[] = FLAG_REGISTRY.filter((f) => f.surfaces.includes("web"));
