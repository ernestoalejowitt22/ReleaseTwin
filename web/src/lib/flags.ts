/**
 * add-feature-flag-seam: server-side feature-flag evaluation for `web/` (RSC, route handlers,
 * server actions). Built on OpenFeature so adopting LaunchDarkly later is a provider swap here and
 * nothing else — see docs/feature-flags.md.
 *
 * Phase 1 provider: OpenFeature's in-memory provider, seeded from `flags.json` with a per-key
 * `FLAG_<KEY>` env override. No network, no account.
 */
import "server-only";
import { auth } from "@clerk/nextjs/server";
import { InMemoryProvider, OpenFeature, type EvaluationContext, type JsonValue, type Provider } from "@openfeature/server-sdk";
import { FLAG_REGISTRY, flagDefinition, type FlagKey, type FlagValue } from "./flags-registry";

export interface FlagContext {
  targetingKey?: string;
  userId?: string;
  plan?: string;
  projectId?: string;
  surface: "web";
  env: "production" | "preview" | "development";
}

function currentEnv(): FlagContext["env"] {
  const v = process.env.VERCEL_ENV ?? process.env.NODE_ENV;
  if (v === "production") return "production";
  if (v === "preview") return "preview";
  return "development";
}

/** `flag-seam-smoke` -> `FLAG_FLAG_SEAM_SMOKE` */
function envVarName(key: string): string {
  return `FLAG_${key.replace(/-/g, "_").toUpperCase()}`;
}

/** Parse an env-var override string into the flag's declared type; undefined = no/invalid override. */
function parseOverride(key: FlagKey, raw: string): FlagValue | undefined {
  const { type } = flagDefinition(key);
  try {
    switch (type) {
      case "boolean":
        if (raw === "true") return true;
        if (raw === "false") return false;
        return undefined;
      case "number": {
        const n = Number(raw);
        return Number.isFinite(n) ? n : undefined;
      }
      case "string":
        return raw;
      case "object": {
        const parsed = JSON.parse(raw);
        return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed) ? parsed : undefined;
      }
    }
  } catch {
    return undefined;
  }
}

type InMemoryConfig = ConstructorParameters<typeof InMemoryProvider>[0];

function buildProvider(): Provider {
  const config: Record<string, { variants: Record<string, unknown>; disabled: boolean; defaultVariant: string }> = {};
  for (const def of FLAG_REGISTRY) {
    const override = process.env[envVarName(def.key)];
    const value = override !== undefined ? (parseOverride(def.key as FlagKey, override) ?? def.default) : def.default;
    config[def.key] = { variants: { value }, disabled: false, defaultVariant: "value" };
  }
  return new InMemoryProvider(config as InMemoryConfig);
}

let ready: Promise<void> | undefined;

async function client() {
  ready ??= OpenFeature.setProviderAndWait(buildProvider());
  await ready;
  return OpenFeature.getClient();
}

function toEvaluationContext(ctx: FlagContext): EvaluationContext {
  const out: EvaluationContext = {};
  for (const [k, v] of Object.entries(ctx)) {
    if (v !== undefined) out[k] = v;
  }
  return out;
}

/**
 * Build the standard evaluation context from the current Clerk session. `plan` is best-effort from
 * session claims; pass an explicit context when you already hold the dashboard view.
 */
export async function serverFlagContext(overrides: Partial<FlagContext> = {}): Promise<FlagContext> {
  const { userId, orgId, sessionClaims } = await auth();
  const planClaim = (sessionClaims as { plan?: string } | null)?.plan;
  return {
    targetingKey: orgId ?? undefined,
    userId: userId ?? undefined,
    plan: planClaim ?? "unknown",
    surface: "web",
    env: currentEnv(),
    ...overrides,
  };
}

/**
 * Resolve a flag. Fails open: a provider error, an unknown key, or a wrong-typed value returns the
 * flag's registry default. Never throws.
 */
export async function getFlag<T extends FlagValue = FlagValue>(key: FlagKey, ctx?: FlagContext): Promise<T> {
  const def = flagDefinition(key);
  const fallback = def.default as T;
  try {
    const c = await client();
    const evalCtx = ctx ? toEvaluationContext(ctx) : toEvaluationContext(await serverFlagContext());
    switch (def.type) {
      case "boolean":
        return (await c.getBooleanValue(key, fallback as boolean, evalCtx)) as T;
      case "string":
        return (await c.getStringValue(key, fallback as string, evalCtx)) as T;
      case "number":
        return (await c.getNumberValue(key, fallback as number, evalCtx)) as T;
      case "object":
        return (await c.getObjectValue(key, fallback as unknown as JsonValue, evalCtx)) as T;
    }
  } catch {
    return fallback;
  }
}

export async function getBooleanFlag(key: FlagKey, ctx?: FlagContext): Promise<boolean> {
  return getFlag<boolean>(key, ctx);
}

/**
 * Resolve every web-surface flag once on the server, to hand to the client `<FlagProvider>` so
 * client reads honour the same `FLAG_*` env overrides. Fails open per-flag.
 */
export async function resolveWebFlags(ctx?: FlagContext): Promise<Record<string, FlagValue>> {
  const evalCtx = ctx ?? (await serverFlagContext());
  const out: Record<string, FlagValue> = {};
  for (const def of FLAG_REGISTRY) {
    if (!def.surfaces.includes("web")) continue;
    out[def.key] = await getFlag(def.key as FlagKey, evalCtx);
  }
  return out;
}
