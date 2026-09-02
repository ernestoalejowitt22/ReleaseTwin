import { cookies } from "next/headers";
import { auth } from "@clerk/nextjs/server";
import { ACTIVE_ORG_COOKIE } from "@/lib/api";

/**
 * Operator setup helper: opens `GET /api/me` while signed in and returns the JSON the API resolved
 * from your Clerk session token. Use it to (1) copy `clerkUserId` into the `ADMIN_OPERATOR_USER_IDS`
 * repo variable and (2) confirm `email` is populated (invitation acceptance needs a verified email
 * claim). No secrets in the response.
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

export async function GET() {
  const { getToken } = await auth();
  const token = await getToken();
  if (!token) {
    return new Response("Unauthorized", { status: 401 });
  }
  const activeOrg = (await cookies()).get(ACTIVE_ORG_COOKIE)?.value;

  const upstream = await fetch(`${API_BASE_URL}/api/me`, {
    cache: "no-store",
    headers: {
      Authorization: `Bearer ${token}`,
      ...(activeOrg ? { "X-Org-Id": activeOrg } : {}),
    },
  });

  return new Response(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": "application/json", "Cache-Control": "no-store" },
  });
}
