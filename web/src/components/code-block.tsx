"use client";

import { useCallback, useState } from "react";
import { Check, Copy } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * A copyable fenced code block for the marketing + docs surface. Keeps its own "copied" state; no
 * syntax highlighting on purpose — these are short shell/YAML snippets, not source listings.
 */
export function CodeBlock({
  code,
  className,
  label,
}: {
  code: string;
  className?: string;
  label?: string;
}) {
  const [copied, setCopied] = useState(false);

  const copy = useCallback(() => {
    void navigator.clipboard.writeText(code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  }, [code]);

  return (
    <div
      className={cn(
        "group relative overflow-hidden rounded-lg border bg-muted/50 text-left",
        className,
      )}
    >
      {label ? (
        <div className="border-b px-4 py-1.5 text-xs font-medium text-muted-foreground">
          {label}
        </div>
      ) : null}
      <pre className="overflow-x-auto p-4 text-[0.8rem] leading-relaxed">
        <code className="font-mono">{code}</code>
      </pre>
      <button
        type="button"
        onClick={copy}
        aria-label="Copy to clipboard"
        className="absolute top-2 right-2 rounded-md border bg-background/80 p-1.5 opacity-0 transition-opacity group-hover:opacity-100 focus-visible:opacity-100"
      >
        {copied ? (
          <Check className="size-3.5 text-primary" />
        ) : (
          <Copy className="size-3.5 text-muted-foreground" />
        )}
      </button>
    </div>
  );
}
