"use client";

import { useRef } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Users } from "lucide-react";
import type { MyOrganization } from "@/lib/types";
import { setActiveOrganization } from "./team-actions";

/**
 * org-membership: header control for the active organization. Always shows the current org; switching
 * posts the choice (validated server-side against membership) and reloads the dashboard.
 */
export function OrgSwitcher({ organizations }: { organizations: MyOrganization[] }) {
  const formRef = useRef<HTMLFormElement>(null);
  const active = organizations.find((o) => o.active) ?? organizations[0];

  if (!active) {
    return null;
  }

  return (
    <div className="flex items-center gap-1.5">
      {organizations.length > 1 ? (
        <form ref={formRef} action={setActiveOrganization}>
          <select
            name="orgId"
            defaultValue={active.id}
            onChange={() => formRef.current?.requestSubmit()}
            className="h-9 max-w-[12rem] rounded-md border bg-transparent px-2 text-sm"
            aria-label="Active organization"
          >
            {organizations.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </select>
        </form>
      ) : (
        <span className="max-w-[12rem] truncate text-sm text-muted-foreground">{active.name}</span>
      )}
      <Link href="/dashboard/members">
        <Button variant="outline" size="sm" className="gap-1.5">
          <Users className="size-4" />
          Team
        </Button>
      </Link>
    </div>
  );
}
