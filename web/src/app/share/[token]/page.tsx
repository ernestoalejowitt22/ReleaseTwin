import { notFound } from "next/navigation";
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
import type { EvidenceLeg, SharedEvidenceView } from "@/lib/types";

/**
 * evidence-sharing (design D7): the UNAUTHENTICATED shared-evidence page. It lives outside the
 * dashboard tree, calls the hosted API's own unauthenticated `/api/shared-runs/{token}` route, and
 * renders only the redacted evidence document — no dashboard chrome, no navigation, no link to any
 * other run or to any account surface.
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

export const metadata = { robots: { index: false, follow: false } };

function resultVariant(result: string): "default" | "destructive" | "secondary" {
  if (result === "passed") return "default";
  if (result === "ineligible") return "secondary";
  return "destructive";
}

function stepOutcomeVariant(outcome: string): "default" | "destructive" | "secondary" {
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
                  <Badge variant={stepOutcomeVariant(step.outcome)}>{step.outcome}</Badge>
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

export default async function SharedEvidencePage({
  params,
}: {
  params: Promise<{ token: string }>;
}) {
  const { token } = await params;

  const response = await fetch(
    `${API_BASE_URL}/api/shared-runs/${encodeURIComponent(token)}`,
    { cache: "no-store" },
  );

  if (response.status === 404) {
    notFound();
  }

  if (response.status === 403) {
    return (
      <main className="mx-auto flex min-h-screen w-full max-w-2xl flex-col items-center justify-center gap-4 p-6 text-center">
        <h1 className="text-2xl font-semibold">This link is no longer available</h1>
        <p className="text-muted-foreground">
          The organization that shared this evidence is not currently on a plan that allows shared
          links. If you expected to see something here, ask them to re-share it.
        </p>
      </main>
    );
  }

  if (!response.ok) {
    throw new Error(`shared-runs responded ${response.status}`);
  }

  const view = (await response.json()) as SharedEvidenceView;

  return (
    <main className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-6 p-6">
      <header className="flex flex-col gap-1">
        <p className="text-xs uppercase tracking-wide text-muted-foreground">
          Shared release evidence · read-only
        </p>
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-semibold">{view.caseId}</h1>
          <Badge variant={resultVariant(view.result)}>{view.result}</Badge>
        </div>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Result</CardTitle>
          <CardDescription>
            {view.reportKind === "flag-proof" ? "Flag proof" : "Case"} ·{" "}
            {view.classification ? `${view.classification} · ` : ""}
            fixture <code>{view.fixtureSha256}</code>
            {view.evidenceUploadedAt
              ? ` · evidence uploaded ${new Date(view.evidenceUploadedAt).toLocaleString()}`
              : ""}
          </CardDescription>
        </CardHeader>
      </Card>

      {!view.hasEvidenceDocument || !view.document ? (
        <Card>
          <CardHeader>
            <CardTitle>No evidence document</CardTitle>
            <CardDescription>
              No evidence document was uploaded for this run — only its metadata-level result is
              shown above.
            </CardDescription>
          </CardHeader>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>
                <code>{view.document.oracleLocator}</code>
              </CardTitle>
              <CardDescription>
                {view.document.redactionNote ??
                  "Redacted by the CLI before upload. Screenshots are best-effort-redacted."}
              </CardDescription>
            </CardHeader>
          </Card>

          {view.document.legs.map((leg, i) => (
            <LegSection key={leg.leg ?? `leg-${i}`} leg={leg} />
          ))}

          {view.screenshotIds.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Screenshots</CardTitle>
                <CardDescription>Best-effort-redacted in the CLI before upload.</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-wrap gap-4">
                {view.screenshotIds.map((id) => (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    key={id}
                    alt={`redacted screenshot ${id}`}
                    className="max-w-full rounded border"
                    src={`/share/${encodeURIComponent(token)}/screenshot/${encodeURIComponent(id)}`}
                  />
                ))}
              </CardContent>
            </Card>
          )}
        </>
      )}
    </main>
  );
}
