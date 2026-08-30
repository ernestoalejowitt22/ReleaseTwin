#!/usr/bin/env bash
# Drives the terminal session that demo/record.sh captures with asciinema.
# Not meant to be run directly — record.sh sets PATH, TERM and the LAUNCHDARKLY_* env first.
set -u

GREEN='\033[1;32m'; RED='\033[1;31m'; CYAN='\033[1;36m'; DIM='\033[2m'; RESET='\033[0m'

type_cmd() {
  printf "${GREEN}\$${RESET} "
  sleep 0.35
  local s=$1
  for (( i=0; i<${#s}; i++ )); do printf '%s' "${s:$i:1}"; sleep 0.02; done
  printf '\n'
  sleep 0.25
}

comment() { printf "${DIM}# %s${RESET}\n" "$1"; sleep 0.6; }

# Presentational only: highlight the runner's own verdict words. The text is the CLI's real output.
colorize() {
  sed -E "s/^(PASS)( )/$(printf "${GREEN}")\1$(printf "${RESET}")\2/;
          s/^(FAIL)( )/$(printf "${RED}")\1$(printf "${RESET}")\2/;
          s/^(FLAGPROOF)( )/$(printf "${CYAN}")\1$(printf "${RESET}")\2/;
          s/(\(Passed\))/$(printf "${GREEN}")\1$(printf "${RESET}")/;
          s/(\(Ineligible\)|\(BothFailed\))/$(printf "${RED}")\1$(printf "${RESET}")/"
}

printf '\n'
comment "Run a real HTTP case against a live API. No credentials."
type_cmd "releasetwin demo/quickstart/cases"
releasetwin demo/quickstart/cases 2>&1 | colorize || true
sleep 1.8

printf '\n'
comment "Prove a fix works: run the case known-bad, then known-good."
comment "(feature-flag creds already in the environment)"
type_cmd "releasetwin demo/flag-proof/cases"
releasetwin demo/flag-proof/cases 2>&1 | colorize || true
sleep 2.5
