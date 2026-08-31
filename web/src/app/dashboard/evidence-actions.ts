"use server";

import { revalidatePath } from "next/cache";
import { api, ApiError } from "@/lib/api";

export type SetEvidenceConfigState = { error: string | null; paidTierRequired?: boolean; saved?: boolean };

export async function setEvidenceConfig(
  projectId: string,
  _prevState: SetEvidenceConfigState,
  formData: FormData,
): Promise<SetEvidenceConfigState> {
  const captureDefault = formData.get("captureDefault") === "on";
  const retentionDays = Number(formData.get("retentionDays") ?? "");

  if (!Number.isInteger(retentionDays) || retentionDays < 1) {
    return { error: "Retention window must be a whole number of days." };
  }

  try {
    await api.put(`/api/projects/${projectId}/evidence-config`, { captureDefault, retentionDays });
  } catch (err) {
    if (err instanceof ApiError) {
      if (err.status === 403 && err.message.includes("entitlement-required")) {
        return { error: "Evidence capture requires the Team tier.", paidTierRequired: true };
      }
      return { error: err.message || "Could not save evidence settings." };
    }
    throw err;
  }

  revalidatePath("/dashboard");
  return { error: null, saved: true };
}
