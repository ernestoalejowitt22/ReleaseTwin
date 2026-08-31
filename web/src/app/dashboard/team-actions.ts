"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { auth } from "@clerk/nextjs/server";
import { ACTIVE_ORG_COOKIE, api, ApiError } from "@/lib/api";

const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

/** org-membership: switch the viewer's active organization (validated server-side against membership). */
export async function setActiveOrganization(formData: FormData) {
  const orgId = String(formData.get("orgId") ?? "");
  if (orgId) {
    (await cookies()).set(ACTIVE_ORG_COOKIE, orgId, {
      httpOnly: true,
      sameSite: "lax",
      path: "/",
      maxAge: 60 * 60 * 24 * 365,
    });
  }
  redirect("/dashboard");
}

export async function createOrganization(formData: FormData) {
  const name = String(formData.get("name") ?? "");
  const created = await api.post<{ id: string }>("/api/organizations/", { name });
  (await cookies()).set(ACTIVE_ORG_COOKIE, created.id, {
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
  });
  redirect("/dashboard");
}

export type InviteState = { error: string | null; sent?: boolean; acceptUrl?: string };

export async function inviteMember(
  organizationId: string,
  _prev: InviteState,
  formData: FormData,
): Promise<InviteState> {
  const email = String(formData.get("email") ?? "").trim();
  const role = String(formData.get("role") ?? "Member");
  if (!email) {
    return { error: "An email address is required." };
  }
  try {
    const res = await api.post<{ acceptUrl: string }>(
      `/api/organizations/${organizationId}/invitations`,
      { email, role },
    );
    revalidatePath("/dashboard/members");
    return { error: null, sent: true, acceptUrl: res.acceptUrl };
  } catch (err) {
    if (err instanceof ApiError) {
      if (err.status === 403) return { error: "Only an admin can invite teammates." };
      return { error: err.message || "Could not send the invitation." };
    }
    throw err;
  }
}

export async function revokeInvitation(organizationId: string, token: string) {
  await api.del(`/api/organizations/${organizationId}/invitations/${encodeURIComponent(token)}`);
  revalidatePath("/dashboard/members");
}

export async function changeMemberRole(organizationId: string, userId: string, formData: FormData) {
  const role = String(formData.get("role") ?? "Member");
  await api.patch<void>(`/api/organizations/${organizationId}/members/${userId}`, { role });
  revalidatePath("/dashboard/members");
}

export async function removeMember(organizationId: string, userId: string) {
  await api.del(`/api/organizations/${organizationId}/members/${userId}`);
  revalidatePath("/dashboard/members");
}

export type AcceptInviteState = { error: string | null };

/**
 * org-membership (design D1a): accepts an invitation. Uses a direct fetch (not the `api` helper) so
 * it can attach `X-Invite-Token` — the signal that makes provisioning skip minting a throwaway org
 * for a user who is only signing up to join an existing one.
 */
export async function acceptInvitation(
  token: string,
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- useActionState passes prevState; unused here
  _state: AcceptInviteState,
): Promise<AcceptInviteState> {
  const { getToken } = await auth();
  const clerkToken = await getToken();

  const response = await fetch(
    `${API_BASE_URL}/api/invitations/${encodeURIComponent(token)}/accept`,
    {
      method: "POST",
      cache: "no-store",
      headers: {
        ...(clerkToken ? { Authorization: `Bearer ${clerkToken}` } : {}),
        "X-Invite-Token": token,
      },
    },
  );

  if (response.status === 409) {
    return { error: "This invitation is no longer valid." };
  }
  if (!response.ok) {
    return { error: "Could not accept this invitation. Try the link again." };
  }

  const result = (await response.json()) as { organizationId: string };
  (await cookies()).set(ACTIVE_ORG_COOKIE, result.organizationId, {
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
  });
  redirect("/dashboard");
}
