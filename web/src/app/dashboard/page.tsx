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
  ProjectSecretSummary,
} from "@/lib/types";
import { IssueTokenButton } from "./issue-token-button";
import { SetupSection } from "./setup-section";
import { EvidenceSettingsSection } from "./evidence-settings-section";
import { ReleasesSection } from "./releases-section";
import {
  createProject,
  disconnectConnection,
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
  const selectedProject = view.selectedProject;
  const adapterCredentials = selectedProject
    ? await api.get<AdapterCredentialSummary[]>(`/api/adapter-credentials/${selectedProject.id}`)
    : [];
  const projectSecrets = selectedProject
    ? await api.get<ProjectSecretSummary[]>(`/api/project-secrets/${selectedProject.id}`)
    : [];
  const evidenceConfig = selectedProject
    ? await api.get<EvidenceConfigView>(`/api/projects/${selectedProject.id}/evidence-config`)
    : null;

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <LayoutDashboard className="size-6" />
          Dashboard
        </h1>
        <div className="flex items-center gap-2">
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
          <div className="flex items-center gap-3 border-t pt-4">
            <Badge variant={view.planTier === "Free" ? "secondary" : "default"}>
              {view.planTier} plan
            </Badge>
            {view.planTier === "Free" && (
              <>
                <p className="text-sm text-muted-foreground">Limited to 1 project.</p>
                <form action={upgradeOrganization}>
                  <Button variant="outline" size="sm" type="submit">
                    Upgrade
                  </Button>
                </form>
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
              <li key={project.id}>
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
              </li>
            ))}
          </ul>
          <form action={createProject} className="flex gap-2">
            <Input type="text" name="name" placeholder="New project name" required />
            <Button type="submit">Create project</Button>
          </form>
        </CardContent>
      </Card>

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

          {evidenceConfig && (
            <EvidenceSettingsSection projectId={selectedProject.id} config={evidenceConfig} />
          )}

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
                <IssueTokenButton projectId={selectedProject.id} />
              </CardContent>
            </Card>
          </div>

          <div className="flex flex-col gap-3">
            <h2 className="flex items-center gap-1.5 text-sm font-semibold tracking-wide text-muted-foreground uppercase">
              <ListChecks className="size-4" />
              Results
            </h2>

            <ReleasesSection
              projectId={selectedProject.id}
              entitled={view.entitlements.releaseRollup}
              selectedRelease={release}
              releaseWindow={releaseWindow}
            />

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
