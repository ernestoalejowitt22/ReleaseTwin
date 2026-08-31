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

/**
 * marketing-site-dynamic-content: human-facing label + one-line description for every catalog
 * entitlement key. The pricing comparison table, the /features hosted table, and the homepage
 * feature section all render from this map, so they cannot disagree with each other or with the
 * catalog. `assertFeatureCopyComplete()` runs at module load — a catalog key with no entry here
 * (or an entry with no catalog key) throws, which fails `next build`.
 */
export const FEATURE_COPY: Record<EntitlementKey, { label: string; description: string; docHref?: string }> = {
  maxProjects: {
    label: "Projects",
    description: "How many active projects can land run history on the dashboard at once.",
  },
  evidenceViewer: {
    label: "Evidence viewer",
    description:
      "Per-step request/response summaries, assertion detail, and UI screenshots — redacted in your CLI before upload.",
    docHref: "/docs/hosted-platform",
  },
  maxEvidenceRetentionDays: {
    label: "Evidence retention",
    description: "How long uploaded evidence is kept before the daily purge removes it. Metadata reports are kept regardless.",
    docHref: "/docs/hosted-platform",
  },
  customRedactionRules: {
    label: "Custom redaction rules",
    description: "Your own allow/deny rules layered on top of the built-in credential redaction.",
    docHref: "/docs/security",
  },
  projectSecrets: {
    label: "Hosted project secrets",
    description: "Store credentials once per project; the CLI fetches them wherever it runs, instead of per-runner env vars.",
    docHref: "/docs/security",
  },
  trendAnalytics: {
    label: "Trend analytics",
    description: "Pass/fail and flake trends across a project's run history.",
  },
  releaseRollup: {
    label: "Release roll-up",
    description: "One view aggregating every case result for a release or build identity.",
  },
  ciIntegration: {
    label: "CI integration",
    description: "Run cases from GitHub Actions or any CI, with the uploaded run history wired in.",
    docHref: "/docs/ci",
  },
  sso: {
    label: "SSO",
    description: "SAML / OIDC single sign-on for your organization.",
  },
  auditLog: {
    label: "Audit log",
    description: "A record of who did what in your account.",
  },
};

/** Curated, ordered subset of entitlements shown in the homepage "hosted adds…" section. */
export const HOMEPAGE_FEATURES: readonly EntitlementKey[] = [
  "evidenceViewer",
  "projectSecrets",
  "trendAnalytics",
  "customRedactionRules",
];

/** Every catalog entitlement key, in canonical order — the row set for the comparison / features tables. */
export function entitlementKeys(): readonly EntitlementKey[] {
  return ENTITLEMENT_KEYS;
}

/** The tiers to render as pricing cards / comparison columns, in catalog order. */
export function tiersForDisplay(): PlanTier[] {
  return PLAN_TIERS;
}

/**
 * Render a single tier's value for an entitlement key: a string for numeric keys (with the
 * `null = unlimited / custom` convention spelled out), a boolean for the rest.
 */
export function formatEntitlementValue(key: EntitlementKey, value: number | boolean | null): string | boolean {
  if (key === "maxProjects") {
    return value === null ? "Unlimited" : String(value);
  }
  if (key === "maxEvidenceRetentionDays") {
    if (value === null) return "Custom";
    const days = value as number;
    if (days < 90) return `${days} days`;
    return `${Math.round(days / 30)} months`;
  }
  return Boolean(value);
}

/** Format a tier's price as a display string (`$0`, `~$49`), the `~` prefix meaning placeholder. */
export function formatPrice(price: PlanPrice): string {
  return `${price.placeholder ? "~" : ""}$${price.amount}`;
}

function assertFeatureCopyComplete(): void {
  const copyKeys = Object.keys(FEATURE_COPY);
  const missing = ENTITLEMENT_KEYS.filter((k) => !(k in FEATURE_COPY));
  const orphaned = copyKeys.filter((k) => !ENTITLEMENT_KEYS.includes(k as EntitlementKey));
  if (missing.length || orphaned.length) {
    throw new Error(
      `FEATURE_COPY is out of sync with the plan catalog — ` +
        `missing copy for [${missing.join(", ")}]; orphaned copy for [${orphaned.join(", ")}]`,
    );
  }
}

assertFeatureCopyComplete();

/** The lowest-ordered tier whose entitlement set grants `key` (any truthy value for numeric keys). */
export function lowestTierWith(key: EntitlementKey): PlanTier | undefined {
  return PLAN_TIERS.find((t) => {
    const value = t.entitlements[key];
    return NUMERIC_ENTITLEMENTS.includes(key) ? value === null || (typeof value === "number" && value > 0) : Boolean(value);
  });
}
