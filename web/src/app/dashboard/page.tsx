import Link from "next/link";
import { UserButton } from "@clerk/nextjs";
import { auth } from "@clerk/nextjs/server";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { ThemeToggle } from "@/components/theme-toggle";
import { PlayCircle, ListChecks, LayoutDashboard, TrendingUp } from "lucide-react";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import type {
  AdapterCredentialSummary,
  DashboardView,
  EvidenceConfigView,
  GuidedSetupView,
  MyOrganization,
  NotificationTarget,
  ProjectSecretSummary,
} from "@/lib/types";
import { FlagSeamSmoke } from "./flag-seam-smoke";
import { OrgSwitcher } from "./org-switcher";
import { NotificationTargetsSection } from "./notification-targets-section";
import { IssueTokenButton } from "./issue-token-button";
import { SetupSection } from "./setup-section";
import { EvidenceSettingsSection } from "./evidence-settings-section";
import { ReleasesSection } from "./releases-section";
import {
  createProject,
  disconnectConnection,
  openBillingPortal,
  revokeToken,
  startGitHubConnection,
  upgradeOrganization,
} from "./actions";

export default async function DashboardPage({
  searchParams,
}: {
  searchParams: Promise<{
    projectId?: string;
    connectionError?: string;
    projectLimitError?: string;
    release?: string;
    releaseWindow?: string;
  }>;
}) {
  await auth.protect();

  const { projectId, connectionError, projectLimitError, release, releaseWindow } =
    await searchParams;
  const view = await api.get<DashboardView>(
    `/api/dashboard${projectId ? `?projectId=${projectId}` : ""}`,
  );
  const organizations = await api.get<MyOrganization[]>("/api/me/organizations");
  const myRole = organizations.find((o) => o.active)?.role ?? "Admin";
  const canManage = myRole === "Admin";
  const canUseProjects = myRole !== "Viewer";
  const selectedProject = view.selectedProject;
  // onboarding-activation: the seeded sample project is read-only and has no real config/tokens —
  // skip the per-project fetches that would 403 for it.
  const isExample = selectedProject?.isExample ?? false;
  const realProject = selectedProject && !isExample ? selectedProject : null;
  const adapterCredentials = realProject
    ? await api.get<AdapterCredentialSummary[]>(`/api/adapter-credentials/${realProject.id}`)
    : [];
  const projectSecrets = realProject
    ? await api.get<ProjectSecretSummary[]>(`/api/project-secrets/${realProject.id}`)
    : [];
  const evidenceConfig = realProject
    ? await api.get<EvidenceConfigView>(`/api/projects/${realProject.id}/evidence-config`)
    : null;
  const notificationTargets =
    realProject && canManage && view.entitlements.runNotifications
      ? await api.get<NotificationTarget[]>(
          `/api/projects/${realProject.id}/notification-targets/`,
        )
      : [];

  return (
    <main
      data-org-id={view.organizationId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 p-6"
    >
      <FlagSeamSmoke />
      <header className="flex items-center justify-between">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <LayoutDashboard className="size-6" />
          Dashboard
        </h1>
        <div className="flex items-center gap-2">
          <OrgSwitcher organizations={organizations} />
          <Link
            href={`/dashboard/trends${selectedProject ? `?projectId=${selectedProject.id}` : ""}`}
          >
            <Button variant="outline" size="sm" className="gap-1.5">
              <TrendingUp className="size-4" />
              Trends
            </Button>
          </Link>
          <ThemeToggle />
          <UserButton />
        </div>
      </header>

      {connectionError && (
        <div className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
          {connectionError}
        </div>
      )}

      {projectLimitError && (
        <div className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
          {projectLimitError}
        </div>
      )}

      {(view.hasReadOnlyProjects || view.billingStatus !== "Active") && (
        <div className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm">
          <p className="font-medium">
            {view.billingStatus === "PastDue"
              ? "Your last payment didn't go through."
              : view.billingStatus === "Canceled"
                ? "Your subscription has been canceled."
                : "Some projects are read-only."}
          </p>
          <p className="text-muted-foreground">
            Projects beyond your current plan&apos;s limit stay visible with all their
            evidence, but can&apos;t accept new runs until you&apos;re back under the limit.
          </p>
          {view.billingEnabled && (
            <div className="mt-2">
              {view.hasBillingLinkage ? (
                <form action={openBillingPortal}>
                  <Button variant="outline" size="sm" type="submit">
                    Manage billing
                  </Button>
                </form>
              ) : (
                <UpgradeControl />
              )}
            </div>
          )}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Usage this month</CardTitle>
          <CardDescription>
            Across every project in your organization — not just the one selected below.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex gap-6">
            <div>
              <p className="text-2xl font-semibold">{view.usage.caseReportCount}</p>
              <p className="text-sm text-muted-foreground">case reports</p>
            </div>
            <div>
              <p className="text-2xl font-semibold">{view.usage.flagProofReportCount}</p>
              <p className="text-sm text-muted-foreground">flag-proof reports</p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-3 border-t pt-4">
            <Badge variant={view.planTier === "Free" ? "secondary" : "default"}>
              {view.planTier} plan
            </Badge>
            {view.planTier === "Free" && !view.hasBillingLinkage && (
              <>
                <p className="text-sm text-muted-foreground">Limited to 1 project.</p>
                {view.billingEnabled && <UpgradeControl />}
              </>
            )}
            {view.hasBillingLinkage && (
              <>
                <p className="text-sm text-muted-foreground">
                  {view.billingCadence === "Annual" ? "Renews annually" : "Renews monthly"} · billed
                  per project through our payment provider.
                </p>
                {view.billingEnabled && (
                  <form action={openBillingPortal}>
                    <Button variant="outline" size="sm" type="submit">
                      Manage billing
                    </Button>
                  </form>
                )}
              </>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Projects</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <ul className="flex flex-col gap-1">
            {view.projects.map((project) => (
              <li key={project.id} className="flex items-center gap-2">
                <Link
                  href={`/dashboard?projectId=${project.id}`}
                  className={
                    view.selectedProject?.id === project.id
                      ? "font-semibold underline"
                      : "text-muted-foreground hover:underline"
                  }
                >
                  {project.name}
                </Link>
                {project.isExample && (
                  <Badge variant="outline" className="text-xs">
                    Example
                  </Badge>
                )}
                {project.readOnly && !project.isExample && (
                  <Badge variant="secondary" className="text-xs">
                    Read-only
                  </Badge>
                )}
              </li>
            ))}
          </ul>
          {canUseProjects && (
            <form action={createProject} className="flex gap-2">
              <Input type="text" name="name" placeholder="New project name" required />
              <Button type="submit">Create project</Button>
            </form>
          )}
        </CardContent>
      </Card>

      {view.guidedSetup && <GuidedSetupPanel setup={view.guidedSetup} />}

      {isExample && selectedProject && (
        <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
          <span className="font-medium text-foreground">{selectedProject.name}</span> is example data —
          browse its run history and open the evidence drill-down to see what your own runs will look
          like. It disappears once your first real run lands.
        </div>
      )}

      {selectedProject && (
        <>
          {view.isSelectedProjectStale && (
            <div className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm">
              <p className="font-medium">Uploads have gone quiet for {selectedProject.name}.</p>
              <p className="text-muted-foreground">
                The gap since the last upload is much longer than this project&apos;s usual cadence — check that{" "}
                <code>RELEASETWIN_API_TOKEN</code> is still set wherever the CLI runs.
              </p>
            </div>
          )}

          {!isExample && canUseProjects && (
            <SetupSection
              projectId={selectedProject.id}
              projectName={selectedProject.name}
              connection={view.connection}
              adapterCredentials={adapterCredentials}
              projectSecrets={projectSecrets}
              isPaidTier={view.entitlements.projectSecrets}
              disconnectConnection={disconnectConnection.bind(null, selectedProject.id)}
              startGitHubConnection={startGitHubConnection.bind(null, selectedProject.id)}
            />
          )}

          {!isExample && canUseProjects && evidenceConfig && (
            <EvidenceSettingsSection projectId={selectedProject.id} config={evidenceConfig} />
          )}

          {!isExample && canManage && (
            <NotificationTargetsSection
              projectId={selectedProject.id}
              entitled={view.entitlements.runNotifications}
              canManage={canManage}
              targets={notificationTargets}
            />
          )}

          {!isExample && canUseProjects && (
          <div className="flex flex-col gap-3">
            <h2 className="flex items-center gap-1.5 text-sm font-semibold tracking-wide text-muted-foreground uppercase">
              <PlayCircle className="size-4" />
              Run
            </h2>

            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle>Journeys — {selectedProject.name}</CardTitle>
                <Link href={`/journeys?projectId=${selectedProject.id}`}>
                  <Button variant="outline" size="sm">
                    Open builder
                  </Button>
                </Link>
              </CardHeader>
              <CardContent>
                <CardDescription>
                  Compose a multi-step pipeline visually and run it from the CLI with a pinned version.
                </CardDescription>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>API tokens — {selectedProject.name}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Prefix</TableHead>
                      <TableHead>Created</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {view.tokens.map((token) => (
                      <TableRow key={token.id}>
                        <TableCell>
                          <code>{token.displayPrefix}…</code>
                        </TableCell>
                        <TableCell>{new Date(token.createdAt).toLocaleString()}</TableCell>
                        <TableCell>
                          <Badge variant={token.isRevoked ? "secondary" : "default"}>
                            {token.isRevoked ? "Revoked" : "Active"}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          {!token.isRevoked && (
                            <form action={revokeToken.bind(null, selectedProject.id, token.id)}>
                              <Button variant="ghost" size="sm" type="submit">
                                Revoke
                              </Button>
                            </form>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                {canUseProjects && <IssueTokenButton projectId={selectedProject.id} />}
              </CardContent>
            </Card>
          </div>
          )}

          <div className="flex flex-col gap-3">
            <h2 className="flex items-center gap-1.5 text-sm font-semibold tracking-wide text-muted-foreground uppercase">
              <ListChecks className="size-4" />
              Results
            </h2>

            {!isExample && (
              <ReleasesSection
                projectId={selectedProject.id}
                entitled={view.entitlements.releaseRollup}
                selectedRelease={release}
                releaseWindow={releaseWindow}
              />
            )}

            <Card>
              <CardHeader>
                <CardTitle>Run history</CardTitle>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Case</TableHead>
                      <TableHead>Outcome</TableHead>
                      <TableHead>Classification</TableHead>
                      <TableHead>Cleanup</TableHead>
                      <TableHead>Evidence</TableHead>
                      <TableHead>Uploaded</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {view.caseReports.map((report, index) => (
                      <TableRow key={`${report.caseId}-${index}`}>
                        <TableCell>{report.caseId}</TableCell>
                        <TableCell>
                          <Badge variant={report.passed ? "default" : "destructive"}>
                            {report.passed ? "PASS" : "FAIL"}
                          </Badge>
                        </TableCell>
                        <TableCell>{report.classification ?? "—"}</TableCell>
                        <TableCell>{report.cleanupStatus}</TableCell>
                        <TableCell>
                          <EvidenceCell
                            status={report.evidenceStatus}
                            reportId={report.reportId}
                            projectId={selectedProject.id}
                          />
                        </TableCell>
                        <TableCell>{new Date(report.uploadedAt).toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Flag-proof results</CardTitle>
                <CardDescription>
                  Paired known-bad/known-good proof — shown distinctly from
                  ordinary case results above.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Case</TableHead>
                      <TableHead>Build</TableHead>
                      <TableHead>Outcome</TableHead>
                      <TableHead>Known-bad leg</TableHead>
                      <TableHead>Known-good leg</TableHead>
                      <TableHead>Evidence</TableHead>
                      <TableHead>Uploaded</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {view.flagProofReports.map((report, index) => (
                      <TableRow key={`${report.caseId}-${index}`}>
                        <TableCell>{report.caseId}</TableCell>
                        <TableCell>{report.buildIdentity}</TableCell>
                        <TableCell>
                          <Badge
                            variant={
                              report.outcome === "Passed"
                                ? "default"
                                : report.outcome === "Ineligible"
                                  ? "secondary"
                                  : "destructive"
                            }
                          >
                            {report.outcome}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          {report.knownBadLegPassed === null ? (
                            "—"
                          ) : (
                            <Badge variant={report.knownBadLegPassed ? "default" : "destructive"}>
                              {report.knownBadLegPassed ? "PASS" : "FAIL"}
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          {report.knownGoodLegPassed === null ? (
                            "—"
                          ) : (
                            <Badge variant={report.knownGoodLegPassed ? "default" : "destructive"}>
                              {report.knownGoodLegPassed ? "PASS" : "FAIL"}
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          <EvidenceCell
                            status={report.evidenceStatus}
                            reportId={report.reportId}
                            projectId={selectedProject.id}
                          />
                        </TableCell>
                        <TableCell>{new Date(report.uploadedAt).toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          </div>
        </>
      )}
    </main>
  );
}

/**
 * billing: cadence choice + "Upgrade" submit. design.md D7 — monthly is preselected (small
 * mid-cycle proration); annual is offered as "save 17%". Submitting starts a Merchant-of-Record
 * checkout and redirects there; the tier only changes once the subscription webhook is processed.
 */
function UpgradeControl() {
  return (
    <form action={upgradeOrganization} className="flex items-center gap-2">
      <label htmlFor="cadence" className="sr-only">
        Billing cadence
      </label>
      <select
        id="cadence"
        name="cadence"
        defaultValue="Monthly"
        className="h-9 rounded-md border border-input bg-transparent px-2 text-sm"
      >
        <option value="Monthly">Monthly</option>
        <option value="Annual">Annual — save 17%</option>
      </select>
      <Button variant="outline" size="sm" type="submit">
        Upgrade
      </Button>
    </form>
  );
}

function EvidenceCell({
  status,
  reportId,
  projectId,
}: {
  status: import("@/lib/types").EvidenceStatus;
  reportId: string;
  projectId: string;
}) {
  if (status === "available") {
    return (
      <Link
        href={`/dashboard/reports/${reportId}/evidence?projectId=${projectId}`}
        className="text-sm underline"
      >
        View
      </Link>
    );
  }

  const label =
    status === "expired"
      ? "Expired"
      : status === "not-entitled"
        ? "Upgrade to store"
        : "—";
  return <span className="text-sm text-muted-foreground">{label}</span>;
}

/**
 * onboarding-activation (design D8): the guided first-run panel. Shown only until the org's first
 * real run lands (the server omits `guidedSetup` after that).
 */
function GuidedSetupPanel({ setup }: { setup: GuidedSetupView }) {
  const steps = [
    { label: "Create a project", done: setup.hasProject },
    { label: "Generate an API token", done: setup.hasToken },
    { label: "Run the CLI against your API", done: false },
  ];
  return (
    <Card className="border-primary/40">
      <CardHeader>
        <CardTitle>Get your first run onto the dashboard</CardTitle>
        <CardDescription>
          Three steps. The example project below shows what you&apos;ll see once a run lands.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ol className="flex flex-col gap-1.5">
          {steps.map((step, i) => (
            <li key={step.label} className="flex items-center gap-2 text-sm">
              <span
                className={
                  step.done
                    ? "flex size-5 items-center justify-center rounded-full bg-primary text-xs text-primary-foreground"
                    : "flex size-5 items-center justify-center rounded-full border text-xs text-muted-foreground"
                }
              >
                {step.done ? "✓" : i + 1}
              </span>
              <span className={step.done ? "text-muted-foreground line-through" : ""}>{step.label}</span>
            </li>
          ))}
        </ol>
        <div>
          <p className="mb-1 text-xs font-medium text-muted-foreground uppercase">Run command</p>
          <pre className="overflow-x-auto rounded bg-muted p-3 text-xs">{setup.cliCommand}</pre>
          <p className="mt-1 text-xs text-muted-foreground">
            Replace <code>&lt;YOUR_TOKEN&gt;</code> with a token from the project above.
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
