# 9.3.1-Cours-gestion-dependances-risques-maintenabilite-TP-musical

Créateur : Diego Carrière

Service « Réveil musical » : à l'heure prévue, un ordonnanceur (hors périmètre) appelle
`POST /wake-ups` avec l'ID de l'utilisateur, le jour et la météo du jour. Le service choisit un
morceau d'après les préférences de l'utilisateur, puis le prévient sur son canal préféré. Brief
complet : [`documentation/TP_reveil_musical.md`](documentation/TP_reveil_musical.md).

## Lancer et tester

Prérequis : SDK .NET 10 (`global.json`), `jq` pour les scripts.

```bash
dotnet build                     # échoue sur le moindre avertissement
dotnet test                      # 259 tests, aucun appel réseau
./scripts/coverage.sh            # tests + seuil de couverture, rapport dans coverage/index.html
./licenses/audit.sh              # gate de licences (+ ./licenses/audit-canary.sh)
./licenses/freshness.sh          # gate de fraîcheur
dotnet run --project src/ReveilMusical.Api --urls http://localhost:5089
```

Un test précis, en ciblant son projet (sous Microsoft.Testing.Platform, un projet où le filtre ne
trouve aucun test compte comme un échec) :
`dotnet test --project tests/ReveilMusical.Application.Tests --filter "FullyQualifiedName~TrackSelectorTests"`.

L'appel que déclencherait l'ordonnanceur :

```bash
curl -X POST http://localhost:5089/wake-ups -H 'Content-Type: application/json' \
     -d '{"userId":"42","day":"LUNDI","weather":"PLUIE"}'
```

```json
{
  "userId": "42", "delivered": true, "degraded": false,
  "track": { "title": "Blues", "artist": "Aya Nakamura", "source": "catalog",
             "preference": "day-and-weather", "keyword": "blues" },
  "notification": { "preferredChannel": "push", "deliveredOn": "push", "operatorAlerted": false,
                    "attempts": [ { "channel": "push", "status": "delivered", "detail": "push-5815…" } ] }
}
```

Utilisateurs de démonstration (`appsettings.json`, section `UserService`, le service utilisateur simulé) :

| ID | Nom | Canal préféré | Contacts | Particularité |
|---|---|---|---|---|
| 42 | Alice | push | push, sms, email | surcharge `LUNDI+PLUIE` → `[monday, blues]` |
| 7 | Bruno | sms | sms, email | couvre `NUAGEUX` et une surcharge `VENDREDI+SOLEIL` ; les autres cas passent par ses mots-clés de secours |
| 13 | Chloé | email | email seul | aucune cascade possible : une panne mail déclenche l'alerte opérateur |

Les envois simulés s'écrivent sur la console et dans `outbox/{mail,sms,push}.log`, relatif au
dossier de travail (`src/ReveilMusical.Api/outbox/` avec `dotnet run`). Pour voir les modes
dégradés, sans recompiler :

| Scénario | Réglage |
|---|---|
| Changer de fournisseur musical, à chaud | `Music:Providers` : `["musicbrainz", "itunes"]` |
| Aucun fournisseur (playlist locale) | `Music:ITunes:BaseUrl` et `Music:MusicBrainz:BaseUrl` vers un hôte injoignable |
| Panne du canal préféré (cascade) | `Vendors:Push:SimulateOutage` : `true` |
| Panne de tous les canaux (503 + alerte) | `SimulateOutage` à `true` sous `Vendors:Mail`, `Vendors:Sms` et `Vendors:Push` |
| Panne du service utilisateur (503) | `UserService:SimulateOutage` : `true` |

