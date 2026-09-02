// Shared CI configuration snippets used by both the docs pages and the marketing
// landing page, so the same wording cannot drift between the two surfaces.

/** Filename label shown above the Bitbucket Pipelines snippet. */
export const BITBUCKET_PIPELINES_LABEL = "bitbucket-pipelines.yml";

/**
 * A Bitbucket Pipelines pull-request gate that runs the ReleaseTwin CLI container and
 * writes the CI-agnostic `--summary-json`. A non-zero exit fails the step and blocks the
 * merge with no extra wiring. Rendered on `/docs/ci` and on the landing page's CI-loop
 * demo — keep it identical in both.
 */
export const BITBUCKET_PIPELINES_SNIPPET = `pipelines:
  pull-requests:
    '**':
      - step:
          name: Release-proof gate
          services: [docker]
          script:
            - >
              docker run --rm -v "$BITBUCKET_CLONE_DIR/cases:/workspace:ro" -v "$BITBUCKET_CLONE_DIR:/out"
              ghcr.io/OWNER/releasetwin/cli:VERSION /workspace --summary-json /out/releasetwin-summary.json
          artifacts:
            - releasetwin-summary.json`;
