"use client";

import { useSyncExternalStore } from "react";
import { useTheme } from "next-themes";
import { Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";

const noopSubscribe = () => () => {};

/**
 * dashboard-visual-refresh: next-themes' own docs recommend a `useState`+`useEffect` "mounted" flag
 * to avoid a hydration mismatch (theme is unknown on the server), but this project's eslint config
 * flags synchronous setState-in-effect as an error — useSyncExternalStore is the modern, lint-clean
 * equivalent: false on the server/first client render, true once hydrated, no render-triggering
 * effect involved.
 */
function useIsMounted() {
  return useSyncExternalStore(
    noopSubscribe,
    () => true,
    () => false,
  );
}

/**
 * Page-wide, low-frequency preference, so it lives in the header next to the Clerk UserButton —
 * same placement convention GitHub/Linear/Vercel use for theirs.
 */
export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const mounted = useIsMounted();

  if (!mounted) {
    return <Button variant="ghost" size="icon" aria-label="Toggle theme" disabled />;
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      aria-label="Toggle theme"
      onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}
    >
      {resolvedTheme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
    </Button>
  );
}
