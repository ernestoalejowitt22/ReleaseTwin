import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api";
import type { JourneySummary, JourneyVersionSummary } from "@/lib/types";
import { JourneyBuilder } from "./journey-builder";

export default async function JourneyPage({
  params,
  searchParams,
}: {
  params: Promise<{ journeyId: string }>;
  searchParams: Promise<{ projectId?: string }>;
}) {
  await auth.protect();

  const { journeyId } = await params;
  const { projectId } = await searchParams;
  if (!projectId) {
    redirect("/dashboard");
  }

  const [journey, versions] = await Promise.all([
    api.get<JourneySummary>(`/api/journeys/${journeyId}?projectId=${projectId}`),
    api.get<JourneyVersionSummary[]>(`/api/journeys/${journeyId}/versions?projectId=${projectId}`),
  ]);

  return (
    <main className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">{journey.name}</h1>
        <Link href={`/journeys?projectId=${projectId}`} className="text-sm text-muted-foreground hover:underline">
          Back to journeys
        </Link>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Build a version</CardTitle>
          <CardDescription>
            Compose an ordered pipeline of steps, wire a capture from one step into a later step&apos;s
            parameter using <code>{"{{captureName}}"}</code>, and save to create a new, immutable version.
            HTTP and UI steps only — see the CLI docs for other adapters.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <JourneyBuilder journeyId={journeyId} projectId={projectId} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Version history</CardTitle>
        </CardHeader>
        <CardContent>
          {versions.length === 0 ? (
            <p className="text-sm text-muted-foreground">No versions saved yet.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Version</TableHead>
                  <TableHead>Created by</TableHead>
                  <TableHead>Created at</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {versions.map((version) => (
                  <TableRow key={version.version}>
                    <TableCell>{version.version}</TableCell>
                    <TableCell>{version.createdByDisplayName}</TableCell>
                    <TableCell>{new Date(version.createdAt).toLocaleString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </main>
  );
}
