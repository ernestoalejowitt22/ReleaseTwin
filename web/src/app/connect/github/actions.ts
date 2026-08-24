"use server";

import { redirect } from "next/navigation";
import { api } from "@/lib/api";

export async function confirmConnection(formData: FormData) {
  const projectId = String(formData.get("projectId") ?? "");
  const externalRepo = String(formData.get("externalRepo") ?? "");
  await api.post("/api/connections/confirm", { projectId, externalRepo });
  redirect(`/dashboard?projectId=${projectId}`);
}
