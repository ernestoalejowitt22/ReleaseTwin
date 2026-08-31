import { cookies } from "next/headers";
import { auth } from "@clerk/nextjs/server";

/** org-membership: cookie holding the viewer's chosen active organization. Forwarded to the API as
 * `X-Org-Id`, which the API honours only if the caller is a member of that org. */
export const ACTIVE_ORG_COOKIE = "rt_active_org";

/**
 * hosted-react-frontend design.md: BFF pattern — only this server-side helper ever calls the
 * .NET API. The browser never sees RELEASETWIN_API_URL or a Clerk session token directly.
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const { getToken } = await auth();
  const token = await getToken();
  const activeOrg = (await cookies()).get(ACTIVE_ORG_COOKIE)?.value;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(activeOrg ? { "X-Org-Id": activeOrg } : {}),
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.text().catch(() => "");
    throw new ApiError(response.status, body || response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PUT", body: body ? JSON.stringify(body) : undefined }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PATCH", body: body ? JSON.stringify(body) : undefined }),
  del: <T>(path: string) => request<T>(path, { method: "DELETE" }),
};
