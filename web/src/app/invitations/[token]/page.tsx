import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AcceptInviteButton } from "./accept-invite-button";

/**
 * org-membership: the invitation landing page. Signing in / signing up is required first; the
 * `X-Invite-Token` header on the API calls made here tells provisioning not to mint a throwaway
 * organization for the accepting user (design D1a).
 */
const API_BASE_URL = process.env.RELEASETWIN_API_URL ?? "http://localhost:5199";

interface InvitePreview {
  organizationName: string | null;
  role: string;
  acceptable: boolean;
}

export default async function AcceptInvitationPage({
  params,
}: {
  params: Promise<{ token: string }>;
}) {
  const { token } = await params;
  const { userId, getToken } = await auth();

  if (!userId) {
    redirect(`/sign-in?redirect_url=${encodeURIComponent(`/invitations/${token}`)}`);
  }

  const clerkToken = await getToken();
  const response = await fetch(`${API_BASE_URL}/api/invitations/${encodeURIComponent(token)}`, {
    cache: "no-store",
    headers: {
      ...(clerkToken ? { Authorization: `Bearer ${clerkToken}` } : {}),
      "X-Invite-Token": token,
    },
  });

  const notFound = response.status === 404 || !response.ok;
  const preview: InvitePreview | null = notFound
    ? null
    : ((await response.json()) as InvitePreview);

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-lg flex-col items-center justify-center gap-4 p-6">
      <Card className="w-full">
        <CardHeader>
          <CardTitle>
            {preview?.organizationName
              ? `Join ${preview.organizationName}`
              : "Invitation"}
          </CardTitle>
          <CardDescription>
            {!preview || !preview.acceptable
              ? "This invitation is not valid — it may have expired, been revoked, or already been used. Ask whoever invited you to send a new one."
              : `You've been invited as a ${preview.role.toLowerCase()}.`}
          </CardDescription>
        </CardHeader>
        {preview?.acceptable && (
          <CardContent>
            <AcceptInviteButton token={token} />
          </CardContent>
        )}
      </Card>
    </main>
  );
}
