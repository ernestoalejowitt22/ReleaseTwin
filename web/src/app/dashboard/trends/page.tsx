import Link from "next/link";
import { auth } from "@clerk/nextjs/server";
import { TrendingUp } from "lucide-react";
import { Button } from "@/components/ui/button";
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
import type { DashboardView, TrendReport, TrendWindowParam } from "@/lib/types";
import { ClassificationChart, RatesChart, VolumeChart } from "./trend-charts";

const WINDOWS: TrendWindowParam[] = ["7d", "30d", "90d"];

function normalizeWindow(value: string | undefined): TrendWindowParam {
  return WINDOWS.includes(value as TrendWindowParam) ? (value as TrendWindowParam) : "30d";
}

export default async function TrendsPage({
  searchParams,
}: {
  searchParams: Promise<{ projectId?: string; window?: string }>;
}) {
  await auth.protect();

  const { projectId, window: windowParam } = await searchParams;
  const window = normalizeWindow(windowParam);

  const view = await api.get<DashboardView>(
    `/api/dashboard${projectId ? `?projectId=${projectId}` : ""}`,
  );
  const selectedProject = view.selectedProject;
  const scopeLabel = selectedProject ? selectedProject.name : "All projects";

  const linkFor = (w: TrendWindowParam) =>
    `/dashboard/trends${selectedProject ? `?projectId=${selectedProject.id}&` : "?"}window=${w}`;

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 p-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <TrendingUp className="size-6" />
          Trends
        </h1>
        <Link
          href={`/dashboard${selectedProject ? `?projectId=${selectedProject.id}` : ""}`}
          className="text-sm underline"
        >
          Back to dashboard
        </Link>
      </header>

      <nav className="flex flex-wrap gap-2 text-sm">
        <Link
          href={`/dashboard/trends?window=${window}`}
          className={!selectedProject ? "font-semibold underline" : "text-muted-foreground hover:underline"}
        >
          Organization
        </Link>
        {view.projects.map((p) => (
          <Link
            key={p.id}
            href={`/dashboard/trends?projectId=${p.id}&window=${window}`}
            className={
              selectedProject?.id === p.id
                ? "font-semibold underline"
                : "text-muted-foreground hover:underline"
            }
          >
            {p.name}
          </Link>
        ))}
      </nav>

      {!view.entitlements.trendAnalytics ? (
        <Card>
          <CardHeader>
            <CardTitle>Trends are a Team-tier feature</CardTitle>
            <CardDescription>
              See whether your case pass rate and flag-proof pass rate are climbing, which cases are
              flaky, and whether run volume is holding — computed from the runs you already upload.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form action="/dashboard" method="get">
              <Button type="submit">Upgrade to unlock trends</Button>
            </form>
          </CardContent>
        </Card>
      ) : (
        <TrendsBody
          projectId={selectedProject?.id ?? null}
          scopeLabel={scopeLabel}
          window={window}
          linkFor={linkFor}
        />
      )}
    </main>
  );
}

async function TrendsBody({
  projectId,
  scopeLabel,
  window,
  linkFor,
}: {
  projectId: string | null;
  scopeLabel: string;
  window: TrendWindowParam;
  linkFor: (w: TrendWindowParam) => string;
}) {
  const path = projectId
    ? `/api/projects/${projectId}/trends?window=${window}`
    : `/api/trends?window=${window}`;
  const report = await api.get<TrendReport>(path);

  const hasData = report.buckets.some((b) => b.runVolume > 0);

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <p className="text-sm text-muted-foreground">
          {scopeLabel} · {report.granularity} buckets
        </p>
        <div className="ml-auto flex gap-1 rounded-md border p-0.5">
          {WINDOWS.map((w) => (
            <Link
              key={w}
              href={linkFor(w)}
              className={`rounded px-2.5 py-1 text-sm ${
                w === window ? "bg-secondary font-medium" : "text-muted-foreground hover:bg-secondary/50"
              }`}
            >
              {w === "7d" ? "7 days" : w === "30d" ? "30 days" : "90 days"}
            </Link>
          ))}
        </div>
      </div>

      {!hasData && (
        <div className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm">
          No runs were uploaded in this window yet.
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Pass rates</CardTitle>
          <CardDescription>
            Case pass rate and flag-proof pass rate per bucket. A gap means no eligible runs that
            bucket — not a 0%.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RatesChart buckets={report.buckets} granularity={report.granularity} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Run volume</CardTitle>
          <CardDescription>Case reports + flag-proof reports uploaded per bucket.</CardDescription>
        </CardHeader>
        <CardContent>
          <VolumeChart buckets={report.buckets} granularity={report.granularity} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Failure classification</CardTitle>
          <CardDescription>Failed case reports by classification, stacked per bucket.</CardDescription>
        </CardHeader>
        <CardContent>
          <ClassificationChart buckets={report.buckets} granularity={report.granularity} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Flakiest cases</CardTitle>
          <CardDescription>
            Cases whose pass/fail outcome flipped most often in this window. A case that never
            flipped does not appear.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {report.flakiestCases.length === 0 ? (
            <p className="text-sm text-muted-foreground">No cases flipped in this window.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Case</TableHead>
                  <TableHead>Flips</TableHead>
                  <TableHead>Last activity</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {report.flakiestCases.map((c) => (
                  <TableRow key={c.caseId}>
                    <TableCell>{c.caseId}</TableCell>
                    <TableCell>
                      <Badge variant="destructive">{c.flipCount}</Badge>
                    </TableCell>
                    <TableCell>{new Date(c.lastActivity).toLocaleString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </>
  );
}
