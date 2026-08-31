import type { TrendBucket } from "@/lib/types";

/**
 * trend-analytics: dependency-free SVG charts (design.md D5 — the charts are simple and a chart
 * library is a supply-chain + bundle cost that would need its own justification). All server-rendered;
 * no client JS.
 */

const WIDTH = 720;
const HEIGHT = 220;
const PAD = { top: 12, right: 12, bottom: 28, left: 40 };
const PLOT_W = WIDTH - PAD.left - PAD.right;
const PLOT_H = HEIGHT - PAD.top - PAD.bottom;

function xFor(index: number, count: number): number {
  if (count <= 1) return PAD.left + PLOT_W / 2;
  return PAD.left + (index / (count - 1)) * PLOT_W;
}

function formatBucketLabel(iso: string, granularity: "daily" | "weekly"): string {
  const d = new Date(iso);
  return granularity === "weekly"
    ? `wk ${d.toLocaleDateString(undefined, { month: "short", day: "numeric" })}`
    : d.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

function AxisLabels({
  buckets,
  granularity,
}: {
  buckets: TrendBucket[];
  granularity: "daily" | "weekly";
}) {
  const step = Math.max(1, Math.ceil(buckets.length / 6));
  return (
    <>
      {buckets.map((b, i) =>
        i % step === 0 || i === buckets.length - 1 ? (
          <text
            key={b.start}
            x={xFor(i, buckets.length)}
            y={HEIGHT - 8}
            textAnchor="middle"
            className="fill-muted-foreground text-[10px]"
          >
            {formatBucketLabel(b.start, granularity)}
          </text>
        ) : null,
      )}
    </>
  );
}

function ChartFrame({ children }: { children: React.ReactNode }) {
  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      className="h-auto w-full"
      role="img"
      preserveAspectRatio="xMidYMid meet"
    >
      <line
        x1={PAD.left}
        y1={PAD.top + PLOT_H}
        x2={PAD.left + PLOT_W}
        y2={PAD.top + PLOT_H}
        className="stroke-border"
        strokeWidth={1}
      />
      {children}
    </svg>
  );
}

/** A rate line (0..1). Null points break the line into segments — a gap, per design.md. */
function RateLine({
  points,
  colorClass,
}: {
  points: (number | null)[];
  colorClass: string;
}) {
  const segments: string[] = [];
  let current: string[] = [];
  points.forEach((value, i) => {
    if (value == null) {
      if (current.length) segments.push(current.join(" "));
      current = [];
      return;
    }
    const x = xFor(i, points.length);
    const y = PAD.top + (1 - value) * PLOT_H;
    current.push(`${current.length ? "L" : "M"}${x.toFixed(1)},${y.toFixed(1)}`);
  });
  if (current.length) segments.push(current.join(" "));

  return (
    <>
      {segments.map((d, i) => (
        <path key={i} d={d} fill="none" className={colorClass} strokeWidth={2} />
      ))}
      {points.map((value, i) =>
        value == null ? null : (
          <circle
            key={i}
            cx={xFor(i, points.length)}
            cy={PAD.top + (1 - value) * PLOT_H}
            r={2.5}
            className={colorClass.replace("stroke-", "fill-")}
          />
        ),
      )}
    </>
  );
}

export function RatesChart({
  buckets,
  granularity,
}: {
  buckets: TrendBucket[];
  granularity: "daily" | "weekly";
}) {
  return (
    <div className="flex flex-col gap-2">
      <ChartFrame>
        {[0, 0.25, 0.5, 0.75, 1].map((t) => (
          <g key={t}>
            <line
              x1={PAD.left}
              y1={PAD.top + (1 - t) * PLOT_H}
              x2={PAD.left + PLOT_W}
              y2={PAD.top + (1 - t) * PLOT_H}
              className="stroke-border"
              strokeWidth={t === 0 ? 0 : 0.5}
              strokeDasharray="2 2"
            />
            <text
              x={PAD.left - 6}
              y={PAD.top + (1 - t) * PLOT_H + 3}
              textAnchor="end"
              className="fill-muted-foreground text-[10px]"
            >
              {Math.round(t * 100)}%
            </text>
          </g>
        ))}
        <RateLine points={buckets.map((b) => b.casePassRate)} colorClass="stroke-chart-2" />
        <RateLine points={buckets.map((b) => b.flagProofPassRate)} colorClass="stroke-chart-4" />
        <AxisLabels buckets={buckets} granularity={granularity} />
      </ChartFrame>
      <div className="flex gap-4 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-0.5 w-4 bg-chart-2" /> Case pass rate
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-0.5 w-4 bg-chart-4" /> Flag-proof pass rate
        </span>
      </div>
    </div>
  );
}

