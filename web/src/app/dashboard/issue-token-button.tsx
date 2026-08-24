"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { issueToken } from "./actions";

/**
 * account-provisioning spec: the raw token is shown once at issuance and never retrievable again —
 * this component is the only place in the UI that ever holds it, client-side, for as long as the
 * page stays open.
 */
export function IssueTokenButton({ projectId }: { projectId: string }) {
  const [token, setToken] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  return (
    <div className="flex flex-col gap-2">
      {token && (
        <div className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm">
          <p className="font-medium">New token (shown once, copy it now):</p>
          <code className="break-all">{token}</code>
        </div>
      )}
      <Button
        variant="outline"
        disabled={isPending}
        onClick={() => startTransition(async () => setToken(await issueToken(projectId)))}
      >
        Issue new token
      </Button>
    </div>
  );
}
