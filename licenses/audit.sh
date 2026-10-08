#!/usr/bin/env bash
# Audit des licences NuGet (exigence légale du brief). La même commande sert en local et en CI :
#   ./licenses/audit.sh                         # la solution, écrit licenses/licenses.json
#   ./licenses/audit.sh <projet|sln> <sortie>   # une autre cible (cf. audit-canary.sh)
# Échoue si une dépendance, directe ou transitive, porte une licence absente de
# allowed-licenses.json, ou une licence que l'outil ne sait pas identifier.
#
# Outil : nuget-license (Apache-2.0), épinglé dans dotnet-tools.json. Il remplace
# dotnet-project-licenses, utilisé par TP-meteo, dont le dépôt se déclare abandonné : la
# fraîcheur exigée par le brief vaut aussi pour l'outillage.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"
input="${1:-$root/ReveilMusical.slnx}"
output="${2:-$here/licenses.json}"

cd "$root"
dotnet tool restore > /dev/null
# Le graphe audité est celui que la restauration a résolu : chaque paquet transitif, à la
# version réellement retenue (Central Package Management, pinning transitif).
dotnet restore "$input" --verbosity quiet

if ! dotnet tool run nuget-license \
    --input "$input" \
    --include-transitive \
    --allowed-license-types "$here/allowed-licenses.json" \
    --licenseurl-to-license-mappings "$here/reviewed-license-urls.json" \
    --output JsonPretty \
    --file-output "$output"; then
  echo "Licence hors liste blanche, ou non identifiée : voir $output et la section « Dépendances et licences » du README." >&2
  exit 1
fi

# Trié par paquet : le diff d'une PR montre exactement le paquet qui entre, sort ou change.
jq --indent 2 'sort_by(.PackageId | ascii_downcase) | map({PackageId, PackageVersion, License, LicenseUrl, PackageProjectUrl})' \
  "$output" > "$output.tmp" && mv "$output.tmp" "$output"

echo "Licences des paquets NuGet, directs et transitifs :"
jq -r 'group_by(.License) | .[] | "  \(.[0].License)  \(length)"' "$output"
