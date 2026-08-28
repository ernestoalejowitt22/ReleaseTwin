"use client";

import { useState } from "react";
import { ChevronDown, ChevronRight, Settings2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import type { AdapterCredentialSummary, DashboardConnectionView, ProjectSecretSummary } from "@/lib/types";
import { AdapterCredentialForm } from "./adapter-credential-form";
import { ProjectSecretsSection } from "./project-secrets-section";

/**
 * dashboard-visual-refresh design.md: "Set up" collapses to a single summary line once at least one
 * of Connection/Adapter-credentials/Project-secrets has something configured, expanded by default
 * only for a brand-new project with nothing set up yet. A single derived boolean, recomputed from
 * server data on every load — no persistence.
 */
export function SetupSection({
  projectId,
  projectName,
  connection,
  adapterCredentials,
  projectSecrets,
  isPaidTier,
  disconnectConnection,
  startGitHubConnection,
}: {
  projectId: string;
  projectName: string;
  connection: DashboardConnectionView | null;
  adapterCredentials: AdapterCredentialSummary[];
  projectSecrets: ProjectSecretSummary[];
  isPaidTier: boolean;
  disconnectConnection: () => Promise<void>;
  startGitHubConnection: () => Promise<void>;
}) {
  const adapterCredentialByName = new Map(adapterCredentials.map((c) => [c.adapter, c]));
  const configuredCount = (connection ? 1 : 0) + adapterCredentials.length + projectSecrets.length;
  const [open, setOpen] = useState(configuredCount === 0);

  const summaryParts = [
    connection ? `GitHub (${connection.externalRepo})` : null,
    ...adapterCredentials.map((c) => c.adapter),
    ...projectSecrets.map((s) => s.name),
  ].filter((part): part is string => Boolean(part));

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-1.5 text-sm font-semibold tracking-wide text-muted-foreground uppercase">
          <Settings2 className="size-4" />
          Set up
        </h2>
        <CollapsibleTrigger asChild>
          <Button variant="ghost" size="sm" className="gap-1.5 text-xs">
            {open ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}
            {configuredCount === 0
              ? "Nothing configured yet"
              : `${configuredCount} configured${summaryParts.length ? ` — ${summaryParts.join(", ")}` : ""}`}
          </Button>
        </CollapsibleTrigger>
      </div>

      <CollapsibleContent className="flex flex-col gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Connection — {projectName}</CardTitle>
          </CardHeader>
          <CardContent>
            {connection ? (
              <div className="flex items-center justify-between">
                <p>
                  Connected to <code>{connection.externalRepo}</code> ({connection.provider})
                </p>
                <form action={disconnectConnection}>
                  <Button variant="outline" type="submit">
                    Disconnect
                  </Button>
                </form>
              </div>
            ) : (
              <div className="flex items-center justify-between">
                <CardDescription>
                  Not connected to a repository yet — labeling only, no code or credentials are ever
                  read.
                </CardDescription>
                <form action={startGitHubConnection}>
                  <Button type="submit">Connect GitHub</Button>
                </form>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Adapter credentials — {projectName}</CardTitle>
            <CardDescription>
              Let the CLI fetch these instead of setting them as environment variables wherever it
              runs — environment variables still take precedence when both are present.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <AdapterCredentialForm
              projectId={projectId}
              adapter="azure-devops"
              title="Azure DevOps"
              fields={[
                { name: "org", label: "Organization" },
                { name: "project", label: "Project" },
                { name: "pat", label: "Personal access token", type: "password" },
                { name: "areaPath", label: "Area path" },
                { name: "variableGroupId", label: "Variable group ID" },
              ]}
              configuredMetadata={adapterCredentialByName.get("azure-devops") ?? null}
            />
            <AdapterCredentialForm
              projectId={projectId}
              adapter="launchdarkly"
              title="LaunchDarkly"
              fields={[
                { name: "apiToken", label: "API token", type: "password" },
                { name: "projectKey", label: "Project key" },
                { name: "environmentKey", label: "Environment key" },
              ]}
              configuredMetadata={adapterCredentialByName.get("launchdarkly") ?? null}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Project secrets — {projectName}</CardTitle>
            <CardDescription>
              Arbitrary named values a journey or case step can reference as{" "}
              <code>{"${VAR_NAME}"}</code> — the local environment always takes precedence when both
              are present.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ProjectSecretsSection projectId={projectId} secrets={projectSecrets} isPaidTier={isPaidTier} />
          </CardContent>
        </Card>
      </CollapsibleContent>
    </Collapsible>
  );
}
