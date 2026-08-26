"use server";

import { revalidatePath } from "next/cache";
import { api, ApiError } from "@/lib/api";

export type SetAdapterCredentialState = { error: string | null };

export async function setAdapterCredential(
  projectId: string,
  adapter: string,
  fieldNames: string[],
  _prevState: SetAdapterCredentialState,
  formData: FormData,
): Promise<SetAdapterCredentialState> {
  const fields = Object.fromEntries(fieldNames.map((name) => [name, String(formData.get(name) ?? "")]));

  try {
    await api.put(`/api/adapter-credentials/${projectId}/${adapter}`, { fields });
  } catch (err) {
    if (err instanceof ApiError) {
      return { error: err.message || "Could not save credentials." };
    }
    throw err;
  }

  revalidatePath("/dashboard");
  return { error: null };
}

export async function revokeAdapterCredential(projectId: string, adapter: string) {
  await api.del(`/api/adapter-credentials/${projectId}/${adapter}`);
  revalidatePath("/dashboard");
}
