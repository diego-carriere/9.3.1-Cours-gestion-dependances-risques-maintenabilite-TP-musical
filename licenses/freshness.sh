#!/usr/bin/env bash
# Fraîcheur des paquets NuGet (exigence légale du brief : « licence et fraîcheur »). Même commande
# en local et en CI :
#   ./licenses/freshness.sh
# Bloque (code de sortie 1) sur :
#   - un paquet vulnérable, direct ou transitif ;
#   - un paquet déprécié, direct ou transitif ;
#   - un paquet direct en retard d'une version majeure sur la dernière stable.
# Un retard mineur ou de correctif n'est qu'un avertissement : il ne doit pas casser un build
# le jour où un éditeur publie, mais il apparaît dans le tableau.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"
solution="$root/ReveilMusical.slnx"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cd "$root"
dotnet restore "$solution" --verbosity quiet

list() { dotnet list "$solution" package "$@" --format json --output-version 1 > "$work/$1.json"; }
list --outdated
list --vulnerable --include-transitive
list --deprecated --include-transitive
dotnet list "$solution" package --format json --output-version 1 > "$work/installed.json"

status=0

# Paquets directs : version installée et dernière stable, une ligne par paquet (dédoublonnée).
jq -r -s '
  (.[0] | [.projects[].frameworks[]?.topLevelPackages[]? | {key: .id, value: .latestVersion}] | from_entries) as $latest
  | .[1] | [.projects[].frameworks[]?.topLevelPackages[]? | {id, resolved: .resolvedVersion}]
  | unique_by(.id) | .[]
  | [.id, .resolved, ($latest[.id] // .resolved)] | @tsv' \
  "$work/--outdated.json" "$work/installed.json" > "$work/direct.tsv"

printf '| Paquet direct | Installé | Dernière stable | État |\n|---|---|---|---|\n'
while IFS=$'\t' read -r id resolved latest; do
  if [ "$resolved" = "$latest" ]; then
    state="à jour"
  elif [ "${resolved%%.*}" != "${latest%%.*}" ]; then
    state="**retard majeur**"
    status=1
  else
    state="retard mineur (avertissement)"
    echo "::warning::$id $resolved → $latest disponible" >&2
  fi
  printf '| %s | %s | %s | %s |\n' "$id" "$resolved" "$latest" "$state"
done < "$work/direct.tsv"

vulnerable="$(jq -r '[.projects[].frameworks[]? | (.topLevelPackages[]?, .transitivePackages[]?) | select(.vulnerabilities) | "\(.id) \(.resolvedVersion)"] | unique | .[]' "$work/--vulnerable.json")"
deprecated="$(jq -r '[.projects[].frameworks[]? | (.topLevelPackages[]?, .transitivePackages[]?) | select(.deprecationReasons) | "\(.id) \(.resolvedVersion) (\(.deprecationReasons | join(", ")))"] | unique | .[]' "$work/--deprecated.json")"

if [ -n "$vulnerable" ]; then
  echo "Paquet(s) vulnérable(s) :" >&2
  echo "$vulnerable" >&2
  status=1
fi

if [ -n "$deprecated" ]; then
  echo "Paquet(s) déprécié(s) :" >&2
  echo "$deprecated" >&2
  status=1
fi

if [ "$status" -eq 0 ]; then
  echo
  echo "OK : aucun paquet vulnérable ni déprécié, aucun paquet direct en retard majeur."
fi
exit "$status"
