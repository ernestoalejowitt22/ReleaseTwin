import { auth } from "@clerk/nextjs/server";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api, ApiError } from "@/lib/api";
import type { GitHubCallbackResult } from "@/lib/types";
import { confirmConnection } from "../actions";

/**
 * project-connections spec: "The connected repo is chosen from a real list, not free text." GitHub
 * redirects the browser here directly (a top-level navigation, no Bearer header) — this page's own
 * server-side render is what calls the .NET API (authenticated, server-to-server) to exchange the
 * code and fetch that list; the picker itself never sees a token, only the resulting repo names.
 */
export default async function GitHubCallbackPage({
  searchParams,
}: {
  searchParams: Promise<{ code?: string; state?: string }>;
}) {
  await auth.protect();

  const { code, state } = await searchParams;

  if (!code || !state) {
    return <CallbackError message="Missing GitHub authorization response." />;
  }

  let result: GitHubCallbackResult;
  try {
    result = await api.post<GitHubCallbackResult>("/api/connections/callback", { code, state });
  } catch (error) {
    const message =
      error instanceof ApiError
        ? "That connection attempt expired or was invalid — try again."
        : "Something went wrong talking to GitHub — try again.";
    return <CallbackError message={message} />;
  }

  return (
    <main className="mx-auto flex w-full max-w-lg flex-1 flex-col justify-center p-6">
      <Card>
        <CardHeader>
          <CardTitle>Choose a repository</CardTitle>
        </CardHeader>
        <CardContent>
          {result.repositories.length === 0 ? (
            <p className="text-muted-foreground">
              No repositories were found for this GitHub account.
            </p>
          ) : (
            <form action={confirmConnection} className="flex flex-col gap-4">
              <input type="hidden" name="projectId" value={result.projectId} />
              <ul className="flex flex-col gap-2">
                {result.repositories.map((repo) => (
                  <li key={repo} className="flex items-center gap-2">
                    <input
                      type="radio"
                      id={repo}
                      name="externalRepo"
                      value={repo}
                      required
                    />
                    <label htmlFor={repo}>{repo}</label>
                  </li>
                ))}
              </ul>
              <Button type="submit">Connect this repository</Button>
            </form>
          )}
        </CardContent>
      </Card>
    </main>
  );
}

function CallbackError({ message }: { message: string }) {
  return (
    <main className="mx-auto flex w-full max-w-lg flex-1 flex-col items-center justify-center gap-4 p-6 text-center">
      <p className="text-destructive">{message}</p>
      <Button asChild variant="outline">
        <a href="/dashboard">Back to dashboard</a>
      </Button>
    </main>
  );
}