Seul `Music:Providers` est relu à chaud ; les autres réglages se lisent au démarrage (variables
d'environnement possibles, par exemple `Vendors__Push__SimulateOutage=true`).

## Les quatre exigences et leur traduction

| Exigence du brief | Traduction technique |
|---|---|
| **Musique** : tester plusieurs sources, en changer vite | Port `IMusicCatalog` ; un adaptateur par fournisseur (iTunes, MusicBrainz). L'ordre de repli `Music:Providers` est relu à chaque appel, ce qui permet de changer de source sans recompiler ni redémarrer. |
| **Notification** : un canal par utilisateur, d'autres à venir | Port `INotificationChannel` ; un adaptateur par SDK (mail, SMS, push). Le canal est identifié par une chaîne (`ChannelId`), pas par une enum : ajouter WhatsApp, c'est un adaptateur et une ligne d'enregistrement. |
| **Légal** : aucun composant sans vérifier sa licence et sa fraîcheur | Central Package Management ; `licenses/audit.sh` (liste blanche, bloquant en CI, canari GPL) ; `licenses/freshness.sh` (paquets vulnérables, dépréciés ou en retard d'une version majeure). |
| **Fiabilité** : jamais de silence | Musique : failover entre fournisseurs, puis cache périmé, puis playlist locale codée en dur. Canal : cascade vers les autres canaux de l'utilisateur, puis alerte opérateur. |

## Règle de choix du morceau

Le brief ne dit pas comment le jour de la semaine intervient. Règle retenue :

1. Le service utilisateur renvoie, pour chaque utilisateur, des **mots-clés** :
   - par météo (par exemple `SOLEIL → [soleil, beau temps, sunshine]`) ;
   - des surcharges par **jour + météo** (par exemple `LUNDI+PLUIE → [monday, blues]`) ;
   - des mots-clés **de secours**.
2. Le jeu de mots-clés est résolu du plus précis au plus général : jour+météo, puis météo, puis secours.
3. Un mot-clé est tiré au hasard, puis recherché chez le fournisseur musical actif. Un morceau est
   ensuite tiré au hasard parmi les résultats.
4. Si une recherche ne renvoie rien, on tire un autre mot-clé du même jeu, puis on passe aux mots-clés de
   secours. Le nombre de recherches est borné (`Wakeup:MaxSearchAttempts`) pour protéger les quotas
   des fournisseurs.
5. Si aucun fournisseur ne répond (panne, quota épuisé), on prend un morceau de la **playlist locale**,
   étiquetée par météo. Le réveil part quand même, en mode dégradé.

Exemple pour un profil donné :

```
parMeteo        SOLEIL -> [soleil, beau temps, sunshine]   PLUIE -> [pluie, rain, storm]
parJourEtMeteo  LUNDI+PLUIE -> [monday, blues]
secours         [wake up, morning]

LUNDI + PLUIE  -> [monday, blues]            (jour+météo)
MARDI + PLUIE  -> [pluie, rain, storm]       (météo)
MARDI + NEIGE  -> [wake up, morning]         (secours)
```

Le hasard passe par le port `IRandom` : en test, il est scripté, donc chaque tirage est déterministe.

## Architecture en couches

```
src/
  ReveilMusical.Domain           modèle, règles, ports : BCL uniquement
  ReveilMusical.Application      cas d'usage (Facade), sélection du morceau, envoi en cascade
  ReveilMusical.Infrastructure   adaptateurs internal : iTunes, MusicBrainz, cache, failover, canaux, profils
  ReveilMusical.FakeVendors      trois « SDK tiers » simulés (mail, SMS, push)
  ReveilMusical.Api              composition root + endpoint HTTP
```

Graphe de compilation (la flèche va vers ce dont on dépend) :

```
ReveilMusical.Api ──► ReveilMusical.Application ──► ReveilMusical.Domain
        │                                                  ▲
        └──► ReveilMusical.Infrastructure ─────────────────┘
                        │
                        └──► ReveilMusical.FakeVendors   (joue le rôle d'un paquet externe)
```

L'Infrastructure implémente les ports du Domaine sans connaître l'Application. Les SDK simulés ne
connaissent ni le Domaine ni la DI : ils ressemblent à des bibliothèques tierces, chacune avec son
propre style d'interface. Des tests d'architecture vérifient ces règles par réflexion
(`ReveilMusical.Domain.Tests.ArchitectureTests`).

## Patterns, et le problème que chacun résout

Support J2 : « un pattern n'est pas un objectif en soi ». Chacun ci-dessous répond à un problème du brief.

| Pattern | Problème | Où |
|---|---|---|
| **Adapter** | Chaque fournisseur ou SDK parle son propre format | `ITunesCatalog`, `MusicBrainzCatalog`, `EmailChannelAdapter`, `SmsChannelAdapter`, `PushChannelAdapter` |
| **Facade** | Le déclenchement orchestre profil, musique et notification | `TriggerWakeUpUseCase` |
| **Strategy** | Le comportement d'envoi dépend du canal de chaque utilisateur | `INotificationChannel`, choisi à l'exécution par `ChannelId` |
| **Factory** (injectée dans la DI) | Retrouver un canal à partir d'un identifiant connu seulement à l'exécution | `KeyedNotificationChannelResolver` |
| **Decorator** | Ajouter cache et résilience sans toucher aux adaptateurs | `CachedMusicCatalog`, `ResilientNotificationChannel` |
| **Composite** (+ chaîne de repli) | Plusieurs fournisseurs musicaux derrière un seul port, essayés dans l'ordre | `FailoverMusicCatalog` |

Pas de pattern pour la résolution jour+météo → météo → secours : trois recherches dans un
dictionnaire n'ont pas besoin d'une Chain of Responsibility.

## Flux d'un appel `POST /wake-ups`

1. **Api** : valide le JSON (`day` : `LUNDI`…`DIMANCHE`, `weather` : `SOLEIL|PLUIE|NEIGE|NUAGEUX`,
   insensibles à la casse). Une entrée invalide donne 400.
2. **`TriggerWakeUpUseCase`** :
   1. il charge le profil (`IUserProfileProvider`). Utilisateur inconnu → 404 ; service utilisateur en
      panne → 503 ;
   2. il choisit le morceau (`TrackSelector`), comme décrit plus haut. Cette étape **ne peut pas échouer** ;
   3. il envoie (`NotificationDispatcher`) : canal préféré, puis `Wakeup:FallbackChannels` filtré sur les
      canaux pour lesquels l'utilisateur a un contact. Le premier succès arrête la cascade. Si tout
      échoue, il appelle `IOperatorAlerter`.
3. **Api** : 200 avec le rapport (morceau, source, canal utilisé, tentatives, `degraded`), ou 503 avec
   `Retry-After` si aucun canal n'a abouti (l'opérateur a alors été alerté).

## Mode dégradé

| Panne | Comportement | Visible dans la réponse |
|---|---|---|
| iTunes en panne ou quota atteint | MusicBrainz prend le relais (failover) | `trackSource: catalog` |
| Fournisseur en panne, recherche déjà faite | Résultat périmé servi depuis le cache | `trackSource: catalog` |
| Tous les fournisseurs en panne | Playlist locale | `trackSource: local-playlist`, `degraded: true` |
| Canal préféré en panne | Canal suivant de la cascade | `channel` ≠ canal préféré, `degraded: true` |
| Tous les canaux en panne | Alerte opérateur (log `Critical`) | 503 + `Retry-After` |

## Correspondance cours → TP

| Concept (Supports J1/J2) | Où il se voit dans ce code |
|---|---|
| Couplage faible, cohésion forte | Un port par responsabilité (`IMusicCatalog`, `INotificationChannel`, `IUserProfileProvider`...) ; un adaptateur par fournisseur ou SDK |
| Architecture en couches | 5 projets ; `ArchitectureTests` (le Domaine ne référence que la BCL, l'Application ni ASP.NET, ni Polly, ni l'Infrastructure, ni les SDK) |
| IoC/DI, pas de `new` | Injection par constructeur partout ; adaptateurs `internal` ; `ActivatorUtilities` pour les décorateurs ; `AdapterIsolationTests` |
| Durées de vie, dépendance captive | Tableau ci-dessous ; `ValidateScopes` + `ValidateOnBuild` dans `Program.cs`, revérifiés par `CompositionTests` |
| Dépendances cachées | `IClock`, `IRandom`, options validées au démarrage, culture invariante, codes français dans le Domaine |
| Boundary et seam | Les ports du Domaine sont la frontière ; `StubHttpMessageHandler`/`FakeUpstream` et les interfaces des SDK simulés sont les points de couture des tests |
| Adapter, Facade, Strategy, Factory | Voir « Patterns » |
| SPOF, circuit breaker, mode dégradé | Failover entre fournisseurs, cache périmé, playlist locale, cascade de canaux, disjoncteur par canal et par fournisseur, alerte |
| Licences, transitives, semver | `licenses/audit.sh`, `licenses/freshness.sh`, tableau « Dépendances et licences » |
| Lock-in, souveraineté | Fournisseurs derrière une interface, changeables par configuration ; télémétrie de test désactivée en CI ; bilan ci-dessous |

## Composition root et injection

`src/ReveilMusical.Api/Program.cs` est le seul fichier qui relie des implémentations à des
abstractions : `AddApplication(...)` puis `AddInfrastructure(configuration)`. La règle « aucune
implémentation concrète instanciée avec `new` » est mécanique, pas seulement affirmée :

1. Les adaptateurs, décorateurs et DTO de l'Infrastructure sont `internal` : l'hôte ne peut pas
   compiler `new ITunesCatalog(...)`. Seules la composition et les classes d'options sont
   publiques (vérifié par `AdapterIsolationTests`).
2. L'Application ne référence pas l'Infrastructure : elle ne peut même pas nommer un adaptateur.
3. Les décorateurs (`CachedMusicCatalog`, `ResilientNotificationChannel`) sont construits par le
   conteneur (`ActivatorUtilities.CreateInstance`), qui ne reçoit explicitement que l'objet décoré.

Injection **par constructeur** partout. **Par méthode** pour le seul `CancellationToken`, un besoin
lié à un appel (la forme « ponctuelle » de Support J1). Par propriété : nulle part.

### Table des durées de vie

| Service | Durée de vie | Pourquoi |
|---|---|---|
| `ITriggerWakeUpUseCase`, `TrackSelector`, `NotificationDispatcher` | Scoped | Une unité de travail par réveil, sans état partagé. |
| `IMusicCatalog` → `FailoverMusicCatalog` ; fournisseurs à clé → `CachedMusicCatalog` | Transient | Sans état propre. L'ordre des fournisseurs est relu à chaque appel. |
| `ITunesCatalog`, `MusicBrainzCatalog` | Transient (`AddHttpClient`) | Client typé jetable ; le pool de handlers (sockets, DNS) est mutualisé par `IHttpClientFactory`. |
| `INotificationChannelResolver` | Transient | Factory sans état sur les services à clés. |
| Canaux à clé → `ResilientNotificationChannel` + adaptateur | Singleton | Sans état ; le disjoncteur du canal doit survivre aux requêtes. |
| SDK simulés (`SmtpMailClient`, `ISmsGatewayClient`, `IPushService`) | Singleton | Ils détiennent le verrou de leur fichier de sortie. |
| `IUserProfileProvider`, `IFallbackPlaylist`, `IOperatorAlerter` | Singleton | Données immuables, aucune dépendance scoped. |
| `IClock`, `IRandom`, `IMemoryCache`, options, pipelines Polly, limiteurs de débit | Singleton | L'état qui doit survivre aux requêtes : un quota ou un cache par requête ne protège rien. |

Aucun singleton ne dépend d'un service scoped : pas de dépendance captive, et le conteneur refuse
de démarrer s'il y en avait une.

## Dépendances cachées neutralisées

| Dépendance cachée (Support J1) | Parade |
|---|---|
| Horloge | `IClock` ; `DateTimeOffset.UtcNow` n'apparaît que dans `SystemClock` (et dans les SDK simulés, qui jouent des bibliothèques tierces). |
| Hasard | `IRandom` ; `Random.Shared` n'apparaît que dans `SystemRandom`. En test, `FakeRandom` rend chaque tirage déterministe. |
| Configuration | Tout passe par des options liées et **validées au démarrage** : User-Agent MusicBrainz, clés de fournisseurs, identifiants de canaux, profils. L'application refuse de démarrer plutôt que d'échouer à 6 h du matin. |
| Culture | Hôte en `InvariantGlobalization` ; noms français des jours et des météos dans une table du Domaine, pas dans `CultureInfo("fr-FR")` ; formatage des URL en culture invariante. |
| Système de fichiers | Les dossiers de sortie des SDK simulés viennent de la configuration. |
| État global | Aucun champ statique mutable dans le Domaine, l'Application, l'Infrastructure ou l'hôte (vérifié par réflexion). |

## Gestion des erreurs

`Result<T>` pour les échecs attendus (utilisateur inconnu, fournisseur ou canal en panne) : un
adaptateur ne lève jamais pour eux, ce que les suites de contrat vérifient sur chaque
implémentation. Les bugs lèvent toujours. La traduction en HTTP tient dans un seul fichier,
`Errors/WakeUpErrorMapper.cs`.

| Situation | HTTP |
|---|---|
| Entrée invalide (champ absent, jour ou météo inconnus, JSON illisible) | 400, `ProblemDetails` qui nomme chaque champ fautif |
| Utilisateur inconnu | 404 |
| Service utilisateur en panne | 503 + `Retry-After` |
| Réveil remis, même en mode dégradé | 200 + rapport (`degraded: true` le cas échéant) |
| Aucun canal n'a abouti | 503 + `Retry-After` + rapport complet (l'opérateur est déjà alerté) |

