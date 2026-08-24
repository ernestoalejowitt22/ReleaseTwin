import Link from "next/link";
import { auth } from "@clerk/nextjs/server";
import { Button } from "@/components/ui/button";

export default async function LandingPage() {
  const { userId } = await auth();

  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-6 px-6 text-center">
      <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">
        ReleaseTwin
      </h1>
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
    </main>
  );
}
