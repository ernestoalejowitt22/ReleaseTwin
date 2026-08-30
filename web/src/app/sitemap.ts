import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/site";

/**
 * Static list of the public marketing routes. Keep in sync when adding a page under
 * src/app/(marketing)/ — there are few enough that hand-maintaining this is clearer than globbing
 * the route tree at build time.
 */
const ROUTES: Array<{
  path: string;
  priority: number;
  changeFrequency: MetadataRoute.Sitemap[number]["changeFrequency"];
}> = [
  { path: "/", priority: 1.0, changeFrequency: "weekly" },
  { path: "/pricing", priority: 0.8, changeFrequency: "monthly" },
  { path: "/docs", priority: 0.7, changeFrequency: "weekly" },
  { path: "/docs/quickstart", priority: 0.7, changeFrequency: "monthly" },
  { path: "/docs/ci", priority: 0.6, changeFrequency: "monthly" },
  { path: "/docs/case-files", priority: 0.6, changeFrequency: "monthly" },
  { path: "/docs/hosted-platform", priority: 0.6, changeFrequency: "monthly" },
  { path: "/docs/security", priority: 0.6, changeFrequency: "monthly" },
];

export default function sitemap(): MetadataRoute.Sitemap {
  const lastModified = new Date();
  return ROUTES.map(({ path, priority, changeFrequency }) => ({
    url: `${SITE_URL}${path}`,
    lastModified,
    changeFrequency,
    priority,
  }));
}
