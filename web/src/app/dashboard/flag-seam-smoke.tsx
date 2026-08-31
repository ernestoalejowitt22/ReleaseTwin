"use client";

/**
 * add-feature-flag-seam: proves the client-side flag read path. Renders nothing visible — the value
 * only reaches the DOM as a data attribute for the e2e/smoke check. Delete when a real flag replaces
 * `flag-seam-smoke`.
 */
import { useBooleanFlag } from "@/lib/flags-client";

export function FlagSeamSmoke() {
  const on = useBooleanFlag("flag-seam-smoke");
  return <span hidden data-flag-seam-smoke={on ? "on" : "off"} />;
}
