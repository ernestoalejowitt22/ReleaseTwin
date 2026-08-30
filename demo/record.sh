#!/usr/bin/env bash
#
# Records the ReleaseTwin landing-page demo and renders it to a self-contained animated SVG.
#
#   demo/record.sh
#
# Requires: dotnet 8+, asciinema, npx (svg-term-cli is fetched on demand), and AWS credentials
# that can read the LaunchDarkly test account secret (or the LAUNCHDARKLY_* vars already exported).
#
set -euo pipefail
cd "$(dirname "$0")/.."

BIN_DIR="demo/.bin"
CAST="demo/flag-proof.cast"
SVG="web/public/demo-flag-proof.svg"

echo "==> Building the CLI"
dotnet publish src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj -c Release -o "$BIN_DIR" \
  -p:PublishSingleFile=true --self-contained false --nologo -v q
mv -f "$BIN_DIR/ReleaseTwin.Cli" "$BIN_DIR/releasetwin"
export PATH="$PWD/$BIN_DIR:$PATH"
export TERM=xterm-256color

if [[ -z "${LAUNCHDARKLY_API_TOKEN:-}" ]]; then
  echo "==> Fetching LaunchDarkly test account from AWS Secrets Manager"
  set -a
  . <(aws secretsmanager get-secret-value --secret-id releasetwin/e2e/launchdarkly-account \
        --query SecretString --output text \
      | jq -r '"LAUNCHDARKLY_API_TOKEN=\(.apiToken)\nLAUNCHDARKLY_PROJECT_KEY=\(.projectKey)\nLAUNCHDARKLY_ENVIRONMENT_KEY=\(.environmentKey)"')
  set +a
fi
export LAUNCHDARKLY_FLAG_KEY="${LAUNCHDARKLY_FLAG_KEY:-naha.service-catalog-api}"

echo "==> Recording"
asciinema rec "$CAST" -f asciicast-v2 --overwrite \
  --idle-time-limit 2 \
  -c "demo/scripts/session.sh"

echo "==> Rendering $SVG"
npx --yes svg-term-cli --in "$CAST" --out "$SVG" --window --padding 14 --width 76 --height 16

echo "==> Done: $SVG"
