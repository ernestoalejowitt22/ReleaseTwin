"use server";

import { revalidatePath } from "next/cache";
import { api, ApiError } from "@/lib/api";
import type { ShareLinkSummary } from "@/lib/types";

export type ShareLinksResult =
  | { entitled: true; links: ShareLinkSummary[] }
  | { entitled: false; links: [] };

export async function loadShareLinks(
  reportId: string,
  projectId: string,
): Promise<ShareLinksResult> {
  try {
    const links = await api.get<ShareLinkSummary[]>(
      `/api/reports/${reportId}/share-links/?projectId=${projectId}`,
    );
    return { entitled: true, links };
  } catch (err) {
    if (err instanceof ApiError && err.status === 403) {
      return { entitled: false, links: [] };
    }
    throw err;
  }
}

export async function createShareLink(reportId: string, projectId: string): Promise<string> {
  const result = await api.post<{ url: string }>(
    `/api/reports/${reportId}/share-links/?projectId=${projectId}`,
    {},
  );
  revalidatePath(`/dashboard/reports/${reportId}/evidence`);
  return result.url;
}

export async function revokeShareLink(reportId: string, projectId: string, linkId: string) {
  await api.del(`/api/reports/${reportId}/share-links/${linkId}?projectId=${projectId}`);
  revalidatePath(`/dashboard/reports/${reportId}/evidence`);
}
