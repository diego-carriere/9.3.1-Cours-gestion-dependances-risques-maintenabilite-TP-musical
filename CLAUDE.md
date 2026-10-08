# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Reading documentation: open the Markdown, not the PDF

Every document in `documentation/` exists as both `.md` and `.pdf`. Always open the `.md` to save tokens: the
PDF costs far more for the same content. The `.md` files are text extractions, so diagrams show up as garbled
`<!-- Start of picture text -->` callouts. Open the matching PDF page (with `pages`) only when such a diagram
actually matters to the task and its text is unreadable.

## Repository purpose

"Réveil musical", the second practical exercise of the course "Gestion des dépendances, risques et
maintenabilité". It is a standalone repo, split from the sibling course repo `../TP-cours/`, whose `TP-meteo/`
holds the first exercise (TP1–TP4). No code exists yet.

- `documentation/TP_reveil_musical.md`: the brief, summarised below.
- `documentation/Support J2.md`: the course deck. It repeats all of `Support J1.md` and then adds Day 2
  (boundary/seam, Adapter/Facade/Strategy/Factory, licences, semver, lock-in). Read J2 alone.

## Stack and conventions

.NET 10, set up like `../TP-cours/TP-meteo/`. Copy from it instead of reinventing:

- `global.json` (SDK 10.0.x, `rollForward: latestFeature`), `Directory.Build.props` (nullable,
  `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `IDE0005` unused-using as error),
  `Directory.Packages.props` (central package management with transitive pinning), and a `.slnx` with `src/`
  and `tests/`.
- Layered projects (Domain ← Application ← Infrastructure, plus a host as the composition root), each depending
  only downward. Infrastructure adapters are `internal` and exposed to their test project only through
  `InternalsVisibleTo`, so the composition root physically cannot `new` one. Each layer registers itself through
  an `IServiceCollection` extension.
- Microsoft.Extensions.DependencyInjection, options bound from `appsettings.json` and validated, and HTTP through
  `IHttpClientFactory` (TP-meteo adds `Microsoft.Extensions.Http.Resilience`).
- xUnit + coverlet.collector. Tests run offline with a faked HTTP transport, and one contract suite runs against
  every implementation of a port.
- Licence gate: TP-meteo's `licenses/audit.sh` (`dotnet-project-licenses` as a local tool, a whitelist, CI
  failure on anything else, plus a GPL canary) is the model.

Commands, from the repo root, once the solution exists:

- `dotnet build`: fails on any warning
- `dotnet test`; for a single test, `dotnet test --filter "FullyQualifiedName~<Class>.<Method>"`
- `dotnet test --collect:"XPlat Code Coverage"`: coverage (the brief asks for good coverage)
- `dotnet list package --include-transitive --outdated` (and `--vulnerable`): input for the README table

Commits use plain conventional commits, e.g. `feat(infra): iTunes adapter`, with no `[TP-…]` prefix. Never add
Claude as a co-author: no `Co-Authored-By: Claude …` trailer in commit messages.

Working discipline carried over from TP-meteo: settle the architectural choices (patterns, SOLID, conventions)
and justify each against the brief's constraints in `README.md` before coding. Keep `README.md` in sync with the
code.

## The brief: what is evaluated

Four non-negotiable business needs. Their technical translation is ours, and the grade is on how they show in
the code:

- **Music provider swappable quickly.**
  - iTunes Search: `GET https://itunes.apple.com/search?term=<track>&media=music&limit=5`. No key, about 20
    requests/minute, which must be respected with a cache. `trackViewUrl` is iTunes-specific and must not leak
    into the domain.
  - MusicBrainz: `GET https://musicbrainz.org/ws/2/recording?query=<track>&fmt=json`. Requests without an
    identifiable User-Agent (app name + contact) are rejected. As with MET Norway in TP-meteo, the contact
    e-mail lives in configuration.
  - A hard-coded local fallback list, used when no provider is available.
- **Notification channel chosen per user.** Email, SMS and push are simulated by hand-written mocks (console or
  log file, no real sending). Each mock deliberately has a *different* interface, and adapters bring them back
  to one common interface. WhatsApp and voice call are planned, so a new channel must mean a new adapter plus a
  registration, nothing else.
- **Legal.** No external component without first checking its licence and freshness.
- **Reliability.** An outage of the music provider or of the channel must never prevent the wake-up. Degraded
  mode is acceptable; silence is not.

Entry point: the call that triggers the send. Scheduling is not coded. The call receives the user ID, the day of
the week and today's weather type (`SOLEIL` / `PLUIE` / `NEIGE` / `NUAGEUX`). These are inputs: there is no
weather API. An internal user service (mocked, behind an interface, treated like any other provider) returns,
from the ID, the user's track for each weather type, a fallback track for uncovered cases, and the preferred
channel. The brief never says how the day of the week affects the choice: decide it and document it in the
README.

Explicit architecture rules: no domain class knows a technical detail of a particular provider or channel, and
no concrete implementation is instantiated with `new` (IoC/DI).

Deliverables:
- the full source in this Git repo;
- a `README.md` listing every package/SDK with its licence, installed version and freshness (latest stable),
  plus a justification for any questionable component (copyleft, old version...);
- unit tests with good coverage.
