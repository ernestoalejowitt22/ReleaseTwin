"use server";

import { revalidatePath } from "next/cache";
import { api, ApiError } from "@/lib/api";

export type SetProjectSecretState = { error: string | null; paidTierRequired?: boolean };

export async function setProjectSecret(
  projectId: string,
  _prevState: SetProjectSecretState,
  formData: FormData,
): Promise<SetProjectSecretState> {
  const name = String(formData.get("name") ?? "").trim();
  const value = String(formData.get("value") ?? "");

  if (!name) {
    return { error: "Name is required." };
  }

  try {
    await api.put(`/api/project-secrets/${projectId}/${encodeURIComponent(name)}`, { value });
  } catch (err) {
    if (err instanceof ApiError) {
      // plan-tier-gating convention: a distinct error code, same as createProject's own
      // free-tier-project-limit handling, so the UI can show the upgrade prompt specifically.
      if (err.status === 403 && err.message.includes("entitlement-required")) {
        return { error: "Storing project secrets requires the Team tier.", paidTierRequired: true };
      }
      return { error: err.message || "Could not save the secret." };
    }
    throw err;
  }

  revalidatePath("/dashboard");
  return { error: null };
}

export async function revokeProjectSecret(projectId: string, name: string) {
  await api.del(`/api/project-secrets/${projectId}/${encodeURIComponent(name)}`);
  revalidatePath("/dashboard");
}
