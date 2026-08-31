"use server";

import { revalidatePath } from "next/cache";
import { api, ApiError } from "@/lib/api";

export type NotificationTargetState = { error: string | null; added?: boolean };

export async function addNotificationTarget(
  projectId: string,
  _prev: NotificationTargetState,
  formData: FormData,
): Promise<NotificationTargetState> {
  const kind = String(formData.get("kind") ?? "Slack");
  const url = String(formData.get("url") ?? "").trim();
  if (!url) {
    return { error: "A webhook URL is required." };
  }
  try {
    await api.post(`/api/projects/${projectId}/notification-targets/`, { kind, url });
  } catch (err) {
    if (err instanceof ApiError) {
      if (err.status === 400 && err.message.includes("invalid-url")) {
        return { error: "That URL was rejected — it must be https and must not point at a private address." };
      }
      if (err.status === 403) {
        return { error: "Run notifications require the Team plan." };
      }
      return { error: err.message || "Could not add the notification target." };
    }
    throw err;
  }
  revalidatePath("/dashboard");
  return { error: null, added: true };
}

export async function setNotificationTargetEnabled(
  projectId: string,
  targetId: string,
  enabled: boolean,
) {
  await api.patch(`/api/projects/${projectId}/notification-targets/${targetId}`, { enabled });
  revalidatePath("/dashboard");
}

export async function deleteNotificationTarget(projectId: string, targetId: string) {
  await api.del(`/api/projects/${projectId}/notification-targets/${targetId}`);
  revalidatePath("/dashboard");
}