export function VolumeChart({
  buckets,
  granularity,
}: {
  buckets: TrendBucket[];
  granularity: "daily" | "weekly";
}) {
  const max = Math.max(1, ...buckets.map((b) => b.runVolume));
  const barW = Math.max(2, (PLOT_W / buckets.length) * 0.7);
  return (
    <ChartFrame>
      {[0, 0.5, 1].map((t) => (
        <text
          key={t}
          x={PAD.left - 6}
          y={PAD.top + (1 - t) * PLOT_H + 3}
          textAnchor="end"
          className="fill-muted-foreground text-[10px]"
        >
          {Math.round(t * max)}
        </text>
      ))}
      {buckets.map((b, i) => {
        const h = (b.runVolume / max) * PLOT_H;
        return (
          <rect
            key={b.start}
            x={xFor(i, buckets.length) - barW / 2}
            y={PAD.top + PLOT_H - h}
            width={barW}
            height={h}
            className="fill-chart-3"
          />
        );
      })}
      <AxisLabels buckets={buckets} granularity={granularity} />
    </ChartFrame>
  );
}

const CLASSIFICATION_ORDER = ["Prerequisite", "Product", "Infrastructure", "Unstable"];
const CLASSIFICATION_COLOR: Record<string, string> = {
  Prerequisite: "fill-chart-1",
  Product: "fill-chart-2",
  Infrastructure: "fill-chart-3",
  Unstable: "fill-chart-4",
};

export function ClassificationChart({
  buckets,
  granularity,
}: {
  buckets: TrendBucket[];
  granularity: "daily" | "weekly";
}) {
  const totals = buckets.map((b) =>
    CLASSIFICATION_ORDER.reduce((sum, k) => sum + (b.classificationBreakdown[k] ?? 0), 0),
  );
  const max = Math.max(1, ...totals);
  const barW = Math.max(2, (PLOT_W / buckets.length) * 0.7);

  return (
    <div className="flex flex-col gap-2">
      <ChartFrame>
        {[0, 0.5, 1].map((t) => (
          <text
            key={t}
            x={PAD.left - 6}
            y={PAD.top + (1 - t) * PLOT_H + 3}
            textAnchor="end"
            className="fill-muted-foreground text-[10px]"
          >
            {Math.round(t * max)}
          </text>
        ))}
        {buckets.map((b, i) => {
          let yCursor = PAD.top + PLOT_H;
          return (
            <g key={b.start}>
              {CLASSIFICATION_ORDER.map((k) => {
                const count = b.classificationBreakdown[k] ?? 0;
                if (count === 0) return null;
                const h = (count / max) * PLOT_H;
                yCursor -= h;
                return (
                  <rect
                    key={k}
                    x={xFor(i, buckets.length) - barW / 2}
                    y={yCursor}
                    width={barW}
                    height={h}
                    className={CLASSIFICATION_COLOR[k]}
                  />
                );
              })}
            </g>
          );
        })}
        <AxisLabels buckets={buckets} granularity={granularity} />
      </ChartFrame>
      <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
        {CLASSIFICATION_ORDER.map((k) => (
          <span key={k} className="flex items-center gap-1.5">
            <span
              className={`inline-block size-3 rounded-sm ${CLASSIFICATION_COLOR[k].replace("fill-", "bg-")}`}
            />
            {k}
          </span>
        ))}
      </div>
    </div>
  );
}
