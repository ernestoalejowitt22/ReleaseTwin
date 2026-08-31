"use client";

/**
 * add-feature-flag-seam: client-side feature-flag evaluation for `web/` components. The server
 * resolves the web flag set (registry defaults + `FLAG_*` env overrides) and passes the values in;
 * this provider seeds an OpenFeature web in-memory provider from them so the read path is the same
 * OpenFeature seam a real provider will plug into later. See docs/feature-flags.md.
 *
 * No inline <script> — a normal client provider component, per design.md Risks (layout.tsx is
 * script-fragile under this Next 16 / React 19 combo).
 */
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { InMemoryProvider, OpenFeature, type Client, type JsonValue } from "@openfeature/web-sdk";
import { WEB_FLAGS, flagDefinition, type FlagKey, type FlagValue } from "./flags-registry";

export type ResolvedFlags = Partial<Record<FlagKey, FlagValue>>;

const FlagClientContext = createContext<Client | null>(null);

const PROVIDER_DOMAIN = "web";

type InMemoryConfig = ConstructorParameters<typeof InMemoryProvider>[0];

function buildClient(values: ResolvedFlags): Client {
  const config: Record<string, { variants: Record<string, unknown>; disabled: boolean; defaultVariant: string }> = {};
  for (const def of WEB_FLAGS) {
    const value = values[def.key as FlagKey] ?? def.default;
    config[def.key] = { variants: { value }, disabled: false, defaultVariant: "value" };
  }
  OpenFeature.setProvider(PROVIDER_DOMAIN, new InMemoryProvider(config as InMemoryConfig));
  return OpenFeature.getClient(PROVIDER_DOMAIN);
}

export function FlagProvider({ values, children }: { values: ResolvedFlags; children: ReactNode }) {
  // Re-seed only when the resolved set actually changes (it is stable per navigation).
  const client = useMemo(() => buildClient(values), [values]);
  return <FlagClientContext.Provider value={client}>{children}</FlagClientContext.Provider>;
}

/**
 * Read a flag in a client component. Fails open to the registry default if the provider is missing
 * or errors. Never throws.
 */
export function useFlag<T extends FlagValue = FlagValue>(key: FlagKey): T {
  const client = useContext(FlagClientContext);
  const def = flagDefinition(key);
  const fallback = def.default as T;
  return useMemo(() => {
    if (!client) return fallback;
    try {
      switch (def.type) {
        case "boolean":
          return client.getBooleanValue(key, fallback as boolean) as T;
        case "string":
          return client.getStringValue(key, fallback as string) as T;
        case "number":
          return client.getNumberValue(key, fallback as number) as T;
        case "object":
          return client.getObjectValue(key, fallback as unknown as JsonValue) as T;
      }
    } catch {
      return fallback;
    }
  }, [client, key, def.type, fallback]);
}

export function useBooleanFlag(key: FlagKey): boolean {
  return useFlag<boolean>(key);
}
