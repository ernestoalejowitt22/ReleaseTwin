import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
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
import { api, ApiError } from "@/lib/api";
import type { EvidenceDetailView, EvidenceLeg, MyOrganization } from "@/lib/types";
import { ShareLinkControls } from "@/app/dashboard/share-link-controls";
import { loadShareLinks } from "@/app/dashboard/share-actions";

function outcomeVariant(outcome: string): "default" | "destructive" | "secondary" {
  if (outcome === "Passed" || outcome === "ExpectedFailure") return "default";
  if (outcome === "NotExecuted") return "secondary";
  return "destructive";
}

function LegSection({ leg }: { leg: EvidenceLeg }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{leg.leg ? `Leg: ${leg.leg}` : "Steps"}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>#</TableHead>
              <TableHead>Operation</TableHead>
              <TableHead>Outcome</TableHead>
              <TableHead>Duration</TableHead>
              <TableHead>Assertion</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {leg.steps.map((step) => (
              <TableRow key={step.index}>
                <TableCell>{step.index}</TableCell>
                <TableCell>
                  <code>{step.operationName}</code>
                </TableCell>
                <TableCell>
                  <Badge variant={outcomeVariant(step.outcome)}>{step.outcome}</Badge>
                </TableCell>
                <TableCell>{step.durationMs} ms</TableCell>
                <TableCell>
                  {step.assertion ? (
                    <span className="text-sm">
                      <code>{step.assertion.expression}</code>: expected{" "}
                      <code>{step.assertion.expected ?? "—"}</code>, observed{" "}
                      <code>{step.assertion.observed ?? "—"}</code>
                    </span>
                  ) : (
                    "—"
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        {leg.steps.map((step) =>
          step.adapter ? (
            <details key={`adapter-${step.index}`} className="rounded-md border p-3 text-sm">
              <summary className="cursor-pointer font-medium">
                Step {step.index} — adapter evidence
              </summary>
              <pre className="mt-2 overflow-x-auto rounded bg-muted p-2 text-xs">
                {JSON.stringify(step.adapter, null, 2)}
              </pre>
            </details>
          ) : null,
        )}
      </CardContent>
    </Card>
  );
}

export default async function EvidenceDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ reportId: string }>;
  searchParams: Promise<{ projectId?: string }>;
}) {
  await auth.protect();

  const { reportId } = await params;
  const { projectId } = await searchParams;
  if (!projectId) {
    redirect("/dashboard");
  }

  let detail: EvidenceDetailView | null = null;
  try {
    detail = await api.get<EvidenceDetailView>(
      `/api/dashboard/reports/${reportId}/evidence?projectId=${projectId}`,
    );
  } catch (err) {
    if (err instanceof ApiError && (err.status === 404 || err.status === 403)) {
      detail = null;
    } else {
      throw err;
    }
  }

  const organizations = await api.get<MyOrganization[]>("/api/me/organizations");
  const canManage = (organizations.find((o) => o.active)?.role ?? "Admin") === "Admin";
  const shareLinks = canManage
    ? await loadShareLinks(reportId, projectId)
    : ({ entitled: false, links: [] } as const);

  return (
    <main className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Run evidence</h1>
        <Link
          href={`/dashboard?projectId=${projectId}`}
          className="text-sm text-muted-foreground hover:underline"
        >
          Back to dashboard
        </Link>
      </header>

      {detail === null ? (
        <Card>
          <CardHeader>
            <CardTitle>No evidence available</CardTitle>
            <CardDescription>
              This report has no stored evidence — it was never uploaded, has been purged past its
              retention window, or your organization is not on a plan that stores evidence.
            </CardDescription>
          </CardHeader>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>
                {detail.document.caseId} — <code>{detail.document.oracleLocator}</code>
              </CardTitle>
              <CardDescription>
                {detail.document.redactionNote ??
                  "Redacted by your CLI before upload. Screenshots are best-effort-redacted."}{" "}
                Uploaded {new Date(detail.uploadedAt).toLocaleString()}.
              </CardDescription>
            </CardHeader>
          </Card>

          {detail.document.legs.map((leg, i) => (
            <LegSection key={leg.leg ?? `leg-${i}`} leg={leg} />
          ))}

          {detail.screenshotIds.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Screenshots</CardTitle>
                <CardDescription>Best-effort-redacted in your CLI before upload.</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-wrap gap-4">
                {detail.screenshotIds.map((id) => (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    key={id}
                    alt={`redacted screenshot ${id}`}
                    className="max-w-full rounded border"
                    src={`/dashboard/reports/${reportId}/evidence/screenshot/${id}?projectId=${projectId}`}
                  />
                ))}
              </CardContent>
            </Card>
          )}
        </>
      )}

      {projectId && (
        <ShareLinkControls
          reportId={reportId}
          projectId={projectId}
          entitled={shareLinks.entitled}
          canManage={canManage}
          links={shareLinks.links}
        />
      )}
    </main>
  );
}
