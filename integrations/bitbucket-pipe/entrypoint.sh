#!/bin/sh
# SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
# SPDX-License-Identifier: Apache-2.0
#
# The CLI's cases-directory argument is positional-only; every other setting a case run
# needs (RELEASETWIN_JUNIT_XML, RELEASETWIN_SUMMARY_JSON, adapter credentials) is already
# a plain env var the base image's CLI reads directly, so this is the only translation
# this wrapper does.
set -eu
exec dotnet ReleaseTwin.Cli.dll "${CASES_PATH:-cases}"
