# SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
# SPDX-License-Identifier: Apache-2.0
#
# bitbucket-custom-pipe: a thin wrapper around the published CLI image so a Bitbucket
# Pipelines step can configure it entirely through `variables:` (environment variables).
# The CLI's cases-directory argument is positional-only, so this wrapper is the one place
# that translates a CASES_PATH variable into that positional argument. Everything else a
# case run needs (RELEASETWIN_JUNIT_XML, RELEASETWIN_SUMMARY_JSON, adapter credentials)
# already works as a plain env var on the base image and passes through unmodified.
#
# Rebuilt from the just-published CLI image on each tagged release (see
# .github/workflows/release.yml), which passes --build-arg BASE_IMAGE=<name>@<digest> so
# this is always built from that exact release's image, never a stale tag. The default
# below is only for a local `docker build` with no build-arg (see README.md / tasks.md 5.1)
# and is advanced to the latest released version tag alongside each release.
ARG BASE_IMAGE=ghcr.io/ernestoalejowitt22/releasetwin/cli:0.2.0
FROM ${BASE_IMAGE}

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
