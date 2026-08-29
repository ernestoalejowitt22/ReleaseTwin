import { auth } from "@clerk/nextjs/server";

/**
 * BFF proxy for a redacted evidence screenshot blob. The browser never talks to the .NET API
 * directly (BFF pattern) — this route attaches the Clerk session token and streams the PNG back.
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ reportId: string; id: string }> },
) {
  const { getToken } = await auth();
  const token = await getToken();
  if (!token) {
    return new Response("Unauthorized", { status: 401 });
  }

  const { reportId, id } = await params;
  const projectId = new URL(request.url).searchParams.get("projectId");
  if (!projectId) {
    return new Response("projectId is required", { status: 400 });
  }

  const upstream = await fetch(
    `${API_BASE_URL}/api/dashboard/evidence-screenshots/${encodeURIComponent(id)}?projectId=${projectId}&reportId=${reportId}`,
    { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" },
  );

  if (!upstream.ok) {
    return new Response(null, { status: upstream.status });
  }

  return new Response(upstream.body, {
    status: 200,
    headers: {
      "Content-Type": "image/png",
      "Cache-Control": "private, no-store",
    },
  });
}
