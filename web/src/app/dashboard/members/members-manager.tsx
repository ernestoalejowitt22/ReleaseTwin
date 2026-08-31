"use client";

import { useActionState, useState, useTransition } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { OrgInvitation, OrgMember } from "@/lib/types";
import {
  changeMemberRole,
  inviteMember,
  removeMember,
  revokeInvitation,
  type InviteState,
} from "../team-actions";

const ROLES = ["Admin", "Member", "Viewer"] as const;

function RoleSelect({
  name,
  defaultValue,
  disabled,
}: {
  name: string;
  defaultValue: string;
  disabled?: boolean;
}) {
  return (
    <select
      name={name}
      defaultValue={defaultValue}
      disabled={disabled}
      className="h-9 rounded-md border bg-transparent px-2 text-sm"
    >
      {ROLES.map((r) => (
        <option key={r} value={r}>
          {r}
        </option>
      ))}
    </select>
  );
}

export function MembersManager({
  organizationId,
  isAdmin,
  members,
  invitations,
}: {
  organizationId: string;
  isAdmin: boolean;
  members: OrgMember[];
  invitations: OrgInvitation[];
}) {
  const [inviteState, inviteAction] = useActionState<InviteState, FormData>(
    inviteMember.bind(null, organizationId),
    { error: null },
  );
  const [copied, setCopied] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle>Members</CardTitle>
          <CardDescription>
            Admins manage billing, tokens, members, and notifications. Members trigger runs and view
            evidence. Viewers are read-only.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Joined</TableHead>
                {isAdmin && <TableHead />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {members.map((m) => (
                <TableRow key={m.userId}>
                  <TableCell>{m.displayName ?? "—"}</TableCell>
                  <TableCell>{m.email ?? "—"}</TableCell>
                  <TableCell>
                    {isAdmin ? (
                      <form
                        action={changeMemberRole.bind(null, organizationId, m.userId)}
                        className="flex items-center gap-2"
                      >
                        <RoleSelect name="role" defaultValue={m.role} />
                        <Button variant="ghost" size="sm" type="submit">
                          Save
                        </Button>
                      </form>
                    ) : (
                      <Badge variant="secondary">{m.role}</Badge>
                    )}
                  </TableCell>
                  <TableCell>{new Date(m.joinedAt).toLocaleDateString()}</TableCell>
                  {isAdmin && (
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={isPending}
                        onClick={() =>
                          startTransition(() => removeMember(organizationId, m.userId))
                        }
                      >
                        Remove
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {isAdmin && (
        <Card>
          <CardHeader>
            <CardTitle>Invite a teammate</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <form action={inviteAction} className="flex flex-wrap items-center gap-2">
              <Input type="email" name="email" placeholder="teammate@example.com" required />
              <RoleSelect name="role" defaultValue="Member" />
              <Button type="submit">Send invite</Button>
            </form>
            {inviteState.error && (
              <p className="text-sm text-destructive">{inviteState.error}</p>
            )}
            {inviteState.sent && inviteState.acceptUrl && (
              <div className="rounded-md border bg-muted/50 p-3 text-sm">
                <p className="font-medium">Invitation created.</p>
                <p className="text-muted-foreground">
                  We&apos;ll email the link; you can also share it directly:
                </p>
                <code className="mt-1 block break-all">{inviteState.acceptUrl}</code>
                <Button
                  variant="outline"
                  size="sm"
                  className="mt-2"
                  onClick={() => {
                    navigator.clipboard.writeText(inviteState.acceptUrl!);
                    setCopied(inviteState.acceptUrl!);
                  }}
                >
                  {copied === inviteState.acceptUrl ? "Copied" : "Copy link"}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {isAdmin && invitations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Pending invitations</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Email</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {invitations.map((inv) => (
                  <TableRow key={inv.token}>
                    <TableCell>{inv.email}</TableCell>
                    <TableCell>{inv.role}</TableCell>
                    <TableCell>{new Date(inv.expiresAt).toLocaleDateString()}</TableCell>
                    <TableCell className="flex gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          navigator.clipboard.writeText(inv.acceptUrl);
                          setCopied(inv.token);
                        }}
                      >
                        {copied === inv.token ? "Copied" : "Copy link"}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={isPending}
                        onClick={() =>
                          startTransition(() => revokeInvitation(organizationId, inv.token))
                        }
                      >
                        Revoke
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
