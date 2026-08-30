#!/usr/bin/env node
// ui-session-video: stitch the Cypress recording (ReleaseTwin dashboard) + the UI-adapter recording
// (the browser driving NAHA's admin app) into one narrated 3-act demo clip.
//
//   node scripts/stitch-demo-video.mjs
//     --video-dir <dir>        where the adapter wrote its .webm (default: $RELEASETWIN_UI_VIDEO_DIR)
//     --cypress-video <file>   the Cypress .mp4 (default: newest *naha-admin-ui-journey* under cypress/videos)
//     --out <file>             output (default: demo/naha-releasetwin-flow.mp4)
//     --act1-speed <n>         speed factor for the dashboard act (default 2)
//     --act1-end <sec>         seconds to drop from the end of Act 1 — the idle cy.task gap and the
//                              evidence page live there; Act 3 pulls the evidence page back in
//                              (default 24, tuned for the ~46s naha-admin-ui-journey recording)
//     --act3-len <sec>         seconds of the Cypress tail to use as Act 3, the evidence page (default 18)
//     --act2-freeze <sec>      hold the last frame of the NAHA-driving clip this long (default 4)
//     --blur-secret-input      draw a box over the project-secret input region during Act 1
//
// Run `npm run demo:naha-video` to produce a fresh recording first, then stitch.

import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const webDir = path.resolve(import.meta.dirname, "..");
const repoRoot = path.resolve(webDir, "..");

// ---- args -------------------------------------------------------------------------------------
const args = process.argv.slice(2);
const arg = (name, fallback) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
};
const flag = (name) => args.includes(`--${name}`);

// --video-dir → $RELEASETWIN_UI_VIDEO_DIR → the conventional dir demo:naha-video points the CLI at.
const videoDir = arg(
  "video-dir",
  process.env.RELEASETWIN_UI_VIDEO_DIR || path.join(repoRoot, "demo", ".adapter-video"),
);
const outPath = path.resolve(arg("out", path.join(repoRoot, "demo", "naha-releasetwin-flow.mp4")));
const act1Speed = Number(arg("act1-speed", "2"));
const act1DropEnd = Number(arg("act1-end", "24"));
const act3Len = Number(arg("act3-len", "18"));
const blurSecret = flag("blur-secret-input");

// ---- ffmpeg ---------------------------------------------------------------------------------
function resolveFfmpeg() {
  try {
    return require("@ffmpeg-installer/ffmpeg").path;
  } catch {
    /* not installed */
  }
  const cache =
    process.platform === "darwin"
      ? path.join(os.homedir(), "Library/Caches/ms-playwright")
      : path.join(os.homedir(), ".cache/ms-playwright");
  if (existsSync(cache)) {
    const bundled = readdirSync(cache)
      .filter((d) => d.startsWith("ffmpeg-"))
      .map((d) => {
        const dir = path.join(cache, d);
        const bin = readdirSync(dir).find((f) => f.startsWith("ffmpeg"));
        return bin ? path.join(dir, bin) : null;
      })
      .filter(Boolean)
      .sort();
    if (bundled.length) return bundled.at(-1);
  }
  return "ffmpeg"; // hope it's on PATH
}
const ffmpeg = resolveFfmpeg();
const ff = (a) => execFileSync(ffmpeg, a, { stdio: ["ignore", "inherit", "inherit"] });
function probeDurationSec(file) {
  try {
    const out = execFileSync(ffmpeg, ["-i", file], { stdio: ["ignore", "pipe", "pipe"] }).toString();
    return parseDur(out);
  } catch (e) {
    return parseDur((e.stderr || "").toString());
  }
}
function parseDur(text) {
  const m = /Duration:\s*(\d+):(\d+):(\d+\.\d+)/.exec(text);
  return m ? Number(m[1]) * 3600 + Number(m[2]) * 60 + Number(m[3]) : null;
}

// ---- inputs --------------------------------------------------------------------------------
function newest(dir, predicate) {
  if (!dir || !existsSync(dir)) return null;
  const files = readdirSync(dir)
    .filter(predicate)
    .map((f) => path.join(dir, f))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
  return files[0] ?? null;
}

const cypressVideo =
  arg("cypress-video", null) ??
  newest(path.join(webDir, "cypress", "videos"), (f) => f.includes("naha-admin-ui-journey") && f.endsWith(".mp4"));
const playwrightVideo =
  newest(videoDir, (f) => /^E2E-NAHA-UI-.*\.webm$/.test(f)) ?? newest(videoDir, (f) => f.endsWith(".webm"));

