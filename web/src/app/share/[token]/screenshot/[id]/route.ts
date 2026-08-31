/**
 * evidence-sharing: unauthenticated proxy for a redacted screenshot referenced by a shared-evidence
 * link. Streams the PNG from the hosted API's own unauthenticated share route — no Clerk token.
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ token: string; id: string }> },
) {
  const { token, id } = await params;

  const upstream = await fetch(
    `${API_BASE_URL}/api/shared-runs/${encodeURIComponent(token)}/screenshots/${encodeURIComponent(id)}`,
    { cache: "no-store" },
  );

  if (!upstream.ok) {
    return new Response(null, { status: upstream.status });
  }

  return new Response(upstream.body, {
    status: 200,
    headers: {
      "Content-Type": "image/png",
      "Cache-Control": "public, max-age=300",
    },
  });
}
