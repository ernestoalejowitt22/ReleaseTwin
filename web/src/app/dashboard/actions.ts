"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";
import type { GitHubAuthorizeResult } from "@/lib/types";

export async function createProject(formData: FormData) {
  const name = String(formData.get("name") ?? "");
  const created = await api.post<{ id: string }>("/api/dashboard/projects", { name });
  redirect(`/dashboard?projectId=${created.id}`);
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
