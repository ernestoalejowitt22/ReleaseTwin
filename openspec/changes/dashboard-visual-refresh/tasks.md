## 1. Color tokens

- [x] 1.1 In `web/src/app/globals.css`, replace `--primary`, `--ring`, and `--sidebar-primary` in
      both the light block and the `.dark` block with real indigo chroma/hue values
      (~`oklch(0.55 0.18 275)` light-mode primary; a lighter/darker variant per block per shadcn's
      existing light/dark pairing convention). Leave `--destructive` untouched.
- [x] 1.2 Replace `--secondary` and `--accent` with a low-chroma tint of the same hue (not full
      saturation) in both blocks, so they still recede behind primary actions.
- [x] 1.3 Replace `--chart-1` through `--chart-5` with a sequential lightness ramp of the same hue
      in both blocks.
- [x] 1.4 Visually confirm (screenshot) the accent doesn't collide with PASS/FAIL badge colors on
      the dashboard's run-history table.

## 2. Badge consistency audit

- [x] 2.1 Wrap the flag-proof leg outcomes (`web/src/app/dashboard/page.tsx:354-367`) in `Badge`
      with the appropriate variant, matching the pattern already used for case-report PASS/FAIL at
      line 315.
- [x] 2.2 Sweep `web/src/app/dashboard/page.tsx` and `web/src/app/journeys/**` for any other
      status-shaped plain-text value (configured/not-configured, revoked/active, etc.) not already
      using `Badge`, and convert each to the matching existing variant.

## 3. Dark mode

- [x] 3.1 Mount a `ThemeProvider` at the root in `web/src/app/layout.tsx`, wrapping the existing
      `ClerkProvider`/`html`/`body` structure without changing its current behavior. Pivoted away
      from the `next-themes` package itself (uninstalled) — reproduced for real that it
      unconditionally renders an inline `<script>` as part of its client-rendered tree, and this
      project's exact Next.js 16 / React 19 combination throws "Encountered a script tag while
      rendering React component" on any client-side re-render of it, blanking the whole page. Not
      next-themes-specific: a plain JSX `<script>` and even Next's own official `next/script`
      (`strategy="beforeInteractive"`) hit the identical failure, confirmed against a real `next dev`
      server with a fresh `.next` cache. Hand-rolled `theme-provider.tsx` instead (React context +
      `useSyncExternalStore`, no `<script>` element anywhere) — see its own comment for the full
      writeup. Practical consequence, accepted and documented rather than silently dropped: no
      anti-flash-of-wrong-theme mechanism exists (that normally requires a pre-hydration script), so
      a cold load can briefly show the light theme before the client-side read applies the real one.
- [x] 3.2 Add a toggle control in the dashboard header, next to the Clerk `UserButton`.
- [x] 3.3 Confirm (screenshot, both modes) that every page already styled via the `globals.css`
      token architecture — dashboard, journeys, sign-in, landing — renders correctly in both.

## 4. Dashboard regrouping

- [x] 4.1 Reorganize `web/src/app/dashboard/page.tsx`'s card order into three visually distinct
      sections: Set up (Connection, Adapter credentials, Project secrets), Run (Journeys, API
      tokens), Results (Run history, Flag-proof results) — section labels, not tabs.
- [x] 4.2 Implement the Set-up section's collapse-when-configured behavior: a single summary line
      when at least one of Connection/Adapter-credentials/Project-secrets has something configured,
      expanded by default otherwise. Single derived boolean, no persistence.
- [x] 4.3 Confirm both states (brand-new project, fully-configured project) render as designed.

## 5. Landing page

- [x] 5.1 Add a real product screenshot (the post-refresh dashboard) to `web/src/app/page.tsx`,
      alongside the existing copy. Implemented as a live-rendered preview using the actual Card/
      Table/Badge components (real case IDs from the bundled examples) rather than a static image —
      no file-saving pipeline available to capture/commit a literal screenshot in this session, and
      this stays in sync with the real theme automatically instead of going stale.
- [x] 5.2 Add 3–4 one-line feature callouts beneath the existing value-proposition text.

## 6. Icons

- [x] 6.1 Add `lucide-react` icons to the dashboard's section headers (one per zone from task 4) and
      to the primary nav/header elements where they clarify meaning (not decoratively everywhere).

## 8. Typography (resolves design.md's Open Question)

- [x] 8.1 Found and fixed a real, pre-existing bug while testing dark mode locally: `globals.css`'s
      `@theme inline` block defined `--font-sans: var(--font-sans)` — a circular self-reference that
      never actually resolved to the real Geist font `next/font` binds to `--font-geist-sans` on
      `<html>`. Confirmed via computed style: the *entire page* (not just headings) was rendering in
      the browser's serif fallback, not Geist. Fixed to `--font-sans: var(--font-geist-sans)`. This
      also resolves design.md's deferred serif/sans Open Question — `--font-heading` was already
      aliased to `--font-sans`, so fixing the one line moves both body and headings to a real,
      modern sans-serif face, matching the Linear/Vercel-style alternative design.md left open.

## 7. Real verification

- [ ] 7.1 Update or extend the existing Cypress specs' `cy.screenshot(...)` calls (already present
      in `adapter-credentials.cy.ts`, `journey-builder.cy.ts`, `project-secrets.cy.ts`, etc.) to
      confirm the refreshed dashboard renders correctly across the real, already-covered workflows
      — no new test infrastructure needed, this reuses what real e2e coverage already exists.
- [ ] 7.2 Run the full existing Cypress suite (`npm run e2e`, `e2e:secrets`, `e2e:naha`, etc.) after
      the visual changes to confirm no selector (`input[name=...]`, `.rounded-xl`, `.rounded-lg`,
      button text) broke — this change touches classnames and layout that several real specs
      already select against.
