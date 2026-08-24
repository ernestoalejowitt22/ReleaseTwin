import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

// hosted-react-frontend design.md: Next.js 16 renamed "middleware" to "proxy" (file convention
// changed, functionality unchanged) — clerkMiddleware()'s returned function is still a standard
// request => response|undefined handler, so it works unmodified as the proxy export.
//
// createRouteMatcher-based protection here is a defense-in-depth net, not the sole guard — Clerk's
// SDK flags path-matching-only middleware as deprecated in favor of resource-based checks, so every
// protected page also calls `auth.protect()` itself (dashboard/page.tsx, connect/github/callback/page.tsx).
const isProtectedRoute = createRouteMatcher(["/dashboard(.*)", "/connect(.*)"]);

export default clerkMiddleware(async (auth, req) => {
  if (isProtectedRoute(req)) {
    await auth.protect();
  }
});

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
