/**
 * plan-catalog-and-entitlements: the marketing site and the hosted API share ONE plan catalog,
 * `hosted/plans.json`. The API embeds it for entitlement enforcement; this module is the typed view
 * the `web/` app renders from.
 *
 * The raw JSON is imported directly (it lives one level up from `web/`, on the BSL side alongside
 * this app). `validateCatalog()` runs at module load — a malformed or shape-drifted `plans.json`
 * throws here, which fails `next build`, so the marketing copy can never quietly disagree with the
 * enforced catalog.
 */
import rawCatalog from "../../../hosted/plans.json";

export type EntitlementKey =
  | "maxProjects"
  | "evidenceViewer"
  | "maxEvidenceRetentionDays"
  | "customRedactionRules"
  | "projectSecrets"
  | "trendAnalytics"
  | "releaseRollup"
  | "ciIntegration"
  | "sso"
  | "auditLog";

/** Keep in sync with the C# `Entitlements` record and the keys in `plans.json`. */
export const ENTITLEMENT_KEYS: readonly EntitlementKey[] = [
  "maxProjects",
  "evidenceViewer",
  "maxEvidenceRetentionDays",
  "customRedactionRules",
  "projectSecrets",
  "trendAnalytics",
  "releaseRollup",
  "ciIntegration",
  "sso",
  "auditLog",
];

/** Entitlement keys whose value is `number | null` (null = unlimited / custom); the rest are `boolean`. */
const NUMERIC_ENTITLEMENTS: readonly EntitlementKey[] = ["maxProjects", "maxEvidenceRetentionDays"];

export interface Entitlements {
  maxProjects: number | null;
  evidenceViewer: boolean;
  maxEvidenceRetentionDays: number | null;
  customRedactionRules: boolean;
  projectSecrets: boolean;
  trendAnalytics: boolean;
  releaseRollup: boolean;
  ciIntegration: boolean;
  sso: boolean;
  auditLog: boolean;
}

export interface PlanPrice {
  amount: number;
  unit: string;
  placeholder: boolean;
}

export interface PlanTier {
  id: "free" | "team" | "enterprise";
  name: string;
  price: PlanPrice;
  support: string;
  entitlements: Entitlements;
}

export interface PlanCatalog {
  tiers: PlanTier[];
}

function validateCatalog(input: unknown): PlanCatalog {
  if (typeof input !== "object" || input === null || !Array.isArray((input as { tiers?: unknown }).tiers)) {
    throw new Error("plans.json: missing `tiers` array");
  }
  const tiers = (input as { tiers: unknown[] }).tiers;
  const expectedIds = ["free", "team", "enterprise"];
  const actualIds = tiers.map((t) => (t as { id?: unknown }).id);
  if (actualIds.length !== 3 || expectedIds.some((id, i) => actualIds[i] !== id)) {
    throw new Error(`plans.json: tiers must be exactly [${expectedIds.join(", ")}] in order, got [${actualIds.join(", ")}]`);
  }

  for (const tier of tiers as Array<Record<string, unknown>>) {
    if (typeof tier.name !== "string" || typeof tier.support !== "string") {
      throw new Error(`plans.json: tier '${String(tier.id)}' is missing name or support`);
    }
    const price = tier.price as Record<string, unknown> | undefined;
    if (!price || typeof price.amount !== "number" || typeof price.unit !== "string" || typeof price.placeholder !== "boolean") {
      throw new Error(`plans.json: tier '${String(tier.id)}' has a malformed price`);
    }
    const ent = tier.entitlements as Record<string, unknown> | undefined;
    if (!ent) {
      throw new Error(`plans.json: tier '${String(tier.id)}' is missing entitlements`);
    }
    for (const key of ENTITLEMENT_KEYS) {
      if (!(key in ent)) {
        throw new Error(`plans.json: tier '${String(tier.id)}' is missing entitlement '${key}'`);
      }
      const value = ent[key];
      if (NUMERIC_ENTITLEMENTS.includes(key)) {
        if (value !== null && typeof value !== "number") {
          throw new Error(`plans.json: entitlement '${key}' on '${String(tier.id)}' must be a number or null`);
        }
      } else if (typeof value !== "boolean") {
        throw new Error(`plans.json: entitlement '${key}' on '${String(tier.id)}' must be a boolean`);
      }
    }
  }

  return input as PlanCatalog;
}

export const PLAN_CATALOG: PlanCatalog = validateCatalog(rawCatalog);

export const PLAN_TIERS: PlanTier[] = PLAN_CATALOG.tiers;

export function tierById(id: PlanTier["id"]): PlanTier {
  const tier = PLAN_TIERS.find((t) => t.id === id);
  if (!tier) {
    throw new Error(`plans.json: no tier '${id}'`);
  }
  return tier;
}

/** The lowest-ordered tier whose entitlement set grants `key` (any truthy value for numeric keys). */
export function lowestTierWith(key: EntitlementKey): PlanTier | undefined {
  return PLAN_TIERS.find((t) => {
    const value = t.entitlements[key];
    return NUMERIC_ENTITLEMENTS.includes(key) ? value === null || (typeof value === "number" && value > 0) : Boolean(value);
  });
}
