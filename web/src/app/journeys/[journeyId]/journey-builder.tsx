"use client";

import { useMemo, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { saveJourneyVersion } from "../actions";

interface KeyValue {
  key: string;
  value: string;
}

interface CaptureRow {
  name: string;
  from: string;
}

interface StepState {
  operation: string;
  params: KeyValue[];
  headers: KeyValue[];
  captures: CaptureRow[];
}

let nextId = 0;
function newId() {
  nextId += 1;
  return nextId;
}

function emptyStep(): StepState {
  return { operation: "", params: [], headers: [], captures: [] };
}

/** Every capture name declared by an earlier step in the current pipeline — shown as a reference, not auto-inserted. */
function availableCaptures(steps: StepState[], uptoIndex: number): string[] {
  return steps
    .slice(0, uptoIndex)
    .flatMap((step) => step.captures.map((c) => c.name.trim()))
    .filter((name) => name.length > 0);
}

function yamlString(value: string): string {
  const escaped = value.replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\n/g, "\\n");
  return `"${escaped}"`;
}

function buildYaml(state: {
  caseId: string;
  oracleLocator: string;
  fixtureLocator: string;
  fixtureSha256: string;
  steps: StepState[];
  cleanup: string[];
}): string {
  const lines: string[] = [];
  lines.push(`id: ${yamlString(state.caseId)}`);
  lines.push(`oracle:`);
  lines.push(`  locator: ${yamlString(state.oracleLocator)}`);
  lines.push(`fixture:`);
  lines.push(`  locator: ${yamlString(state.fixtureLocator)}`);
  if (state.fixtureSha256.trim()) {
    lines.push(`  sha256: ${yamlString(state.fixtureSha256)}`);
  }

  lines.push(`pipeline:`);
  for (const step of state.steps) {
    lines.push(`  - operation: ${yamlString(step.operation)}`);
    const params = step.params.filter((p) => p.key.trim().length > 0);
    const headers = step.headers.filter((h) => h.key.trim().length > 0);
    if (params.length > 0 || headers.length > 0) {
      lines.push(`    with:`);
      for (const p of params) {
        lines.push(`      ${p.key}: ${yamlString(p.value)}`);
      }
      if (headers.length > 0) {
        lines.push(`      headers:`);
        for (const h of headers) {
          lines.push(`        ${h.key}: ${yamlString(h.value)}`);
        }
      }
    }
    const captures = step.captures.filter((c) => c.name.trim().length > 0);
    if (captures.length > 0) {
      lines.push(`    capture:`);
      for (const c of captures) {
        lines.push(`      - name: ${yamlString(c.name)}`);
        lines.push(`        from: ${yamlString(c.from)}`);
      }
    }
  }

  const cleanup = state.cleanup.filter((op) => op.trim().length > 0);
  if (cleanup.length > 0) {
    lines.push(`cleanup:`);
    for (const op of cleanup) {
      lines.push(`  - operation: ${yamlString(op)}`);
    }
  }

  return lines.join("\n") + "\n";
}

