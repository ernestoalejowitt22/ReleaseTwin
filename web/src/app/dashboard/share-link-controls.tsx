"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import type { ShareLinkSummary } from "@/lib/types";
import { createShareLink, revokeShareLink } from "./share-actions";

/**
 * evidence-sharing: create / copy / revoke read-only links to this run's redacted evidence.
 * Team-gated — a Free session sees an upgrade hint instead.
 */
export function ShareLinkControls({
  reportId,
  projectId,
  entitled,
  canManage,
  links,
}: {
  reportId: string;
  projectId: string;
  entitled: boolean;
  canManage: boolean;
  links: readonly ShareLinkSummary[];
}) {
  const [newUrl, setNewUrl] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [isPending, startTransition] = useTransition();

  if (!entitled) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Share this evidence</CardTitle>
          <CardDescription>
            Send a revocable, read-only link to this run&apos;s redacted evidence — no account needed
            to view it. Available on the Team plan.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const active = links.filter((l) => l.state === "Active");

  return (
    <Card>
      <CardHeader>
        <CardTitle>Share this evidence</CardTitle>
        <CardDescription>
          A read-only link to the redacted evidence above. Revoke it any time; it also expires on its
          own.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {newUrl && (
          <div className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm">
            <p className="font-medium">Link created (the token is shown once):</p>
            <code className="break-all">{newUrl}</code>
            <Button
              variant="outline"
              size="sm"
              className="mt-2"
              onClick={() => {
                navigator.clipboard.writeText(newUrl);
                setCopied(true);
              }}
            >
              {copied ? "Copied" : "Copy link"}
            </Button>
          </div>
        )}

        {active.length > 0 && (
          <ul className="flex flex-col gap-1 text-sm">
            {active.map((l) => (
              <li key={l.id} className="flex items-center justify-between gap-2">
                <span className="text-muted-foreground">
                  created {new Date(l.createdAt).toLocaleDateString()} · expires{" "}
                  {new Date(l.expiresAt).toLocaleDateString()}
                </span>
                {canManage && (
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={isPending}
                    onClick={() =>
                      startTransition(() => revokeShareLink(reportId, projectId, l.id))
                    }
                  >
                    Revoke
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}

        {canManage && (
          <Button
            variant="outline"
            disabled={isPending}
            onClick={() =>
              startTransition(async () => {
                setCopied(false);
                setNewUrl(await createShareLink(reportId, projectId));
              })
            }
          >
            Create share link
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
