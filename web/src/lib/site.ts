/**
 * Canonical origin for the marketing surface, used by metadata, sitemap.xml, robots.txt and the
 * Open Graph image. Resolution order:
 *   1. NEXT_PUBLIC_SITE_URL         — set this once the real domain is live (e.g. https://releasetwin.com)
 *   2. VERCEL_PROJECT_PRODUCTION_URL — Vercel injects this automatically for the production deploy
 *   3. localhost                    — dev fallback
 *
 * Keep it a bare origin (no trailing slash, no path) — callers append their own paths.
 */
export const SITE_URL = (() => {
  const explicit = process.env.NEXT_PUBLIC_SITE_URL;
  if (explicit) return explicit.replace(/\/$/, "");
  const vercel = process.env.VERCEL_PROJECT_PRODUCTION_URL;
  if (vercel) return `https://${vercel}`;
  return "http://localhost:3000";
})();

export const SITE_NAME = "ReleaseTwin";

/**
 * The legal entity named in the Terms and Privacy Policy. TODO: replace with the registered
 * LLC name once formed (see the planning notes' "Company & billing"). Kept in one place so the
 * legal pages don't need editing again.
 */
export const LEGAL_ENTITY = "the ReleaseTwin project";

/** Where legal / privacy / security questions go. TODO: swap for a domain address. */
export const LEGAL_CONTACT_EMAIL = "ernestoalejo22@gmail.com";

export const SITE_DESCRIPTION =
  "Self-serve release-proof testing. Compose HTTP and UI journeys, run them from your own CI, " +
  "and prove a fix works by running the same case known-bad and known-good — your test data never leaves your infra.";
