"use client";

import { useActionState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { setAdapterCredential, revokeAdapterCredential, type SetAdapterCredentialState } from "./adapter-credentials-actions";

interface FieldSpec {
  name: string;
  label: string;
  type?: "text" | "password";
}

/**
 * hosted-adapter-credentials design.md: a small, labeled form per known adapter (not a generic
 * key/value editor — there are exactly two known adapters today with fixed, well-known field sets).
 * Submitted values are never redisplayed once saved (adapter-credentials spec) — this form always
 * renders blank, whether or not a credential is already configured for this project.
 */
export function AdapterCredentialForm({
  projectId,
  adapter,
  title,
  fields,
  configuredMetadata,
}: {
  projectId: string;
  adapter: string;
  title: string;
  fields: FieldSpec[];
  configuredMetadata: { lastSetByDisplayName: string; updatedAt: string } | null;
}) {
  const [state, formAction, isPending] = useActionState<SetAdapterCredentialState, FormData>(
    setAdapterCredential.bind(null, projectId, adapter, fields.map((f) => f.name)),
    { error: null },
  );
  const [isRevoking, startRevoke] = useTransition();

  return (
    <div className="flex flex-col gap-2 rounded-lg border p-3">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium">{title}</p>
        {configuredMetadata ? (
          <span className="text-xs text-muted-foreground">
            Configured by {configuredMetadata.lastSetByDisplayName} on {new Date(configuredMetadata.updatedAt).toLocaleString()}
          </span>
        ) : (
          <span className="text-xs text-muted-foreground">Not configured</span>
        )}
      </div>

      <form action={formAction} className="flex flex-col gap-2">
        {fields.map((field) => (
          <div key={field.name} className="flex items-center gap-2">
            <label className="w-36 shrink-0 text-xs text-muted-foreground">{field.label}</label>
            <Input type={field.type ?? "text"} name={field.name} placeholder={configuredMetadata ? "•••••• (leave to overwrite)" : ""} />
          </div>
        ))}
        {state.error && <p className="text-sm text-destructive">{state.error}</p>}
        <div className="flex gap-2">
          <Button type="submit" size="sm" disabled={isPending}>
            {isPending ? "Saving…" : configuredMetadata ? "Rotate" : "Save"}
          </Button>
          {configuredMetadata && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isRevoking}
              onClick={() => startRevoke(() => revokeAdapterCredential(projectId, adapter))}
            >
              Revoke
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}
