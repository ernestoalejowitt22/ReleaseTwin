import Link from "next/link";
import { Rocket } from "lucide-react";
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
import { Button } from "@/components/ui/button";
import { api, ApiError } from "@/lib/api";
import type {
  ReleaseCaseState,
  ReleaseHeadlineState,
  ReleaseRollup,
  ReleaseWindowParam,
} from "@/lib/types";

const WINDOWS: ReleaseWindowParam[] = ["7d", "14d", "30d", "90d"];

function normalizeWindow(value: string | undefined): ReleaseWindowParam {
  return WINDOWS.includes(value as ReleaseWindowParam) ? (value as ReleaseWindowParam) : "14d";
}

const HEADLINE_LABEL: Record<ReleaseHeadlineState, string> = {
  Proven: "Proven",
  NotProven: "Not proven",
  Incomplete: "Incomplete",
};

function headlineVariant(state: ReleaseHeadlineState): "default" | "destructive" | "secondary" {
  return state === "Proven" ? "default" : state === "NotProven" ? "destructive" : "secondary";
}

function caseStateVariant(state: ReleaseCaseState): "default" | "destructive" | "secondary" {
  return state === "Green" ? "default" : state === "Failing" ? "destructive" : "secondary";
}

/**
 * release-readiness-rollup: the per-project Releases section — label list, headline state, counts,
 * and the per-case latest result. Entitlement is checked from the bootstrap DTO; the upgrade prompt
 * renders without calling the endpoint.
 */
export async function ReleasesSection({
  projectId,
  entitled,
  selectedRelease,
  releaseWindow,
}: {
  projectId: string;
  entitled: boolean;
  selectedRelease?: string;
  releaseWindow?: string;
}) {
  if (!entitled) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Rocket className="size-5" />
            Release readiness is a Team-tier feature
          </CardTitle>
          <CardDescription>
            Group a release&apos;s cases by a <code>release:</code> label in the case file and see, at
            a glance, whether the release is proven, has a failing case, or is still incomplete.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form action="/dashboard" method="get">
            <Button type="submit">Upgrade to unlock release readiness</Button>
          </form>
        </CardContent>
      </Card>
    );
  }

  const labels = await api.get<string[]>(`/api/projects/${projectId}/releases`);
  const activeLabel =
    selectedRelease && labels.includes(selectedRelease) ? selectedRelease : labels[0];
  const window = normalizeWindow(releaseWindow);

  const rollup = activeLabel
    ? await api
        .get<ReleaseRollup>(
          `/api/projects/${projectId}/releases/${encodeURIComponent(activeLabel)}?window=${window}`,
        )
        .catch((e) => {
          if (e instanceof ApiError) return null;
          throw e;
        })
    : null;

  return (
    <Card data-testid="releases-section">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Rocket className="size-5" />
          Releases
        </CardTitle>
        <CardDescription>
          Readiness for each release seen in this project&apos;s reports.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {labels.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No <code>release:</code> labels have been uploaded for this project yet.
          </p>
        ) : (
          <>
            <div className="flex flex-wrap gap-2 text-sm">
              {labels.map((label) => (
                <Link
                  key={label}
                  href={`/dashboard?projectId=${projectId}&release=${encodeURIComponent(label)}&releaseWindow=${window}`}
                  className={
                    label === activeLabel
                      ? "font-semibold underline"
                      : "text-muted-foreground hover:underline"
                  }
                >
                  {label}
                </Link>
              ))}
            </div>

            {rollup && (
              <>
                <div className="flex flex-wrap items-center gap-3">
                  <Badge variant={headlineVariant(rollup.headline)}>
                    {HEADLINE_LABEL[rollup.headline]}
                  </Badge>
                  <span className="text-sm text-muted-foreground">
                    {rollup.greenCount} green · {rollup.failingCount} failing · {rollup.staleCount}{" "}
                    stale · last {rollup.windowDays} days
                  </span>
                  <span className="ml-auto flex gap-1 rounded-md border p-0.5">
                    {WINDOWS.map((w) => (
                      <Link
                        key={w}
                        href={`/dashboard?projectId=${projectId}&release=${encodeURIComponent(activeLabel!)}&releaseWindow=${w}`}
                        className={`rounded px-2 py-0.5 text-xs ${
                          w === window
                            ? "bg-secondary font-medium"
                            : "text-muted-foreground hover:bg-secondary/50"
                        }`}
                      >
                        {w}
                      </Link>
                    ))}
                  </span>
                </div>

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Case</TableHead>
                      <TableHead>State</TableHead>
                      <TableHead>Latest result</TableHead>
                      <TableHead>Last report</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rollup.cases.map((c) => (
                      <TableRow key={c.caseId}>
                        <TableCell>{c.caseId}</TableCell>
                        <TableCell>
                          <Badge variant={caseStateVariant(c.state)}>{c.state}</Badge>
                        </TableCell>
                        <TableCell>{c.latestOutcome}</TableCell>
                        <TableCell>{new Date(c.latestReportAt).toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
