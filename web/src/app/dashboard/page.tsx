import Link from "next/link";
import { UserButton } from "@clerk/nextjs";
import { auth } from "@clerk/nextjs/server";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
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
import type { DashboardView } from "@/lib/types";
import { IssueTokenButton } from "./issue-token-button";
import {
  createProject,
  disconnectConnection,
  revokeToken,
  startGitHubConnection,
} from "./actions";

export default async function DashboardPage({
  searchParams,
}: {
  searchParams: Promise<{ projectId?: string; connectionError?: string }>;
}) {
  await auth.protect();

  const { projectId, connectionError } = await searchParams;
  const view = await api.get<DashboardView>(
    `/api/dashboard${projectId ? `?projectId=${projectId}` : ""}`,
  );
  const selectedProject = view.selectedProject;

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        <UserButton />
      </header>

      {connectionError && (
        <div className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
          {connectionError}
        </div>
      )}

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
          <Card>
            <CardHeader>
              <CardTitle>Connection — {selectedProject.name}</CardTitle>
            </CardHeader>
            <CardContent>
              {view.connection ? (
                <div className="flex items-center justify-between">
                  <p>
                    Connected to{" "}
                    <code>{view.connection.externalRepo}</code> (
                    {view.connection.provider})
                  </p>
                  <form action={disconnectConnection.bind(null, selectedProject.id)}>
                    <Button variant="outline" type="submit">
                      Disconnect
                    </Button>
                  </form>
                </div>
              ) : (
                <div className="flex items-center justify-between">
                  <CardDescription>
                    Not connected to a repository yet — labeling only, no code
                    or credentials are ever read.
                  </CardDescription>
                  <form action={startGitHubConnection.bind(null, selectedProject.id)}>
                    <Button type="submit">Connect GitHub</Button>
                  </form>
                </div>
              )}
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
                    <TableHead>Uploaded</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {view.flagProofReports.map((report, index) => (
                    <TableRow key={`${report.caseId}-${index}`}>
                      <TableCell>{report.caseId}</TableCell>
                      <TableCell>{report.buildIdentity}</TableCell>
                      <TableCell className="font-semibold">{report.outcome}</TableCell>
                      <TableCell>
                        {report.knownBadLegPassed === null
                          ? "—"
                          : report.knownBadLegPassed
                            ? "PASS"
                            : "FAIL"}
                      </TableCell>
                      <TableCell>
                        {report.knownGoodLegPassed === null
                          ? "—"
                          : report.knownGoodLegPassed
                            ? "PASS"
                            : "FAIL"}
                      </TableCell>
                      <TableCell>{new Date(report.uploadedAt).toLocaleString()}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </>
      )}
    </main>
  );
}
