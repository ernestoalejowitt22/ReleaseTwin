import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { FlagProvider, useBooleanFlag } from "./flags-client";

function Probe() {
  const on = useBooleanFlag("flag-seam-smoke");
  return <span data-testid="probe">{String(on)}</span>;
}

describe("useFlag (client, fail-open)", () => {
  it("returns the registry default with no provider mounted", () => {
    render(<Probe />);
    expect(screen.getByTestId("probe").textContent).toBe("true");
  });

  it("reflects a server-resolved override passed through FlagProvider", () => {
    render(
      <FlagProvider values={{ "flag-seam-smoke": false }}>
        <Probe />
      </FlagProvider>,
    );
    expect(screen.getByTestId("probe").textContent).toBe("false");
  });
});
