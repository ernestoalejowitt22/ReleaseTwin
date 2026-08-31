"use client";

import { useActionState, useTransition } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { NotificationTarget } from "@/lib/types";
import {
  addNotificationTarget,
  deleteNotificationTarget,
  setNotificationTargetEnabled,
  type NotificationTargetState,
} from "./notification-actions";

/**
 * run-notifications: per-project outbound targets. Team-gated — a Free / non-admin session gets an
 * upgrade hint instead of the controls.
 */
export function NotificationTargetsSection({
  projectId,
  entitled,
  canManage,
  targets,
}: {
  projectId: string;
  entitled: boolean;
  canManage: boolean;
  targets: NotificationTarget[];
}) {
  const [state, action] = useActionState<NotificationTargetState, FormData>(
    addNotificationTarget.bind(null, projectId),
    { error: null },
  );
  const [isPending, startTransition] = useTransition();

  if (!entitled) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Run notifications</CardTitle>
          <CardDescription>
            Get a Slack or webhook alert when a run fails or a flag proof doesn&apos;t discriminate.
            Available on the Team plan.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Run notifications</CardTitle>
        <CardDescription>
          Fired on a failed run or a failed / ineligible flag proof. The payload carries only the
          result and a link back — never fixture content or secrets.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {targets.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Kind</TableHead>
                <TableHead>URL</TableHead>
                <TableHead>Last delivery</TableHead>
                <TableHead>Enabled</TableHead>
                {canManage && <TableHead />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {targets.map((t) => (
                <TableRow key={t.id}>
                  <TableCell>{t.kind}</TableCell>
                  <TableCell className="max-w-[16rem] truncate">
                    <code className="text-xs">{t.url}</code>
                  </TableCell>
                  <TableCell>
                    {t.lastAttemptAt ? (
                      <span className="text-xs">
                        <Badge variant={t.lastOutcome === "success" ? "default" : "destructive"}>
                          {t.lastOutcome ?? "—"}
                        </Badge>{" "}
                        {new Date(t.lastAttemptAt).toLocaleString()}
                      </span>
                    ) : (
                      <span className="text-xs text-muted-foreground">never</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {canManage ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={isPending}
                        onClick={() =>
                          startTransition(() =>
                            setNotificationTargetEnabled(projectId, t.id, !t.enabled),
                          )
                        }
                      >
                        {t.enabled ? "On" : "Off"}
                      </Button>
                    ) : (
                      <Badge variant={t.enabled ? "default" : "secondary"}>
                        {t.enabled ? "On" : "Off"}
                      </Badge>
                    )}
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={isPending}
                        onClick={() =>
                          startTransition(() => deleteNotificationTarget(projectId, t.id))
                        }
                      >
                        Delete
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {canManage && (
          <form action={action} className="flex flex-wrap items-center gap-2">
            <select
              name="kind"
              defaultValue="Slack"
              className="h-9 rounded-md border bg-transparent px-2 text-sm"
            >
              <option value="Slack">Slack</option>
              <option value="Webhook">Webhook</option>
            </select>
            <Input type="url" name="url" placeholder="https://hooks.slack.com/services/…" required />
            <Button type="submit">Add target</Button>
          </form>
        )}
        {state.error && <p className="text-sm text-destructive">{state.error}</p>}
      </CardContent>
    </Card>
  );
}
