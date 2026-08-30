import { ImageResponse } from "next/og";
import { SITE_NAME } from "@/lib/site";

// Applies to every route that doesn't define its own opengraph-image. Twitter's crawler reads
// og:image, so this covers the summary_large_image card too.
export const alt = `${SITE_NAME} — self-serve release-proof testing`;
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          height: "100%",
          width: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          background: "#0a0a0a",
          color: "#fafafa",
          padding: "80px",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "20px", fontSize: 40, fontWeight: 700 }}>
          <div
            style={{
              width: 56,
              height: 56,
              borderRadius: 14,
              background: "#22c55e",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 34,
            }}
          >
            ⚗
          </div>
          {SITE_NAME}
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          <div style={{ fontSize: 68, fontWeight: 700, lineHeight: 1.1, letterSpacing: "-0.02em" }}>
            Prove a fix works before you ship it.
          </div>
          <div style={{ fontSize: 32, color: "#a1a1aa", lineHeight: 1.3 }}>
            Compose HTTP + UI journeys, run them in your own CI, and flag-proof the result. Your
            test data never leaves your infra.
          </div>
        </div>
      </div>
    ),
    { ...size },
  );
}
