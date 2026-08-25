"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { api, ApiError } from "@/lib/api";
import type { GitHubAuthorizeResult } from "@/lib/types";

export async function createProject(formData: FormData) {
  const name = String(formData.get("name") ?? "");

  // redirect() throws internally to interrupt rendering — keep it out of this try/catch, which is
  // scoped to just the API call, so that throw is never mistaken for the error being caught below.
  let created: { id: string };
  try {
    created = await api.post<{ id: string }>("/api/dashboard/projects", { name });
  } catch (err) {
    // plan-tier-gating design.md: distinct from a generic error so the customer sees the actual
    // reason and the Upgrade control, not just "something went wrong".
    if (err instanceof ApiError && err.status === 403 && err.message.includes("free-tier-project-limit")) {
      redirect(`/dashboard?projectLimitError=${encodeURIComponent("Free plan is limited to 1 project — upgrade to create more.")}`);
    }
    throw err;
  }

  redirect(`/dashboard?projectId=${created.id}`);
}

export async function upgradeOrganization() {
  await api.post("/api/dashboard/upgrade");
  revalidatePath("/dashboard");
}

export async function issueToken(projectId: string) {
  const result = await api.post<{ token: string }>(`/api/dashboard/projects/${projectId}/tokens`);
  revalidatePath("/dashboard");
  return result.token;
}

export async function revokeToken(projectId: string, tokenId: string) {
  await api.del(`/api/dashboard/projects/${projectId}/tokens/${tokenId}`);
  revalidatePath("/dashboard");
}

export async function disconnectConnection(projectId: string) {
  await api.del(`/api/dashboard/projects/${projectId}/connection`);
  revalidatePath("/dashboard");
}

export async function startGitHubConnection(projectId: string) {
  const result = await api.post<GitHubAuthorizeResult>("/api/connections/start", { projectId });
  if (!result.configured || !result.authorizeUrl) {
    redirect(`/dashboard?projectId=${projectId}&connectionError=${encodeURIComponent("GitHub connections are not configured yet.")}`);
  }
  redirect(result.authorizeUrl);
}
