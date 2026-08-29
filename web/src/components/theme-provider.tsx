"use client";

import { createContext, useCallback, useContext, useEffect, useSyncExternalStore, type ReactNode } from "react";

type Theme = "light" | "dark" | "system";
type ResolvedTheme = "light" | "dark";

interface ThemeContextValue {
  theme: Theme;
  resolvedTheme: ResolvedTheme;
  setTheme: (theme: Theme) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);
const STORAGE_KEY = "theme";

function getSystemTheme(): ResolvedTheme {
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function readStoredTheme(): Theme {
  try {
    return (localStorage.getItem(STORAGE_KEY) as Theme | null) ?? "system";
  } catch {
    return "system";
  }
}

/** Fires on the OS preference changing and on `setTheme`'s own synthetic dispatch below — real cross-tab `storage` events only fire in other tabs, so same-tab updates need this manual nudge. */
function subscribe(callback: () => void) {
  const mql = window.matchMedia("(prefers-color-scheme: dark)");
  mql.addEventListener("change", callback);
  window.addEventListener("storage", callback);
  return () => {
    mql.removeEventListener("change", callback);
    window.removeEventListener("storage", callback);
  };
}

/**
 * dashboard-visual-refresh: hand-rolled instead of the next-themes package. next-themes'
 * ThemeProvider unconditionally renders an inline <script> as part of its client-rendered tree,
 * and this project's Next.js 16 / React 19 combination throws "Encountered a script tag while
 * rendering React component" on any client-side re-render of it (React intentionally never
 * executes a client-rendered <script>) — reproduced for real against a real `next dev` server with
 * a fresh .next cache, not an HMR artifact, and it blanks the whole page. This isn't specific to
 * next-themes: a plain JSX <script> and even Next's own official `next/script`
 * (strategy="beforeInteractive") hit the identical failure — so no <script> element of any kind is
 * used anywhere in this tree. The practical consequence: there's no anti-flash-of-wrong-theme
 * mechanism (that normally requires a pre-hydration script) — a cold load can briefly show the
 * light theme before this provider's client-side read applies the real one. Accepted and
 * documented, not silently dropped; see design.md's Risks section.
 *
 * Reads theme state via useSyncExternalStore rather than a useState+useEffect mount pattern — this
 * project's eslint config flags synchronous setState-in-effect as an error, and reading an external
 * source (localStorage/matchMedia) with a server-safe fallback snapshot is exactly what
 * useSyncExternalStore is for.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const theme = useSyncExternalStore(subscribe, readStoredTheme, () => "system" as Theme);
  const resolvedTheme = useSyncExternalStore(
    subscribe,
    () => (theme === "system" ? getSystemTheme() : theme),
    () => "light" as ResolvedTheme,
  );

  // Legitimate effect use: syncing the resolved theme onto the DOM (an external system), not
  // calling setState.
  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle("dark", resolvedTheme === "dark");
    root.style.colorScheme = resolvedTheme;
  }, [resolvedTheme]);

  const setTheme = useCallback((next: Theme) => {
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // localStorage unavailable — theme still applies for this render, just won't persist.
    }
    window.dispatchEvent(new StorageEvent("storage", { key: STORAGE_KEY }));
  }, []);

  return <ThemeContext.Provider value={{ theme, resolvedTheme, setTheme }}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error("useTheme must be used within a ThemeProvider");
  }
  return ctx;
}
