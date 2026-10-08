#!/usr/bin/env bash
# Avis de licence des paquets NuGet distribués avec l'hôte (THIRD-PARTY-NOTICES.txt, copié dans
# l'image Docker). MIT et BSD-3-Clause exigent de reproduire l'avis de copyright et la licence dans
# toute distribution binaire : l'audit (audit.sh) dit qu'une licence est permise, ce fichier remplit
# l'obligation qu'elle impose. Même commande en local et en CI :
#   ./licenses/notices.sh
# Seul le graphe de l'hôte compte : les paquets de test ne sont jamais distribués, et le framework
# partagé ASP.NET Core vient de l'image de base, avec ses propres avis.
# Pour chaque paquet : le fichier de licence qu'il embarque, sinon le copyright de son nuspec et le
# texte de référence de sa licence (licenses/texts/). Échoue sur une licence sans texte relu.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"
host="$root/src/ReveilMusical.Api/ReveilMusical.Api.csproj"
output="${1:-$root/THIRD-PARTY-NOTICES.txt}"

dotnet restore "$host" --verbosity quiet
packages="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"

# Premier élément du nuspec qui porte cette balise (licence, copyright, URL du projet).
nuspec_field() { sed -n "s:.*<$2[^>]*>\(.*\)</$2>.*:\1:p" "$1" | head -n 1; }

licences_used=()
{
  echo "Avis de licence des composants tiers distribués avec ReveilMusical.Api"
  echo "Généré par licenses/notices.sh à partir du graphe restauré : ne pas éditer à la main."

  while IFS=/ read -r id version; do
    lower="$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')" # bash 3 (macOS) : pas de ${id,,}
    dir="$packages/$lower/$version"
    nuspec="$dir/$lower.nuspec"
    licence="$(nuspec_field "$nuspec" license)"
    bundled="$(find "$dir" -maxdepth 1 -type f -iname 'license*' | sort | head -n 1)"

    echo
    echo "================================================================================"
    echo "$id $version"
    echo "Licence : ${licence:-non déclarée}"
    echo "Projet : $(nuspec_field "$nuspec" projectUrl)"
    echo "$(nuspec_field "$nuspec" copyright)"
    echo "--------------------------------------------------------------------------------"

    if [ -n "$bundled" ]; then
      tr -d '\r' < "$bundled"
    elif [ -n "$licence" ] && [ -f "$here/texts/$licence.txt" ]; then
      echo "Texte de la licence : voir « $licence » en fin de fichier."
      licences_used+=("$licence")
    else
      echo "ÉCHEC : $id $version (licence '${licence:-?}') n'embarque aucun texte de licence, et licenses/texts/ n'en a pas de relu." >&2
      exit 1
    fi
  done < <(jq -r '.libraries | to_entries[] | select(.value.type == "package") | .key' \
             "$root/src/ReveilMusical.Api/obj/project.assets.json" | sort -f)

  for licence in $(printf '%s\n' ${licences_used[@]+"${licences_used[@]}"} | sort -u); do
    echo
    echo "================================================================================"
    echo "$licence"
    echo "--------------------------------------------------------------------------------"
    cat "$here/texts/$licence.txt"
  done
} > "$output.tmp"

mv "$output.tmp" "$output"
echo "Avis de licence écrits dans $output."
