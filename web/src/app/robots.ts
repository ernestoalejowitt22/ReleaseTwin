import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/site";

/**
 * Marketing pages are crawlable; the authenticated app surface (dashboard, journeys, the OAuth
 * connect flow, Clerk's own auth pages) has nothing to index and is disallowed.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: ["/dashboard", "/journeys", "/connect", "/sign-in", "/sign-up"],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
    host: SITE_URL,
  };
}
