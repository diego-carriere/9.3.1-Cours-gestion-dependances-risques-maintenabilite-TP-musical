# 9.3.1-Cours-gestion-dependances-risques-maintenabilite-TP-musical — Créateur : Diego Carrière

Service « Réveil musical » : un ordonnanceur (hors périmètre) appelle `POST /wake-ups` avec l'ID de
l'utilisateur, le jour et la météo du jour. Le service choisit un morceau, puis prévient l'utilisateur
sur son canal préféré. Brief : [`documentation/TP_reveil_musical.md`](documentation/TP_reveil_musical.md).

## Lancer et tester

```bash
dotnet build                     # échoue sur le moindre avertissement
dotnet test                      # 314 tests, aucun appel réseau
./scripts/coverage.sh            # tests + seuil de couverture, rapport dans coverage/index.html
./licenses/audit.sh && ./licenses/freshness.sh   # gates licences et fraîcheur
dotnet run --project src/ReveilMusical.Api --urls http://localhost:5089
curl -X POST http://localhost:5089/wake-ups -H 'Content-Type: application/json' \
     -d '{"userId":"42","day":"LUNDI","weather":"PLUIE"}'
```

Utilisateurs de démonstration (`appsettings.json`, section `UserService`) : `42` (push), `7` (sms),
`13` (email). Les envois simulés s'écrivent sur la console et dans `outbox/{mail,sms,push}.log`.

## Les quatre exigences et leur traduction

| Exigence | Traduction technique |
|---|---|
| **Musique** : changer de fournisseur vite | Port `IMusicCatalog`, un adaptateur par fournisseur (iTunes, MusicBrainz). L'ordre `Music:Providers` est relu à chaque appel : on change de source sans recompiler ni redémarrer. `trackViewUrl` ne sort pas de l'adaptateur iTunes. |
| **Notification** : un canal par utilisateur | Port `INotificationChannel`, un adaptateur par SDK simulé (mail, SMS, push, chacun avec sa propre interface). Canal identifié par une chaîne : WhatsApp = un adaptateur + `AddNotificationChannel<T>("whatsapp")` (prouvé par `NewChannelTests`). |
| **Légal** : licence et fraîcheur vérifiées | Central Package Management ; `licenses/audit.sh` (liste blanche, bloquant en CI, canari GPL) ; `licenses/freshness.sh`. |
| **Fiabilité** : jamais de silence | Musique : failover entre fournisseurs, cache (quota iTunes de 20 req/min), puis playlist locale codée en dur. Canal : cascade vers les autres canaux de l'utilisateur, puis alerte opérateur. |

## Règle de choix du morceau

Le brief ne dit pas comment le jour intervient. Le service utilisateur renvoie un morceau par météo,
des surcharges par **jour + météo** (ex. `LUNDI+PLUIE → Manic Monday`) et un morceau de secours.
On essaie du plus précis au plus général : jour+météo, puis météo, puis secours. Chaque morceau est
cherché chez le fournisseur actif (premier résultat) ; s'il est introuvable, on passe au niveau
suivant, soit trois recherches au plus. Si aucun fournisseur ne répond, un morceau de la playlist
locale part quand même, en mode dégradé (`degraded: true`).

## Architecture

```
src/
  ReveilMusical.Domain           modèle, règles, ports : BCL uniquement
  ReveilMusical.Application      cas d'usage (Facade), sélection du morceau, envoi en cascade
  ReveilMusical.Infrastructure   adaptateurs internal : iTunes, MusicBrainz, cache, failover, canaux
  ReveilMusical.FakeVendors      trois « SDK tiers » simulés (mail, SMS, push)
  ReveilMusical.Api              composition root + endpoint HTTP
```

Dépendances vers le bas uniquement ; aucun `new` concret : adaptateurs `internal`, `Program.cs` seul
point de composition (vérifié par `ArchitectureTests` et `AdapterIsolationTests`).

