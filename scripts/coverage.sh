#!/usr/bin/env bash
# Couverture de code, avec seuil bloquant. Même commande en local et en CI :
#   ./scripts/coverage.sh                 # rapport HTML dans coverage/index.html
#   LINE_THRESHOLD=95 ./scripts/coverage.sh
# Mesure : coverlet.MTP sur chaque projet de test, rapports fusionnés par ReportGenerator, sur le
# seul code de production (src/) : les projets de test et TestSupport sont exclus.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="$root/coverage"
line_threshold="${LINE_THRESHOLD:-90}"
branch_threshold="${BRANCH_THRESHOLD:-80}"

cd "$root"
rm -rf "$out"
dotnet tool restore > /dev/null

dotnet test --solution ReveilMusical.slnx \
  --coverlet --coverlet-output-format cobertura \
  --results-directory "$out/raw"

dotnet tool run reportgenerator \
  "-reports:$out/raw/coverage.cobertura.*.xml" \
  "-targetdir:$out" \
  "-reporttypes:Html;JsonSummary;TextSummary" \
  "-assemblyfilters:+ReveilMusical.*;-ReveilMusical.*Tests;-ReveilMusical.TestSupport" \
  > /dev/null

line="$(jq '.summary.linecoverage' "$out/Summary.json")"
branch="$(jq '.summary.branchcoverage' "$out/Summary.json")"
echo "Couverture : lignes ${line} % (seuil ${line_threshold} %), branches ${branch} % (seuil ${branch_threshold} %)."
echo "Rapport : $out/index.html"

if awk -v l="$line" -v b="$branch" -v lt="$line_threshold" -v bt="$branch_threshold" 'BEGIN { exit !(l < lt || b < bt) }'; then
  echo "Couverture sous le seuil." >&2
  exit 1
fi