export function JourneyBuilder({ journeyId, projectId }: { journeyId: string; projectId: string }) {
  const [caseId, setCaseId] = useState("");
  const [oracleLocator, setOracleLocator] = useState("");
  const [fixtureLocator, setFixtureLocator] = useState("");
  const [fixtureSha256, setFixtureSha256] = useState("");
  const [steps, setSteps] = useState<(StepState & { id: number })[]>([]);
  const [cleanup, setCleanup] = useState<{ id: number; operation: string }[]>([]);
  const [savedVersion, setSavedVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const router = useRouter();

  const yaml = useMemo(
    () => buildYaml({ caseId, oracleLocator, fixtureLocator, fixtureSha256, steps, cleanup: cleanup.map((c) => c.operation) }),
    [caseId, oracleLocator, fixtureLocator, fixtureSha256, steps, cleanup],
  );

  function addStep() {
    setSteps((prev) => [...prev, { ...emptyStep(), id: newId() }]);
  }

  function removeStep(id: number) {
    setSteps((prev) => prev.filter((s) => s.id !== id));
  }

  function moveStep(id: number, direction: -1 | 1) {
    setSteps((prev) => {
      const index = prev.findIndex((s) => s.id === id);
      const swapWith = index + direction;
      if (index < 0 || swapWith < 0 || swapWith >= prev.length) {
        return prev;
      }
      const next = [...prev];
      [next[index], next[swapWith]] = [next[swapWith], next[index]];
      return next;
    });
  }

  function updateStep(id: number, updater: (step: StepState) => StepState) {
    setSteps((prev) => prev.map((s) => (s.id === id ? { ...updater(s), id } : s)));
  }

  function save() {
    setError(null);
    startTransition(async () => {
      try {
        const version = await saveJourneyVersion(journeyId, projectId, yaml);
        setSavedVersion(version);
        router.refresh();
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to save journey version.");
      }
    });
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium">Case ID</label>
          <Input value={caseId} onChange={(e) => setCaseId(e.target.value)} placeholder="MY-JOURNEY-1" />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium">Oracle locator</label>
          <Input value={oracleLocator} onChange={(e) => setOracleLocator(e.target.value)} placeholder="tickets/MY-JOURNEY-1" />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium">Fixture locator</label>
          <Input value={fixtureLocator} onChange={(e) => setFixtureLocator(e.target.value)} placeholder="example.json" />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium">Fixture sha256 (optional)</label>
          <Input value={fixtureSha256} onChange={(e) => setFixtureSha256(e.target.value)} placeholder="leave blank to trust whatever's on disk" />
        </div>
      </div>
      <p className="text-xs text-muted-foreground">
        The fixture is resolved locally by whatever machine runs the CLI (via <code>RELEASETWIN_FIXTURES_ROOT</code>) —
        hosted fixture storage isn&apos;t built yet.
      </p>

      <div className="flex flex-col gap-4">
        <h3 className="text-sm font-semibold">Pipeline steps</h3>
        {steps.map((step, index) => (
          <div key={step.id} data-testid={`step-${index}`} className="flex flex-col gap-3 rounded-lg border p-3">
            <div className="flex items-center justify-between gap-2">
              <Input
                className="max-w-xs"
                data-testid="step-operation"
                value={step.operation}
                onChange={(e) => updateStep(step.id, (s) => ({ ...s, operation: e.target.value }))}
                placeholder="http.request, ui.navigate, ui.click, ..."
              />
              <div className="flex gap-1">
                <Button type="button" variant="ghost" size="sm" onClick={() => moveStep(step.id, -1)} disabled={index === 0}>
                  Up
                </Button>
                <Button type="button" variant="ghost" size="sm" onClick={() => moveStep(step.id, 1)} disabled={index === steps.length - 1}>
                  Down
                </Button>
                <Button type="button" variant="ghost" size="sm" onClick={() => removeStep(step.id)}>
                  Remove
                </Button>
              </div>
            </div>

            {availableCaptures(steps, index).length > 0 && (
              <p className="text-xs text-muted-foreground">
                Captured so far: {availableCaptures(steps, index).map((name) => `{{${name}}}`).join(", ")}
              </p>
            )}

            <KeyValueEditor
              testId="params"
              label="Parameters"
              rows={step.params}
              onChange={(rows) => updateStep(step.id, (s) => ({ ...s, params: rows }))}
              placeholderKey="url"
              placeholderValue="https://example.com or {{captureName}}"
            />

            <KeyValueEditor
              testId="headers"
              label="Headers (optional)"
              rows={step.headers}
              onChange={(rows) => updateStep(step.id, (s) => ({ ...s, headers: rows }))}
              placeholderKey="Authorization"
              placeholderValue="Bearer {{token}}"
            />

            <div data-testid="captures" className="flex flex-col gap-2">
              <p className="text-xs font-medium text-muted-foreground">Captures (values this step makes available to later steps)</p>
              {step.captures.map((capture, ci) => (
                <div key={ci} className="flex gap-2">
                  <Input
                    className="w-32"
                    data-testid="capture-name"
                    value={capture.name}
                    onChange={(e) =>
                      updateStep(step.id, (s) => ({
                        ...s,
                        captures: s.captures.map((c, i) => (i === ci ? { ...c, name: e.target.value } : c)),
                      }))
                    }
                    placeholder="name"
                  />
                  <Input
                    data-testid="capture-from"
                    value={capture.from}
                    onChange={(e) =>
                      updateStep(step.id, (s) => ({
                        ...s,
                        captures: s.captures.map((c, i) => (i === ci ? { ...c, from: e.target.value } : c)),
                      }))
                    }
                    placeholder="json:$.token or text:#selector or header:X-Name"
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => updateStep(step.id, (s) => ({ ...s, captures: s.captures.filter((_, i) => i !== ci) }))}
                  >
                    Remove
                  </Button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="self-start"
                onClick={() => updateStep(step.id, (s) => ({ ...s, captures: [...s.captures, { name: "", from: "" }] }))}
              >
                Add capture
              </Button>
            </div>
          </div>
        ))}
        <Button type="button" variant="outline" onClick={addStep} className="self-start">
          Add step
        </Button>
      </div>

      <div className="flex flex-col gap-2">
        <h3 className="text-sm font-semibold">Cleanup (runs regardless of pass/fail)</h3>
        {cleanup.map((c) => (
          <div key={c.id} className="flex gap-2">
            <Input
              value={c.operation}
              onChange={(e) =>
                setCleanup((prev) => prev.map((row) => (row.id === c.id ? { ...row, operation: e.target.value } : row)))
              }
              placeholder="ui.closePage"
            />
            <Button type="button" variant="ghost" size="sm" onClick={() => setCleanup((prev) => prev.filter((row) => row.id !== c.id))}>
              Remove
            </Button>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="self-start"
          onClick={() => setCleanup((prev) => [...prev, { id: newId(), operation: "" }])}
        >
          Add cleanup step
        </Button>
      </div>

      <div className="flex flex-col gap-2">
        <h3 className="text-sm font-semibold">Generated YAML (preview)</h3>
        <Textarea value={yaml} readOnly rows={Math.min(20, yaml.split("\n").length + 1)} className="font-mono text-xs" />
      </div>

      {error && (
        <div className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">{error}</div>
      )}
      {savedVersion !== null && !error && (
        <div className="rounded-md border border-emerald-500/50 bg-emerald-500/10 p-3 text-sm">
          Saved as version {savedVersion}. Run it with:
          <pre className="mt-1 overflow-x-auto rounded bg-black/5 p-2 text-xs">
            <code>{`dotnet run --project src/ReleaseTwin.Cli -- --journey ${journeyId}@${savedVersion}`}</code>
          </pre>
        </div>
      )}

      <Button type="button" onClick={save} disabled={isPending || !caseId || !oracleLocator || !fixtureLocator}>
        {isPending ? "Saving…" : "Save as new version"}
      </Button>
    </div>
  );
}

function KeyValueEditor({
  testId,
  label,
  rows,
  onChange,
  placeholderKey,
  placeholderValue,
}: {
  testId: string;
  label: string;
  rows: KeyValue[];
  onChange: (rows: KeyValue[]) => void;
  placeholderKey: string;
  placeholderValue: string;
}) {
  return (
    <div data-testid={testId} className="flex flex-col gap-2">
      <p className="text-xs font-medium text-muted-foreground">{label}</p>
      {rows.map((row, i) => (
        <div key={i} className="flex gap-2">
          <Input
            className="w-40"
            data-testid="kv-key"
            value={row.key}
            onChange={(e) => onChange(rows.map((r, idx) => (idx === i ? { ...r, key: e.target.value } : r)))}
            placeholder={placeholderKey}
          />
          <Input
            data-testid="kv-value"
            value={row.value}
            onChange={(e) => onChange(rows.map((r, idx) => (idx === i ? { ...r, value: e.target.value } : r)))}
            placeholder={placeholderValue}
          />
          <Button type="button" variant="ghost" size="sm" onClick={() => onChange(rows.filter((_, idx) => idx !== i))}>
            Remove
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => onChange([...rows, { key: "", value: "" }])}>
        Add {label.toLowerCase().replace(" (optional)", "")}
      </Button>
    </div>
  );
}
