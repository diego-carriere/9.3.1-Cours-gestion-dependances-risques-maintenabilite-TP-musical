# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Les manifestes d'abord, sans le code : `dotnet restore` reste en cache tant qu'aucune version
# de paquet ne change. Seuls les projets nécessaires à l'hôte sont copiés (pas les tests).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ReveilMusical.Domain/ReveilMusical.Domain.csproj src/ReveilMusical.Domain/
COPY src/ReveilMusical.Application/ReveilMusical.Application.csproj src/ReveilMusical.Application/
COPY src/ReveilMusical.FakeVendors/ReveilMusical.FakeVendors.csproj src/ReveilMusical.FakeVendors/
COPY src/ReveilMusical.Infrastructure/ReveilMusical.Infrastructure.csproj src/ReveilMusical.Infrastructure/
COPY src/ReveilMusical.Api/ReveilMusical.Api.csproj src/ReveilMusical.Api/
RUN dotnet restore src/ReveilMusical.Api/ReveilMusical.Api.csproj

COPY src/ src/
RUN dotnet publish src/ReveilMusical.Api/ReveilMusical.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# MIT et BSD-3-Clause : l'avis de copyright et la licence accompagnent toute distribution binaire.
# Généré par licenses/notices.sh, vérifié à jour par la CI.
COPY THIRD-PARTY-NOTICES.txt .

# Les SDK simulés écrivent leurs envois dans outbox/ : le dossier doit appartenir à l'utilisateur
# non-root de l'image.
RUN mkdir -p /app/outbox && chown "$APP_UID" /app/outbox

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# Utilisateur non-root fourni par l'image de base .NET.
USER $APP_UID

# Le User-Agent MusicBrainz vient d'appsettings.json ; surchargeable par
# -e Music__MusicBrainz__UserAgent="MonApp/1.0 ( contact )".
ENTRYPOINT ["dotnet", "ReveilMusical.Api.dll"]