## Dépendances et licences

Exigence du brief : aucun composant externe sans vérification préalable de sa **licence** et de sa
**fraîcheur**. Les deux sont des scripts bloquants, identiques en local et en CI :

| Contrôle | Commande | Bloque sur |
|---|---|---|
| Licences | `./licenses/audit.sh` | toute licence, directe ou transitive, absente de [`licenses/allowed-licenses.json`](licenses/allowed-licenses.json) (MIT, Apache-2.0, BSD-3-Clause) ou non identifiée. Le scan brut versionné, [`licenses/licenses.json`](licenses/licenses.json), doit être à jour (la CI le vérifie par `git diff`). |
| Contrôle négatif | `./licenses/audit-canary.sh` | le gate doit refuser un paquet GPL-3.0, et pour sa licence : sinon il ne protège plus rien. |
| Fraîcheur | `./licenses/freshness.sh` | paquet vulnérable ou déprécié (direct ou transitif), paquet direct en retard d'une version **majeure**. Un retard mineur n'est qu'un avertissement. En CI, le contrôle tourne aussi chaque semaine : un paquet devient périmé sans qu'on touche au code. |

Central Package Management (`Directory.Packages.props`) avec pinning transitif : une seule version
par paquet pour toute la solution, réponse directe au conflit transitif de Support J1.

