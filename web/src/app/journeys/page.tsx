import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { JourneySummary } from "@/lib/types";
import { createJourney } from "./actions";

export default async function JourneysPage({
  searchParams,
}: {
  searchParams: Promise<{ projectId?: string }>;
}) {
  await auth.protect();

  const { projectId } = await searchParams;
  if (!projectId) {
    redirect("/dashboard");
  }

  const journeys = await api.get<JourneySummary[]>(`/api/journeys?projectId=${projectId}`);

  return (
    <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Journeys</h1>
        <Link href="/dashboard" className="text-sm text-muted-foreground hover:underline">
          Back to dashboard
        </Link>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Journeys for this project</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {journeys.length === 0 && (
            <p className="text-sm text-muted-foreground">No journeys yet — create one below.</p>
          )}
          <ul className="flex flex-col gap-1">
            {journeys.map((journey) => (
              <li key={journey.id}>
                <Link
                  href={`/journeys/${journey.id}?projectId=${projectId}`}
                  className="text-muted-foreground hover:underline hover:text-foreground"
                >
                  {journey.name}
                </Link>
              </li>
            ))}
          </ul>
          <form action={createJourney.bind(null, projectId)} className="flex gap-2">
            <Input type="text" name="name" placeholder="New journey name" required />
            <Button type="submit">Create journey</Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
