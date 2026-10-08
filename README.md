# 9.3.1-Cours-gestion-dependances-risques-maintenabilite-TP-musical

Créateur : Diego Carrière

Service « Réveil musical » : à l'heure prévue, un ordonnanceur (hors périmètre) appelle
`POST /wake-ups` avec l'ID de l'utilisateur, le jour et la météo du jour. Le service choisit un
morceau d'après les préférences de l'utilisateur, puis le prévient sur son canal préféré. Brief
complet : [`documentation/TP_reveil_musical.md`](documentation/TP_reveil_musical.md).

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
