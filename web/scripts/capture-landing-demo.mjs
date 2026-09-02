// SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
// SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
//
// landing-demo-ci-loop: regenerates the CI-gate panels for the marketing landing page
// from the two real ReleaseTwin run summaries in ./demo-summaries/. No browser, no deps —
// emits self-contained SVG so the assets stay crisp and diff-reviewable.
//
//   node web/scripts/capture-landing-demo.mjs
//
// The summaries are the verified output of the CLI run against NAHA PR #74
// (releasetwin/cases): failed.json = DEMO-GATE-1 asserting the wrong role, passed.json =
// the corrected assertion. Regenerate them with:
//   dotnet <cli> <naha>/releasetwin/cases --summary-json passed.json      (both green)
//   # then flip demo-gate.yaml's `expected:` and re-run into failed.json
//
// The comment text below is kept identical to integrations/github-action/render.mjs
// `renderBody()` — the Action is the source of truth for the wording; if it changes,
// update `commentLines()` here to match and rerun.

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const OUT = join(HERE, "..", "public", "demo");

/** Mirror of render.mjs renderBody(): the plain-text lines of the PR comment. */
function commentLines(s) {
  const icon = s.overall === "passed" ? "✅" : "❌";
  const fp = s.flagProof ?? { proven: 0, ineligible: 0, regressed: 0 };
  const lines = [
    { kind: "h1", text: `ReleaseTwin — ${icon} ${s.overall}` },
    { kind: "p", text: `${s.totals.passed} passed · ${s.totals.failed} failed · ${s.totals.cases} cases` },
    { kind: "p", text: `Flag proof: ${fp.proven} proven · ${fp.ineligible} ineligible · ${fp.regressed} regressed` },
  ];
  const notable = (s.cases ?? []).filter(
    (c) =>
      c.outcome === "failed" ||
      (c.flagProof && c.flagProof !== "Ineligible" && c.flagProof !== "Passed") ||
      c.flagProof === "Passed",
  );
  if (notable.length) {
    lines.push({
      kind: "table",
      head: ["Case", "Outcome", "Classification", "Flag proof", "Release"],
      rows: notable.map((c) => [
        c.id,
        c.outcome,
        c.classification ?? "—",
        c.flagProof ?? "—",
        c.release ?? "—",
      ]),
    });
  }
  return lines;
}

const esc = (t) =>
  String(t).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

// GitHub-dark palette.
const C = {
  bg: "#0d1117",
  border: "#30363d",
  text: "#e6edf3",
  muted: "#8b949e",
  green: "#3fb950",
  red: "#f85149",
  headerBg: "#161b22",
  mono: "#2f81f7",
};