### Paquets directs

Scan du 2026-10-08. « Dernière stable » vient de `dotnet list package --outdated`.

| Paquet | Rôle | Projet | Licence | Installé | Dernière stable |
|---|---|---|---|---|---|
| Microsoft.Extensions.DependencyInjection.Abstractions | conteneur IoC (abstractions) | Application | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Logging.Abstractions | journalisation | Application, Infrastructure | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Options | options validées | Application | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Options.ConfigurationExtensions | liaison options ↔ configuration | Infrastructure | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Options.DataAnnotations | validation des options au démarrage | Infrastructure | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Caching.Memory | cache des recherches musicales | Infrastructure | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Http.Resilience | `IHttpClientFactory` + pipeline Polly HTTP | Infrastructure | MIT | 10.10.0 | 10.10.0 |
| Polly.Extensions | pipelines non HTTP des canaux | Infrastructure | BSD-3-Clause | 8.8.0 | 8.8.0 |
| Polly.RateLimiting | quotas iTunes / MusicBrainz | Infrastructure | BSD-3-Clause | 8.8.0 | 8.8.0 |
| xunit.v3 | framework de test | tests | Apache-2.0 | 4.0.1 | 4.0.1 |
| xunit.v3.assert, xunit.v3.extensibility.core | suites de contrat partagées | TestSupport | Apache-2.0 | 4.0.1 | 4.0.1 |
| coverlet.MTP | couverture de code | tests | MIT | 10.1.0 | 10.1.0 |
| Microsoft.AspNetCore.Mvc.Testing | hôte en mémoire pour les E2E | Api.E2ETests | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.DependencyInjection | conteneur concret pour les tests de composition | Application.Tests, Infrastructure.Tests | MIT | 10.0.12 | 10.0.12 |
| Microsoft.Extensions.Configuration | configuration en mémoire pour les tests | Infrastructure.Tests | MIT | 10.0.12 | 10.0.12 |

