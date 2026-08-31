"use client";

import { useActionState } from "react";
import { Button } from "@/components/ui/button";
import { acceptInvitation, type AcceptInviteState } from "@/app/dashboard/team-actions";

export function AcceptInviteButton({ token }: { token: string }) {
  const [state, action, pending] = useActionState<AcceptInviteState>(
    acceptInvitation.bind(null, token),
    { error: null },
  );

  return (
    <form action={action} className="flex flex-col gap-2">
      <Button type="submit" disabled={pending}>
        {pending ? "Joining…" : "Accept invitation"}
      </Button>
      {state.error && <p className="text-sm text-destructive">{state.error}</p>}
    </form>
  );
}