function commentSvg(summary) {
  const lines = commentLines(summary);
  const W = 780;
  const pad = 24;
  let y = 0;
  const parts = [];

  // comment header strip
  parts.push(
    `<rect x="0" y="0" width="${W}" height="44" fill="${C.headerBg}"/>`,
    `<circle cx="26" cy="22" r="11" fill="#30363d"/>`,
    `<text x="46" y="27" font-weight="600" fill="${C.text}">github-actions</text>`,
    `<rect x="146" y="12" width="34" height="20" rx="10" fill="none" stroke="${C.border}"/>`,
    `<text x="163" y="26" font-size="11" fill="${C.muted}" text-anchor="middle">Bot</text>`,
    `<text x="192" y="27" fill="${C.muted}">commented on pull request #74</text>`,
  );
  y = 44 + 28;

  for (const l of lines) {
    if (l.kind === "h1") {
      parts.push(
        `<text x="${pad}" y="${y}" font-size="22" font-weight="700" fill="${C.text}">${esc(l.text)}</text>`,
      );
      y += 18;
      parts.push(`<line x1="${pad}" y1="${y}" x2="${W - pad}" y2="${y}" stroke="${C.border}"/>`);
      y += 34;
    } else if (l.kind === "p") {
      parts.push(`<text x="${pad}" y="${y}" font-size="14" fill="${C.text}">${esc(l.text)}</text>`);
      y += 30;
    } else if (l.kind === "table") {
      const cols = [150, 90, 130, 100, 150];
      const rowH = 40;
      const tableW = cols.reduce((a, b) => a + b, 0);
      const drawRow = (cells, ty, header) => {
        let cx = pad;
        cells.forEach((cell, i) => {
          parts.push(
            `<rect x="${cx}" y="${ty}" width="${cols[i]}" height="${rowH}" fill="${header ? C.headerBg : "none"}" stroke="${C.border}"/>`,
          );
          const mono = !header && i === 0;
          parts.push(
            `<text x="${cx + 12}" y="${ty + 25}" font-size="13" ${
              mono ? `font-family="ui-monospace,monospace" fill="${C.mono}"` : `fill="${header ? C.muted : C.text}" font-weight="${header ? 600 : 400}"`
            }>${esc(cell)}</text>`,
          );
          cx += cols[i];
        });
      };
      y += 6;
      drawRow(l.head, y, true);
      y += rowH;
      for (const r of l.rows) {
        drawRow(r, y, false);
        y += rowH;
      }
      y += 16;
      void tableW;
    }
  }
  y += 12;

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${Math.ceil(y)}" font-family="-apple-system,Segoe UI,Helvetica,Arial,sans-serif" font-size="14">
<rect x="0.5" y="0.5" width="${W - 1}" height="${Math.ceil(y) - 1}" rx="6" fill="${C.bg}" stroke="${C.border}"/>
${parts.join("\n")}
</svg>\n`;
}

function checkSvg(summary) {
  const passed = summary.overall === "passed";
  const W = 560;
  const H = 56;
  const color = passed ? C.green : C.red;
  const glyph = passed
    ? `<circle cx="20" cy="${H / 2}" r="9" fill="${color}"/><path d="M16 ${H / 2} l3 3 l6 -6" stroke="${C.bg}" stroke-width="2" fill="none"/>`
    : `<circle cx="20" cy="${H / 2}" r="9" fill="${color}"/><path d="M16 ${H / 2 - 4} l8 8 M24 ${H / 2 - 4} l-8 8" stroke="${C.bg}" stroke-width="2"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" font-family="-apple-system,Segoe UI,Helvetica,Arial,sans-serif">
<rect x="0.5" y="0.5" width="${W - 1}" height="${H - 1}" rx="6" fill="${C.bg}" stroke="${C.border}"/>
${glyph}
<text x="40" y="${H / 2 - 2}" font-size="14" font-weight="600" fill="${C.text}">ReleaseTwin</text>
<text x="40" y="${H / 2 + 16}" font-size="12" fill="${C.muted}">${summary.totals.passed} passed, ${summary.totals.failed} failed · ${
    passed ? "check passed" : "make this a required check to block the merge"
  }</text>
<text x="${W - 16}" y="${H / 2 + 5}" font-size="12" font-weight="600" fill="${color}" text-anchor="end">${
    passed ? "Passing" : "Failing"
  }</text>
</svg>\n`;
}

// Neutral CI-log palette — deliberately NOT the GitHub-dark `C` above and with no
// GitHub/Bitbucket chrome, so this panel reads as "any CI", not a product screenshot.
const L = {
  bg: "#0b0f14",
  border: "#2b333d",
  text: "#d7dee6",
  muted: "#8b949e",
  green: "#3fb950",
  red: "#f85149",
};

/**
 * A generic pipeline-log render of the CLI running as a merge gate, derived entirely from
 * the run summary: the per-case PASS/FAIL lines, the totals, the flag-proof line, and the
 * non-zero exit that fails the CI step. No vendor chrome — the landing caption names it as
 * the CLI's own stdout on any CI.
 */
function pipelineLogSvg(summary) {
  const failed = summary.overall !== "passed";
  const fp = summary.flagProof ?? { proven: 0, ineligible: 0, regressed: 0 };
  const rows = [
    { c: L.muted, t: "$ releasetwin ./cases --summary-json releasetwin-summary.json" },
    { t: "" },
    ...(summary.cases ?? []).map((c) => {
      const ok = c.outcome === "passed";
      return { c: ok ? L.green : L.red, t: `  ${ok ? "PASS" : "FAIL"}  ${c.id}` };
    }),
    { t: "" },
    {
      c: L.text,
      t: `  ${summary.totals.cases} cases · ${summary.totals.passed} passed · ${summary.totals.failed} failed`,
    },
    {
      c: L.muted,
      t: `  flag proof: ${fp.proven} proven · ${fp.ineligible} ineligible · ${fp.regressed} regressed`,
    },
    { t: "" },
    {
      c: failed ? L.red : L.green,
      t: failed
        ? "  step failed — exit 1 — merge blocked"
        : "  step passed — exit 0",
    },
  ];

  const W = 780;
  const padX = 20;
  const rowH = 22;
  const headH = 40;
  const H = headH + 16 + rows.length * rowH + 12;
  const parts = [
    `<rect x="0" y="0" width="${W}" height="${headH}" fill="#11161c"/>`,
    `<circle cx="20" cy="20" r="5" fill="#3a4048"/>`,
    `<circle cx="38" cy="20" r="5" fill="#3a4048"/>`,
    `<circle cx="56" cy="20" r="5" fill="#3a4048"/>`,
    `<text x="78" y="25" font-size="12" fill="${L.muted}">ci: release-proof gate</text>`,
  ];
  let y = headH + 16 + 12;
  for (const r of rows) {
    if (r.t) {
      parts.push(
        `<text x="${padX}" y="${y}" font-size="13" font-family="ui-monospace,SFMono-Regular,Menlo,monospace" fill="${r.c ?? L.text}">${esc(r.t)}</text>`,
      );
    }
    y += rowH;
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
<rect x="0.5" y="0.5" width="${W - 1}" height="${H - 1}" rx="6" fill="${L.bg}" stroke="${L.border}"/>
${parts.join("\n")}
</svg>\n`;
}

function main() {
  mkdirSync(OUT, { recursive: true });
  const load = (n) => JSON.parse(readFileSync(join(HERE, "demo-summaries", n), "utf8"));
  const failed = load("failed.json");
  const passed = load("passed.json");

  const assets = {
    "pr-comment-failed.svg": commentSvg(failed),
    "pr-comment-passed.svg": commentSvg(passed),
    "pr-check-failed.svg": checkSvg(failed),
    "pr-check-passed.svg": checkSvg(passed),
    "pipeline-log.svg": pipelineLogSvg(failed),
  };
  for (const [name, svg] of Object.entries(assets)) {
    const p = join(OUT, name);
    writeFileSync(p, svg);
    console.log(`wrote ${p} (${svg.length} B)`);
  }
  console.log(
    "\nDashboard panels (run history, evidence viewer, trends, rollup) are captured\n" +
      "separately from the real hosted dashboard — see docs/landing-demo.md.",
  );
}

main();
