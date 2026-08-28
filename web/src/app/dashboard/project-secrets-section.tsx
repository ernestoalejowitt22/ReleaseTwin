"use client";

import { useActionState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import type { ProjectSecretSummary } from "@/lib/types";
import { setProjectSecret, revokeProjectSecret, type SetProjectSecretState } from "./project-secrets-actions";

/**
 * hosted-project-secrets design.md: unlike AdapterCredentialForm (a fixed, known field set per
 * adapter), a project secret's name is customer-chosen — so this is a generic name/value editor for
 * new secrets, plus one rotate-only row per already-stored secret (name fixed, value re-enterable).
 * Submitted values are never redisplayed once saved, same convention adapter-credentials already
 * established.
 */
function SecretRow({ projectId, name, configuredMetadata }: {
  projectId: string;
  name: string;
  configuredMetadata: { lastSetByDisplayName: string; updatedAt: string } | null;
}) {
  const [state, formAction, isPending] = useActionState<SetProjectSecretState, FormData>(
    setProjectSecret.bind(null, projectId),
    { error: null },
  );
  const [isRevoking, startRevoke] = useTransition();

  return (
    <div className="flex flex-col gap-2 rounded-lg border p-3">
      <div className="flex items-center justify-between">
        <p className="font-mono text-sm font-medium">{name}</p>
        {configuredMetadata && (
          <span className="flex items-center gap-2 text-xs text-muted-foreground">
            <Badge variant="default">Configured</Badge>
            by {configuredMetadata.lastSetByDisplayName} on {new Date(configuredMetadata.updatedAt).toLocaleString()}
          </span>
        )}
      </div>

      <form action={formAction} className="flex items-center gap-2">
        <input type="hidden" name="name" value={name} />
        <Input type="password" name="value" placeholder="•••••• (leave to overwrite)" className="flex-1" />
        {state.error && <p className="text-sm text-destructive">{state.error}</p>}
        <Button type="submit" size="sm" disabled={isPending}>
          {isPending ? "Rotating…" : "Rotate"}
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={isRevoking}
          onClick={() => startRevoke(() => revokeProjectSecret(projectId, name))}
        >
          Revoke
        </Button>
      </form>
    </div>
  );
}

function AddSecretForm({ projectId }: { projectId: string }) {
  const [state, formAction, isPending] = useActionState<SetProjectSecretState, FormData>(
    setProjectSecret.bind(null, projectId),
    { error: null },
  );

  return (
    <form action={formAction} className="flex items-center gap-2">
      <Input name="name" placeholder="SECRET_NAME" className="w-48 font-mono" />
      <Input type="password" name="value" placeholder="value" className="flex-1" />
      <Button type="submit" size="sm" disabled={isPending}>
        {isPending ? "Saving…" : "Add secret"}
      </Button>
      {state.error && <p className="text-sm text-destructive">{state.error}</p>}
    </form>
  );
}

export function ProjectSecretsSection({
  projectId,
  secrets,
  isPaidTier,
}: {
  projectId: string;
  secrets: ProjectSecretSummary[];
  isPaidTier: boolean;
}) {
  if (!isPaidTier) {
    // plan-tier-gating: locked, not a bare error only surfacing after a failed submit — the upgrade
    // control itself already lives in the usage card above.
    return (
      <p className="text-sm text-muted-foreground">
        Requires the Paid tier — upgrade above to store secrets your journeys can reference as{" "}
        <code>{"${VAR_NAME}"}</code>.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {secrets.map((secret) => (
        <SecretRow
          key={secret.name}
          projectId={projectId}
          name={secret.name}
          configuredMetadata={{ lastSetByDisplayName: secret.lastSetByDisplayName, updatedAt: secret.updatedAt }}
        />
      ))}
      <AddSecretForm projectId={projectId} />
    </div>
  );
}
