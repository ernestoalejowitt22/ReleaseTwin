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
 * The legal entity named in the Terms and Privacy Policy. ReleaseTwin operates as a Mexican sole
 * proprietor (persona física con actividad empresarial, RESICO) — this is the operator's registered
 * name. Replace with an incorporated entity's name if/when one is formed (S.A.S. / S. de R.L.);
 * kept in one place so the legal pages don't need editing again. Confirm this matches the exact
 * name on the SAT registration (nombre + RFC) before general availability.
 */
export const LEGAL_ENTITY = "Ernesto Alejo (persona física con actividad empresarial)";

/**
 * Contact addresses. Today all three resolve to the same personal inbox; after
 * company-and-domain-launch they become hello@ / security@ / legal@ on the domain — change the
 * values here only, every page reads these constants.
 */
export const CONTACT_EMAIL = "ernestoalejo22@gmail.com";
export const SECURITY_CONTACT_EMAIL = CONTACT_EMAIL;
export const LEGAL_CONTACT_EMAIL = CONTACT_EMAIL;

export const SITE_DESCRIPTION =
  "Self-serve release-proof testing. Compose HTTP and UI journeys, run them from your own CI, " +
  "and prove a fix works by running the same case known-bad and known-good — your test data never leaves your infra.";
