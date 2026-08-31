"use client";

import { useActionState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import type { EvidenceConfigView } from "@/lib/types";
import { setEvidenceConfig, type SetEvidenceConfigState } from "./evidence-actions";

/**
 * evidence-capture / evidence-store: per-project control to enable the CLI's evidence-capture
 * default and set the retention window. Paid tier only; Free-tier organizations see it disabled
 * with the reason.
 */
export function EvidenceSettingsSection({
  projectId,
  config,
}: {
  projectId: string;
  config: EvidenceConfigView;
}) {
  const [state, formAction, isPending] = useActionState<SetEvidenceConfigState, FormData>(
    setEvidenceConfig.bind(null, projectId),
    { error: null },
  );

  return (
    <Card data-testid="evidence-settings">
      <CardHeader>
        <CardTitle>Evidence capture</CardTitle>
        <CardDescription>
          When enabled, CLI runs for this project capture per-step evidence, redact it in your CLI,
          and upload it here. Retention window in effect: {config.retentionDays} days.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {!config.available ? (
          <p className="text-sm text-muted-foreground">
            Evidence capture requires the Team tier. Upgrade your organization to enable it.
          </p>
        ) : (
          <form action={formAction} className="flex flex-col gap-3">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                name="captureDefault"
                defaultChecked={config.captureDefault}
              />
              Capture evidence by default for this project&apos;s CLI runs
            </label>
            <label className="flex items-center gap-2 text-sm">
              Retention window (days, max {config.maxRetentionDays}):
              <Input
                type="number"
                name="retentionDays"
                min={1}
                max={config.maxRetentionDays}
                defaultValue={config.retentionDays}
                className="w-24"
              />
            </label>
            {state.error && <p className="text-sm text-destructive">{state.error}</p>}
            {state.saved && <p className="text-sm text-muted-foreground">Saved.</p>}
            <Button type="submit" size="sm" disabled={isPending} className="self-start">
              {isPending ? "Saving…" : "Save"}
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}
