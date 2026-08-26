"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";

export async function createJourney(projectId: string, formData: FormData) {
  const name = String(formData.get("name") ?? "");
  const created = await api.post<{ id: string }>("/api/journeys", { projectId, name });
  redirect(`/journeys/${created.id}?projectId=${projectId}`);
}

export async function saveJourneyVersion(journeyId: string, projectId: string, yamlContent: string) {
  const version = await api.post<{ version: number }>(
    `/api/journeys/${journeyId}/versions?projectId=${projectId}`,
    { yamlContent },
  );
  revalidatePath(`/journeys/${journeyId}`);
  return version.version;
}
