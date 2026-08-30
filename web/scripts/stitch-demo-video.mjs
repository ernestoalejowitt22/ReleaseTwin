#!/usr/bin/env node
// ui-session-video / ui-session-video-polish: stitch the Cypress recording (ReleaseTwin dashboard)
// + the UI-adapter recording (the browser driving NAHA's admin app) into one narrated demo clip.
//
//   node scripts/stitch-demo-video.mjs
//     --video-dir <dir>        where the adapter wrote its .webm (default: $RELEASETWIN_UI_VIDEO_DIR)
//     --cypress-video <file>   the Cypress .mp4 (default: newest *naha-admin-ui-journey* under cypress/videos)
//     --out <file>             output (default: demo/naha-releasetwin-flow.mp4)
//     --act1-speed <n>         speed factor for the dashboard act (default 2.4)
//     --act1-end <sec>         seconds to drop from the end of Act 1 — the idle cy.task gap and the
//                              evidence page live there; Act 3 pulls the evidence page back in
//                              (default 30, tuned for the ~58s three-route naha-admin-ui-journey)
//     --act3-len <sec>         seconds of the Cypress tail to use as Act 3, the evidence page (default 16)
//     --act2-freeze <sec>      hold the last frame of the NAHA-driving clip this long (default 0 —
//                              real footage fills the act now; a webm under ~4s auto-gets a 2s hold)
//     --no-crop-cypress        keep the Cypress test-runner chrome (command log + URL bar) in Act 1/3
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
const act1Speed = Number(arg("act1-speed", "2.4"));
const act1DropEnd = Number(arg("act1-end", "30"));
const act3Len = Number(arg("act3-len", "16"));
const blurSecret = flag("blur-secret-input");
const cropCypress = !flag("no-crop-cypress");

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
const adapterDur = probeDurationSec(playwrightVideo);
console.log(`ffmpeg:    ${ffmpeg}`);
console.log(`cypress:   ${cypressVideo}  (${cypressDur ? cypressDur.toFixed(1) + "s" : "?"})`);
console.log(`adapter:   ${playwrightVideo}  (${adapterDur ? adapterDur.toFixed(1) + "s" : "?"})`);

// ---- normalize target --------------------------------------------------------------------
const W = 1280;
const H = 720;
const FPS = 30;
const BG = "0x0d1117";
const V = ["-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-r", String(FPS), "-an"];

const font = [
  "/System/Library/Fonts/Supplemental/Arial.ttf",
  "/System/Library/Fonts/Helvetica.ttc",
  "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
  "/Library/Fonts/Arial.ttf",
].find(existsSync);
const fontArg = font ? `fontfile='${font}':` : "";