Outils (`dotnet-tools.json`) : `nuget-license` 4.0.18 (Apache-2.0, à jour) pour l'audit,
`dotnet-reportgenerator-globaltool` 5.5.11 (Apache-2.0, à jour) pour le rapport de couverture.
SDK .NET 10.0.401 et framework partagé ASP.NET Core : MIT.

### Toute la chaîne transitive

**67 paquets** distincts, tests compris : 55 MIT, 9 Apache-2.0, 3 BSD-3-Clause. **Aucune licence
copyleft, aucune licence propriétaire.** L'hôte ne publie que 12 paquets NuGet (MIT et
BSD-3-Clause) ; le reste ne sert qu'aux tests ou est fourni par le framework partagé.

### Ce qui pose question, et pourquoi c'est accepté

| Composant | Question | Décision |
|---|---|---|
| `dotnet-project-licenses` (outil d'audit de TP-meteo) | Son dépôt se déclare **abandonné** et renvoie vers une réécriture. | **Remplacé** par `nuget-license` (même rôle, maintenu, Apache-2.0). La fraîcheur vaut aussi pour l'outillage. |
| `Microsoft.ApplicationInsights` 2.23.0 (transitif) | Télémétrie : la plateforme de test (`Microsoft.Testing.Extensions.Telemetry`, tirée par xunit.v3) peut envoyer des données d'usage à Microsoft. Question de souveraineté (Support J2), et une version 3.x existe. | Tests uniquement, jamais distribué. La CI pose `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` ; à faire aussi en local. La version est le choix de xunit.v3 : on ne la force pas. |
| `Microsoft.Testing.Platform` 2.4.x (transitif) | Retard mineur (2.5.1 disponible). | Choix de xunit.v3 4.0.1, tests uniquement : avertissement accepté. |
| `System.Threading.RateLimiting` 8.0.0 (transitif) | Version ancienne, tirée par `Polly.RateLimiting`. | Seulement dans le graphe de la bibliothèque Infrastructure : dans l'hôte, le framework partagé ASP.NET Core fournit sa propre version 10 (`dotnet nuget why src/ReveilMusical.Api ...` : aucune dépendance de paquet). Rien d'ancien n'est publié. |
| `Microsoft.Bcl.AsyncInterfaces` 6.0.0 (transitif) | Version ancienne. | Tests uniquement, sans vulnérabilité ni dépréciation connue. |
| xunit.v3 plutôt que xunit 2.x | xunit 2.x (TP-meteo) est en maintenance ; v3 impose Microsoft.Testing.Platform. | Choisi pour la fraîcheur. Conséquence : coverlet.MTP remplace coverlet.collector. |
| Polly (BSD-3-Clause) | Obligation : reproduire l'avis de copyright dans la documentation d'une distribution binaire. | À joindre à toute distribution (image Docker comprise). |

## Services externes

Support J1 : « une dépendance n'est pas que du code ». Les deux API musicales sont des dépendances
externes non contrôlées, isolées derrière `IMusicCatalog` et remplaçables par configuration.

| Service | Conditions (vérifiées le 2026-10-08) | Traduction dans le code |
|---|---|---|
| **iTunes Search API** (Apple) | Gratuite, sans clé. Environ **20 requêtes/minute** (« subject to change »). Le contenu promotionnel (extraits, pochettes) ne sert qu'à promouvoir la boutique, avec attribution et un badge iTunes à proximité, en streaming seulement. Source : [performance-partners.apple.com/search-api](https://performance-partners.apple.com/search-api). | Limiteur à 20 req/min, cache 24 h par mot-clé. Seuls le titre et l'artiste sont repris : aucun extrait, aucune pochette, aucun `trackViewUrl` n'est servi, ce qui reste en deçà des usages encadrés. Le passage en production d'un usage promotionnel demanderait une relecture juridique. |
| **MusicBrainz API** (MetaBrainz Foundation) | En moyenne **1 requête/seconde par IP** ; au-delà, 503. Un **User-Agent identifiable** (« Application/version ( contact ) ») est exigé. Les données cœur (enregistrements, titres, artistes) sont sous **CC0**, les données complémentaires sous CC BY-NC-SA 3.0. Sources : [Rate Limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting), [Data License](https://musicbrainz.org/doc/About/Data_License). | Limiteur à 1 req/s avec une petite file d'attente. User-Agent lu en configuration et validé au démarrage. Seules les données cœur (CC0) sont utilisées. |

## Tests et couverture

| Projet | Nature | Tests |
|---|---|---|
| `ReveilMusical.Domain.Tests` | unitaires (value objects, règle jour+météo, message, `Result`) + architecture | 76 |
| `ReveilMusical.Application.Tests` | unitaires sur fakes (sélection, cascade, cas d'usage, options) + contrat des fakes | 35 |
| `ReveilMusical.Infrastructure.Tests` | adaptateurs HTTP sur réponses réelles capturées, SDK simulés, décorateurs, failover, quotas, composition, isolation des DTO | 102 |
| `ReveilMusical.FakeVendors.Tests` | les trois SDK simulés | 18 |
| `ReveilMusical.Api.E2ETests` | hôte complet en mémoire, seul le transport HTTP sortant est simulé | 28 |

**259 tests, aucun appel réseau, environ 5 s en tout.** Couverture du code de production
(`./scripts/coverage.sh`) : **97,3 % des lignes, 92,9 % des branches**, pour un seuil bloquant de
90 % et 80 %. Par assembly : Application 100 %, Api 99,1 %, Infrastructure 98,1 %, Domaine 96,5 %,
FakeVendors 86,8 % (les constructeurs standard d'exception, exigés par l'analyseur, ne servent pas).

Les tests les plus démonstratifs :

- **Une suite de contrat par port** (`ReveilMusical.TestSupport`) : `MusicCatalogContractTests`,
  `NotificationChannelContractTests`, `UserProfileProviderContractTests` et
  `FallbackPlaylistContractTests`. Elles sont héritées par chaque adaptateur, chaque décorateur et
  chaque fake (Liskov). Un fake qui mentirait sur le comportement réel ferait échouer son contrat.
- **`NewChannelTests`** : un canal WhatsApp, absent du code de production, est ajouté par le seul
  test, avec un adaptateur et une ligne d'enregistrement. L'utilisateur qui le préfère est réveillé
  dessus.
- **`ProviderSwitchingTests`** : modifier `Music:Providers` entre deux réveils change de source
  sans redémarrage ; un second réveil identique est servi par le cache (quota iTunes).
- **`CompositionTests`** : le conteneur se construit sans dépendance captive, et l'application
  refuse de démarrer sur une configuration invalide.
- Ce que les E2E ont attrapé et que les tests unitaires ne voyaient pas : Polly journalise le
  résultat de chaque tentative, et le `ToString()` synthétisé du record `Result<T>` levait sur un
  échec. Corrigé, et couvert par `ResultTests.ToString_never_throws_so_a_failure_can_be_logged`.

## Bilan : combien coûte un changement de fournisseur ?

Le debrief lock-in de Support J2, appliqué à ce code :

| Changement | Ce qu'il faut toucher | Domaine / Application / hôte |
|---|---|---|
| Passer d'iTunes à MusicBrainz | Une ligne de configuration, à chaud | 0 fichier |
| Ajouter un troisième fournisseur musical | Un adaptateur et son DTO, une classe d'options, une clé dans `ProviderKeys`, quelques lignes dans `MusicServiceCollectionExtensions`. Ses tests héritent de `MusicCatalogContractTests`. | 0 fichier |
| Ajouter un canal (WhatsApp, appel vocal) | Un adaptateur + `AddNotificationChannel<T>("whatsapp")` (prouvé par `NewChannelTests`) | 0 fichier |
| Remplacer le service utilisateur simulé par un vrai | Un client HTTP implémentant `IUserProfileProvider` | 0 fichier |

Aucun DTO ne fuit hors de son adaptateur (`AdapterIsolationTests`, `ArchitectureTests`) : aucun
test métier ne casse quand un fournisseur change.

## Simplifications assumées

- Les tests tournent hors ligne : les appels réels à iTunes et MusicBrainz n'en font pas partie.
  Les réponses rejouées ont été capturées sur les vraies API, et le service a été essayé à la main
  contre elles (bascule à chaud vers MusicBrainz comprise).
- Une recherche vide est une réponse : elle ne déclenche pas le failover vers le fournisseur
  suivant, seulement le tirage d'un autre mot-clé.
- Pas de cache du profil utilisateur : si le service utilisateur tombe, on ne sait ni qui réveiller
  ni où ; la réponse est un 503, que l'ordonnanceur peut retenter.
- L'alerte opérateur est un log `Critical`. Un pager ou un ticket serait un autre
  `IOperatorAlerter`, sans rien changer ailleurs.
- Le quota iTunes est local au processus : plusieurs instances de l'hôte se partageraient le
  quota réel sans se coordonner. Il faudrait alors un limiteur distribué, ou un cache partagé.
- Seul l'ordre des fournisseurs est relu à chaud ; les autres réglages se lisent au démarrage.
- Le `Dockerfile` est construit par la CI, pas en local (Docker absent de l'environnement de
  développement). L'audit de licences couvre les paquets NuGet, pas la couche système de l'image.
- Les scripts sont en bash : Linux, macOS et CI ; sous Windows, Git Bash ou WSL.
