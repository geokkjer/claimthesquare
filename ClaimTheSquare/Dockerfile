# ═══════════════════════════════════════════════════════════
# multi-stage: SDK bygger, aspnet kjører.
# Sluttimaget inneholder KUN kjøretiden — ingen kompilator.
#
# Bygget skjer fra repo-rota (se compose.yml / ci.yml), fordi Dockerfile-en
# trenger solution-filen og begge .csproj-filene for å restorre riktig.
# ═══════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore FØRST, og bare fra prosjektfilene. Docker cacher dette laget,
# så restore kjører kun når en .csproj faktisk endres.
COPY ClaimTheSquare.slnx ./
COPY ClaimTheSquare/ClaimTheSquare.csproj ClaimTheSquare/
COPY tests/ClaimTheSquare.Tests/ClaimTheSquare.Tests.csproj tests/ClaimTheSquare.Tests/
# Debug OG Release: asset-filen NuGet skriver er konfigurasjonsspesifikk.
# Restorer du bare Debug og publiserer Release, nekter NuGet å finne pakkene
# med NETSDK1064 — «Package X, version Y was not found».
# (Merk: `dotnet restore` har ingen `-c`; konfigurasjonen må skrives som en
# property. Denne detaljen koster et kvarter første gang.)
RUN dotnet restore && dotnet restore -p:Configuration=Release

COPY . .

# Versjonen skrives inn i imaget ved bygg. Uten --build-arg blir den "dev".
# Dette er identitetskortet deploy-jobben leser etter en utrulling.
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION

# Testene kjører i CI-porten. Her publiserer vi bare — én jobb, ett ansvar.
RUN dotnet publish ClaimTheSquare/ClaimTheSquare.csproj -c Release -o /app/publish

# ── Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ARG er STAGE-SKOPET. Uten denne linja arver runtime-stagen ingen APP_VERSION
# fra build-stagen — ARG gjelder bare der den er deklarert. Uten dette svarer
# /health med "dev" uansett hva du bygget med, og røyk-testen i ci.yml kan ikke
# bevise at porten kjører *dette* bygget. Det er akkurat den egenskapen
# identitetskortet er ment for å ha.
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION

# Npgsql laster libgssapi_krb5.so.2 ved oppstart. Merk pakkenavnet:
# libgssapi-krb5-2 inneholder .so.2 — libkrb5-3 gjør det IKKE.
# wget brukes av healthchecken i compose.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 wget \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Ikke kjør som root.
RUN useradd -m appuser && chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ClaimTheSquare.dll"]
