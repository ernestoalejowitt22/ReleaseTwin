"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";
import type { ComponentProps } from "react";

/**
 * dashboard-visual-refresh: next-themes was already a declared dependency but never mounted —
 * globals.css's `.dark` class selector (`@custom-variant dark (&:is(.dark *))`) has been waiting for
 * this. `attribute="class"` toggles that exact selector; `enableSystem` respects the OS preference
 * by default, matching how every other theme-aware surface in this codebase already behaves.
 */
export function ThemeProvider({ children, ...props }: ComponentProps<typeof NextThemesProvider>) {
  return (
    <NextThemesProvider attribute="class" defaultTheme="system" enableSystem {...props}>
      {children}
    </NextThemesProvider>
  );
}
