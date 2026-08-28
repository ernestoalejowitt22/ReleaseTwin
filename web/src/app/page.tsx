import Link from "next/link";
import { auth } from "@clerk/nextjs/server";
import { GitBranch, KeyRound, Workflow, FlaskConical } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const FEATURES = [
  {
    icon: Workflow,
    title: "Compose journeys visually",
    description: "Chain HTTP and UI steps into a real pipeline, then pin and run it from the CLI.",
  },
  {
    icon: KeyRound,
    title: "Hosted secrets, not env vars",
    description: "Store credentials once per project — the CLI fetches them wherever it runs.",
  },
  {
    icon: FlaskConical,
    title: "Flag-proof",
    description: "Prove a fix actually works by running the same case known-bad and known-good.",
  },
  {
    icon: GitBranch,
    title: "Self-serve, real CLI in minutes",
    description: "Sign up, issue a token, run a zero-credential example case — no setup call.",
  },
];

/**
 * dashboard-visual-refresh design.md called for "a real product screenshot" — this is a live
 * render of the actual Card/Table/Badge components instead of a static image, so it can never go
 * visually stale relative to the real dashboard theme. Case IDs are the same ones the product's own
 * bundled zero-credential examples already produce (see examples/cases/), not invented data.
 */
function DashboardPreview() {
  return (
    <Card className="w-full max-w-xl text-left shadow-lg">
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Run history</CardTitle>
        <Badge>Paid plan</Badge>
      </CardHeader>
      <CardContent>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Case</TableHead>
              <TableHead>Outcome</TableHead>
              <TableHead>Uploaded</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell>HTTP-DEMO-1</TableCell>
              <TableCell>
                <Badge variant="default">PASS</Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">just now</TableCell>
            </TableRow>
            <TableRow>
              <TableCell>AUTH-CHAIN-DEMO-1</TableCell>
              <TableCell>
                <Badge variant="default">PASS</Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">just now</TableCell>
            </TableRow>
            <TableRow>
              <TableCell>CLM-042</TableCell>
              <TableCell>
                <Badge variant="destructive">FAIL</Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">just now</TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default async function LandingPage() {
  const { userId } = await auth();

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col items-center gap-10 px-6 py-16 text-center">
      <div className="flex flex-col items-center gap-6">
        <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">ReleaseTwin</h1>
        <p className="max-w-xl text-lg text-muted-foreground">
          Self-serve release-proof testing. Sign in to get an API token and see
          your uploaded run history.
        </p>
        <Button asChild size="lg">
          {userId ? (
            <Link href="/dashboard">Go to dashboard</Link>
          ) : (
            <Link href="/sign-in">Sign in to get started</Link>
          )}
        </Button>
      </div>

      <DashboardPreview />

      <div className="grid grid-cols-1 gap-6 text-left sm:grid-cols-2">
        {FEATURES.map((feature) => (
          <div key={feature.title} className="flex gap-3">
            <feature.icon className="mt-0.5 size-5 shrink-0 text-primary" />
            <div>
              <p className="text-sm font-semibold">{feature.title}</p>
              <p className="text-sm text-muted-foreground">{feature.description}</p>
            </div>
          </div>
        ))}
      </div>
    </main>
  );
}