| Pattern | Où |
|---|---|
| **Adapter** | `ITunesCatalog`, `MusicBrainzCatalog`, `EmailChannelAdapter`, `SmsChannelAdapter`, `PushChannelAdapter` |
| **Facade** | `TriggerWakeUpUseCase` |
| **Strategy** + **Factory** | `INotificationChannel`, résolu à l'exécution par `KeyedNotificationChannelResolver` |
| **Decorator** | `CachedMusicCatalog`, `ResilientNotificationChannel` (cache, réessai, disjoncteur) |
| **Composite** | `FailoverMusicCatalog` : fournisseurs essayés dans l'ordre |

## Dépendances et licences

Scan du 2026-10-08. En CI, `audit.sh` bloque toute licence hors liste blanche (MIT, Apache-2.0,
BSD-3-Clause) ; `freshness.sh` bloque un paquet vulnérable, déprécié ou en retard d'une majeure.

| Paquet | Rôle | Licence | Installé | Dernière stable |
|---|---|---|---|---|
| Microsoft.Extensions.DependencyInjection(.Abstractions) | conteneur IoC | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Logging.Abstractions | journalisation | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Options (+ .ConfigurationExtensions, .DataAnnotations) | options validées | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Caching.Memory | cache des recherches | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Configuration | configuration (tests) | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Http.Resilience | `IHttpClientFactory` + Polly HTTP | MIT | 10.10.0 | 10.10.0 |
| Polly.Extensions, Polly.RateLimiting | résilience des canaux, quotas | BSD-3-Clause | 8.8.0 | 8.8.0 |
| xunit.v3 (+ .assert, .extensibility.core) | tests | Apache-2.0 | 4.0.1 | 4.0.1 |
| coverlet.MTP | couverture | MIT | 10.1.0 | 10.1.0 |
| Microsoft.AspNetCore.Mvc.Testing | hôte en mémoire (E2E) | MIT | 10.0.12 | 10.0.12 |
| nuget-license (outil) | audit de licences | Apache-2.0 | 4.0.18 | 4.0.18 |
| dotnet-reportgenerator-globaltool (outil) | rapport de couverture | Apache-2.0 | 5.5.11 | 5.5.11 |

SDK .NET 10.0.401 et ASP.NET Core : MIT. Chaîne transitive : 67 paquets (55 MIT, 9 Apache-2.0,
3 BSD-3-Clause), aucune licence copyleft ni propriétaire. L'hôte n'en publie que 12, avec leurs
avis dans `THIRD-PARTY-NOTICES.txt`.

| Composant qui pose question | Décision |
|---|---|
| `dotnet-project-licenses` (outil de TP-meteo), dépôt abandonné | Remplacé par `nuget-license`, maintenu. |
| `Microsoft.ApplicationInsights` 2.23.0 (transitif, télémétrie de la plateforme de test) | Tests uniquement ; `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` en CI. |
| `Microsoft.Testing.Platform` 2.4.x (2.5.1 disponible) | Choix de xunit.v3, tests uniquement : retard mineur accepté. |
| `System.Threading.RateLimiting` 8.0.0 (via Polly.RateLimiting) | Non publié : l'hôte utilise la version 10 du framework partagé. |
| `Microsoft.Bcl.AsyncInterfaces` 6.0.0 (transitif) | Tests uniquement, sans vulnérabilité ni dépréciation. |
| Polly (BSD-3-Clause) : avis de copyright obligatoire | Fourni par `THIRD-PARTY-NOTICES.txt`, copié dans l'image Docker. |

## Tests et couverture

314 tests hors ligne (transport HTTP simulé). Couverture : **97,4 % des lignes, 93,7 % des
branches** (seuil bloquant 90 % / 80 %). Une suite de contrat par port (`ReveilMusical.TestSupport`)
est héritée par chaque adaptateur, décorateur et fake.