if (!cypressVideo || !existsSync(cypressVideo)) {
  console.error(
    "No Cypress video found. Run `npm run demo:naha-video` (it sets CYPRESS_VIDEO=true), " +
      "or pass --cypress-video <file>.",
  );
  process.exit(1);
}
if (!playwrightVideo || !existsSync(playwrightVideo)) {
  console.error(
    `No adapter .webm found under ${videoDir || "(RELEASETWIN_UI_VIDEO_DIR unset)"}. ` +
      "The demo run must set RELEASETWIN_UI_VIDEO_DIR and RELEASETWIN_UI_ENABLED=1.",
  );
  process.exit(1);
}

const cypressDur = probeDurationSec(cypressVideo);
console.log(`ffmpeg:    ${ffmpeg}`);
console.log(`cypress:   ${cypressVideo}  (${cypressDur ? cypressDur.toFixed(1) + "s" : "?"})`);
console.log(`adapter:   ${playwrightVideo}`);

// ---- normalize target --------------------------------------------------------------------
const W = 1280;
const H = 720;
const FPS = 30;
const V = ["-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-r", String(FPS), "-an"];

const font = [
  "/System/Library/Fonts/Supplemental/Arial.ttf",
  "/System/Library/Fonts/Helvetica.ttc",
  "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
  "/Library/Fonts/Arial.ttf",
].find(existsSync);

function drawtext(text) {
  const escaped = text.replace(/:/g, "\\:").replace(/'/g, "\u2019");
  const fontArg = font ? `fontfile='${font}':` : "";
  return `drawtext=${fontArg}text='${escaped}':fontcolor=white:fontsize=46:x=(w-text_w)/2:y=(h-text_h)/2:line_spacing=12`;
}

const tmp = path.join(os.tmpdir(), `stitch-${Date.now()}`);
mkdirSync(tmp, { recursive: true });
const seg = (n) => path.join(tmp, `${n}.mp4`);

function card(n, text) {
  ff([
    "-y",
    "-f",
    "lavfi",
    "-i",
    `color=c=0x0d1117:s=${W}x${H}:d=2.6`,
    "-vf",
    drawtext(text),
    ...V,
    seg(n),
  ]);
}

function clip(n, src, { start, end, speed, freezeTailSec } = {}) {
  const a = ["-y"];
  if (start != null) a.push("-ss", String(start));
  a.push("-i", src);
  if (end != null) a.push("-to", String(end));
  const filters = [`scale=${W}:${H}:force_original_aspect_ratio=decrease`, `pad=${W}:${H}:(ow-iw)/2:(oh-ih)/2`];
  if (speed && speed !== 1) filters.unshift(`setpts=PTS/${speed}`);
  // The adapter's recording of the customer app is dominated by the initial load/paint; hold on the
  // final rendered frame so the real admin UI is actually readable in Act 2.
  if (freezeTailSec) filters.push(`tpad=stop_mode=clone:stop_duration=${freezeTailSec}`);
  if (blurSecret && src === cypressVideo && !start) {
    // rough region of the "Project secrets" value input in the dashboard's setup section
    filters.push("drawbox=x=520:y=470:w=560:h=44:color=black@1:t=fill:enable='between(t,0,1e9)'");
  }
  a.push("-vf", filters.join(","), ...V, seg(n));
  ff(a);
}

// ---- build segments --------------------------------------------------------------------
const act1End = cypressDur ? Math.max(5, cypressDur - act1DropEnd) : null;
const act3Start = cypressDur ? Math.max(0, cypressDur - act3Len) : null;

card("00-card1", "A customer builds a\nrelease-proof journey in ReleaseTwin");
clip("01-act1", cypressVideo, { end: act1End ?? undefined, speed: act1Speed });
card("02-card2", "It runs against NAHA\u2019s live admin app\n\u2014 a real customer target");
clip("03-act2", playwrightVideo, { freezeTailSec: Number(arg("act2-freeze", "4")) });
card("04-card3", "Redacted evidence lands back\non the ReleaseTwin dashboard");
clip("05-act3", cypressVideo, { start: act3Start ?? undefined });

// ---- concat --------------------------------------------------------------------------
const list = ["00-card1", "01-act1", "02-card2", "03-act2", "04-card3", "05-act3"]
  .map((n) => `file '${seg(n)}'`)
  .join("\n");
const listFile = path.join(tmp, "concat.txt");
writeFileSync(listFile, list + "\n");

mkdirSync(path.dirname(outPath), { recursive: true });
ff(["-y", "-f", "concat", "-safe", "0", "-i", listFile, "-c", "copy", outPath]);
rmSync(tmp, { recursive: true, force: true });

console.log(`\n✓ ${outPath}`);
console.log(
  `  Act 1 = Cypress 0\u2013${act1End ? act1End.toFixed(0) + "s" : "end"} @ ${act1Speed}\u00d7 · ` +
    `Act 3 = Cypress last ${act3Len}s. Tune with --act1-end / --act1-speed / --act3-len.`,
);
