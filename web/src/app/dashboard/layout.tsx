/**
 * add-feature-flag-seam: resolves the web flag set on the server (registry defaults + FLAG_* env
 * overrides, evaluated against the current Clerk session) and hands it to the client flag provider,
 * so `useFlag(...)` works in any dashboard client component. Also does the server-side smoke read.
 */
import { resolveWebFlags, getBooleanFlag } from "@/lib/flags";
import { FlagProvider } from "@/lib/flags-client";

export default async function DashboardLayout({ children }: LayoutProps<"/dashboard">) {
  const flags = await resolveWebFlags();

  // Server-side smoke read — proves the RSC path. Structured log only; gates nothing.
  const smoke = await getBooleanFlag("flag-seam-smoke");
  console.info(`flag_seam_smoke surface=web path=server value=${smoke}`);

  return <FlagProvider values={flags}>{children}</FlagProvider>;
}