// drawtext value escaping: colons and single quotes break the filter parser.
const esc = (t) => t.replace(/:/g, "\\:").replace(/'/g, "\u2019");

function titleText(title, subtitle) {
  const t = `drawtext=${fontArg}text='${esc(title)}':fontcolor=white:fontsize=46:x=(w-text_w)/2:y=(h-text_h)/2-${subtitle ? 30 : 0}:line_spacing=12`;
  if (!subtitle) return t;
  const s = `drawtext=${fontArg}text='${esc(subtitle)}':fontcolor=0x9da7b3:fontsize=24:x=(w-text_w)/2:y=(h-text_h)/2+46:line_spacing=10`;
  return `${t},${s}`;
}

// Persistent lower-thirds caption drawn over an act segment.
function captionText(text) {
  return `drawtext=${fontArg}text='${esc(text)}':fontcolor=white:fontsize=27:x=(w-text_w)/2:y=h-96:box=1:boxcolor=black@0.55:boxborderw=16`;
}

const tmp = path.join(os.tmpdir(), `stitch-${Date.now()}`);
mkdirSync(tmp, { recursive: true });
const seg = (n) => path.join(tmp, `${n}.mp4`);

function card(n, title, subtitle, { dur = 2.8 } = {}) {
  ff(["-y", "-f", "lavfi", "-i", `color=c=${BG}:s=${W}x${H}:d=${dur}`, "-vf", titleText(title, subtitle), ...V, seg(n)]);
}

function clip(n, src, { start, end, speed, freezeTailSec, caption, cropChrome } = {}) {
  const a = ["-y"];
  if (start != null) a.push("-ss", String(start));
  a.push("-i", src);
  if (end != null) a.push("-to", String(end));

  const filters = [];
  if (speed && speed !== 1) filters.push(`setpts=PTS/${speed}`);
  // Drop the Cypress test-runner chrome (command-log sidebar + URL bar) so Act 1/3 read as a
  // product screen recording, not a test run. Region measured against the 1280x720 Cypress frame.
  if (cropChrome) filters.push("crop=iw-472:ih-72:462:68");
  filters.push(
    `scale=${W}:${H}:force_original_aspect_ratio=decrease`,
    `pad=${W}:${H}:(ow-iw)/2:(oh-ih)/2:color=${BG}`,
  );
  // The adapter recording can be dominated by the initial load/paint; optionally hold the final
  // rendered frame so the real admin UI stays readable.
  if (freezeTailSec) filters.push(`tpad=stop_mode=clone:stop_duration=${freezeTailSec}`);
  if (blurSecret && src === cypressVideo && !start) {
    filters.push("drawbox=x=520:y=470:w=560:h=44:color=black@1:t=fill:enable='between(t,0,1e9)'");
  }
  if (caption) filters.push(captionText(caption));

  a.push("-vf", filters.join(","), ...V, seg(n));
  ff(a);
}

// ---- build segments --------------------------------------------------------------------
const act1End = cypressDur ? Math.max(5, cypressDur - act1DropEnd) : null;
const act3Start = cypressDur ? Math.max(0, cypressDur - act3Len) : null;

// D4: real footage fills Act 2 now; only pad a suspiciously short webm.
let act2Freeze = Number(arg("act2-freeze", "0"));
if (!args.includes("--act2-freeze") && adapterDur != null && adapterDur < 4) {
  act2Freeze = 2;
  console.warn(`adapter recording is only ${adapterDur.toFixed(1)}s — applying a 2s tail freeze to Act 2.`);
}

card("00-card1", "Build a release-proof journey", "in the ReleaseTwin dashboard");
clip("01-act1", cypressVideo, {
  end: act1End ?? undefined,
  speed: act1Speed,
  caption: "Building the journey",
  cropChrome: cropCypress,
});
card("02-card2", "Run it against NAHA\u2019s live admin app", "a real customer target, driven in a real browser");
clip("03-act2", playwrightVideo, { freezeTailSec: act2Freeze, caption: "Driving NAHA\u2019s admin app" });
card("04-card3", "Redacted evidence on the dashboard", "screenshots and payloads \u2014 you decide what\u2019s safe to show");
clip("05-act3", cypressVideo, {
  start: act3Start ?? undefined,
  caption: "Redacted evidence on the dashboard",
  cropChrome: cropCypress,
});
card("06-card4", "ReleaseTwin", "release-proof journeys against real customer targets", { dur: 3.2 });

// ---- concat --------------------------------------------------------------------------
const order = ["00-card1", "01-act1", "02-card2", "03-act2", "04-card3", "05-act3", "06-card4"];
const list = order.map((n) => `file '${seg(n)}'`).join("\n");
const listFile = path.join(tmp, "concat.txt");
writeFileSync(listFile, list + "\n");

mkdirSync(path.dirname(outPath), { recursive: true });
ff(["-y", "-f", "concat", "-safe", "0", "-i", listFile, "-c", "copy", outPath]);
rmSync(tmp, { recursive: true, force: true });

console.log(`\n\u2713 ${outPath}`);
console.log(
  `  Act 1 = Cypress 0\u2013${act1End ? act1End.toFixed(0) + "s" : "end"} @ ${act1Speed}\u00d7 · ` +
    `Act 2 = adapter${act2Freeze ? ` +${act2Freeze}s freeze` : ""} · Act 3 = Cypress last ${act3Len}s.` +
    (cropCypress ? " Cypress chrome cropped." : "") +
    " Tune with --act1-end / --act1-speed / --act3-len / --act2-freeze / --no-crop-cypress.",
);
