import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api, ApiError } from "@/lib/api";
import type { MyOrganization, OrgInvitation, OrgMember } from "@/lib/types";
import { MembersManager } from "./members-manager";

/**
 * org-membership: team management. Any member can see the roster; only an admin sees the invite
 * form, role controls, and pending invitations.
 */
export default async function MembersPage() {
  await auth.protect();

  const orgs = await api.get<MyOrganization[]>("/api/me/organizations");
  const active = orgs.find((o) => o.active) ?? orgs[0];
  if (!active) {
    redirect("/dashboard");
  }

  const isAdmin = active.role === "Admin";

  const members = await api.get<OrgMember[]>(`/api/organizations/${active.id}/members`);

  let invitations: OrgInvitation[] = [];
  if (isAdmin) {
    try {
      invitations = await api.get<OrgInvitation[]>(`/api/organizations/${active.id}/invitations`);
    } catch (err) {
      if (!(err instanceof ApiError)) throw err;
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Team · {active.name}</h1>
        <Link href="/dashboard" className="text-sm text-muted-foreground hover:underline">
          Back to dashboard
        </Link>
      </header>

      {!isAdmin && (
        <Card>
          <CardHeader>
            <CardTitle>You are a {active.role.toLowerCase()}</CardTitle>
            <CardDescription>
              Only an admin can invite teammates or change roles. Ask an admin of {active.name} if you
              need different access.
            </CardDescription>
          </CardHeader>
        </Card>
      )}

      <MembersManager
        organizationId={active.id}
        isAdmin={isAdmin}
        members={members}
        invitations={invitations.filter((i) => i.state === "Pending")}
      />
    </main>
  );
}
