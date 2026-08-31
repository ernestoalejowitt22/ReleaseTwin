import { cookies } from "next/headers";
import { auth } from "@clerk/nextjs/server";
import { ACTIVE_ORG_COOKIE } from "@/lib/api";

/**
 * data-export: BFF proxy for "Download your data". Calls the hosted API's admin-gated
 * `POST /api/export`. In production the API returns `{ downloadUrl }` (a short-lived S3 presigned
 * URL) — redirect the browser to it. In local dev with no archive store the API streams the ZIP —
 * pass it straight through as a download.
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

export async function GET() {
  const { getToken } = await auth();
  const token = await getToken();
  if (!token) {
    return new Response("Unauthorized", { status: 401 });
  }
  const activeOrg = (await cookies()).get(ACTIVE_ORG_COOKIE)?.value;

  const upstream = await fetch(`${API_BASE_URL}/api/export`, {
    method: "POST",
    cache: "no-store",
    headers: {
      Authorization: `Bearer ${token}`,
      ...(activeOrg ? { "X-Org-Id": activeOrg } : {}),
    },
  });

  if (!upstream.ok) {
    return new Response(upstream.status === 403 ? "Only an admin can export organization data." : "Export failed.", {
      status: upstream.status,
    });
  }

  const contentType = upstream.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    const { downloadUrl } = (await upstream.json()) as { downloadUrl: string };
    return Response.redirect(downloadUrl, 303);
  }

  return new Response(upstream.body, {
    status: 200,
    headers: {
      "Content-Type": "application/zip",
      "Content-Disposition": upstream.headers.get("content-disposition") ?? 'attachment; filename="releasetwin-export.zip"',
      "Cache-Control": "no-store",
    },
  });
}
