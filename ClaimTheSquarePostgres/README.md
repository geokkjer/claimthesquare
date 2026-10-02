# ClaimTheSquarePostgres

Referanseappen for **uke 2, fredag** i `devops-mini`: en liten, komplett
fullstack-app som skal bygges av CI og deployes til en ekte server.

**Appen er ikke poenget — flyten er.** Koden er bevisst liten og lesbar, slik at
ingen skal trenge å forstå den for å følge en utrulling. Det som betyr noe er at
den har de egenskapene en deploy-leksjon trenger:

- **Ett image.** API-et serverer både `/text-objects` og frontenden
  (`wwwroot/index.html`) fra samme origin. Ingen egen frontend-container, ingen
  gateway, ingen CORS.
- **Ett bygg.** Alt går gjennom én `Dockerfile` med multi-stage.
- **Ett identitetskort.** `GET /health` og `GET /version.json` svarer med
  `APP_VERSION`, som skrives inn i imaget ved bygg.
- **Én tilstand.** All data ligger i Postgres, så containeren kan byttes fritt.

## Hva appen gjør

Et 8×8-rutenett. Du klikker en ledig rute, fyller inn tekst og to farger, og
ruten blir «claimed». Frontenden er én `index.html` med vanilla JS + axios, og
kaller `GET /text-objects` og `POST /text-objects` med **relativ sti**.

Det siste er hele arkitekturen i én detalj: fordi stien er relativ, må API-et
servere frontenden selv. Det er derfor `app.UseDefaultFiles()` og
`app.UseStaticFiles()` står i `Program.cs` — og det er derfor hele appen er
**én container på én port**.

| Metode | Sti | Body / respons |
|---|---|---|
| `GET` | `/text-objects` | `[{ index, text, foreColor, backColor }]` |
| `POST` | `/text-objects` | `{ index, text, foreColor, backColor }` → `true` |
| `GET` | `/health` | `{ "status": "ok", "version": "sha-…" }` |
| `GET` | `/version.json` | `{ "version": "sha-…" }` |
| `GET` | `/` | frontenden |

## Struktur

```text
ClaimTheSquarePostgres/
├── ClaimTheSquare/              API-et (minimal API + Dapper + Npgsql)
│   ├── Data/TextObjectRepository.cs   all SQL
│   ├── ViewModel/TextObject.cs        modellen
│   ├── wwwroot/index.html             frontenden (hentet fra GetAcademy)
│   ├── Dockerfile                     multi-stage, ikke-root
│   └── Program.cs                     ruter + /health + /version.json
├── tests/ClaimTheSquare.Tests/  fire xUnit-tester (kjører i CI-porten)
├── db/init/01_schema.sql        skjemaet — eies av DATABASEN i prod
├── compose.yml                  dev: bygger API-et
├── compose.prod.yml             prod-sim: image: fra GHCR, ingen build
├── .github/workflows/ci.yml     fire porter + deploy over SSH
└── .env.example                 kontrakten om hvilke variabler som finnes
```

## Kjøre lokalt

```bash
cp .env.example .env
podman compose up -d --build
curl --fail http://localhost:8080/health
```

Åpne <http://localhost:8080> og fyll en rute. Verifiser at den overlever:

```bash
curl -s http://localhost:8080/text-objects
podman compose down          # beholder data
podman compose up -d         # ruten er der fortsatt
podman compose down -v       # sletter databasen
```

Vil du se dataene i et GUI: `podman compose --profile tools up -d`, deretter
<http://localhost:5050> med `host=db`, `port=5432`.

## Prod-sim: kjør et image du ikke bygde selv

Dette er flyten fredagen trener på. `API_IMAGE` (navn uten tag) og `IMAGE_TAG`
(tag) kommer fra `.env`, og **å endre `IMAGE_TAG` er hele deployen**:

```bash
# .env
API_IMAGE=ghcr.io/<bruker>/<repo>
IMAGE_TAG=sha-a1b2c3d

podman compose -f compose.prod.yml pull
podman compose -f compose.prod.yml up -d
```

De fire kontrollene — hver påstår sitt, og de skal være enige:

```bash
curl --fail  http://localhost:8080/health         # lever prosessen?
curl --silent http://localhost:8080/version.json  # hvilket bygg sier den?
curl --fail  http://localhost:8080/text-objects   # lever hele kjeden + Postgres?
podman inspect "$(podman compose -f compose.prod.yml ps -q api)" \
  --format '{{.Config.Image}}'                    # fasit: hva startet maskinen?
```

## Hvem eier skjemaet?

Bevisst ulikt i dev og prod, og det er en del av pensum:

| | dev (`compose.yml`) | prod (`compose.prod.yml`) |
|---|---|---|
| Skjemaet lages av | appen (`MIGRATE_ON_STARTUP=true`) | `db/init/01_schema.sql`, kjørt av postgres-imaget |
| Hvorfor | en ny database skal virke uten at noen husker rekkefølgen | appen skal ikke endre skjemaet sitt selv |
| På ekte | greit med én instans | migreringer som eget steg i pipelinen — én runner, én lås, ingen kappløp |

Init-skriptet kjører **bare mot et tomt volum**. Har du kjørt `up` én gang, må du
`down -v` for å få skjemaendringer med deg — og det sletter dataene.

## Feller som koster et kvarter

| Symptom | Årsak | Fiks |
|---|---|---|
| `/text-objects` svarer 500, `/health` er grønn | init-skriptet kjørte aldri, tabellen finnes ikke | `down -v` og `up` igjen, og sjekk loggen: `podman compose logs db \| grep schema` |
| Init-skriptet kjører ikke selv på tomt volum | fila er ikke lesbar for containeren | `chmod 644 db/init/*.sql` — en bind-mount beholder rettighetene fra vertsmaskinen |
| `NETSDK1064: Package Dapper … was not found` | NuGet-restore ble kjørt i Debug, publisering i Release | `dotnet restore -p:Configuration=Release` (merk: ingen `-c` på restore) |
| `/text-objects` svarer 500 ved første kall, men oppstarten så sunn ut | connection stringen er lat — Npgsql kobler først opp ved første spørring | bruk tjenestenavnet `db`, ikke `localhost`, og `depends_on: service_healthy` |
| `Connection refused` mot 5432 | API-containeren ser seg selv, ikke vertsmaskinen | inne i `app-network` er `db` et hostname |
| `wget: not found` i healthchecken | wget mangler i runtime-imaget | det er installert i Dockerfile-en — bygg på nytt |
| Helsecheck feiler med 405 | `--spider` sender HEAD, `MapGet` svarer 405 | `wget -q -O /dev/null` (GET) |
| Avvik mellom `/version.json` og `inspect` | du ser på feil stack, eller noen bygde lokalt | lokalt bygg har alltid `dev`; `.env` bestemmer hvilket *image* som hentes, ikke hva som står i det |

## Videre lesning

- Fredagens leksjon: [`../uke-2/5_fredag_oppgaver.md`](../uke-2/5_fredag_oppgaver.md)
  — CI, GHCR, prod-sim, VPS, DNS, nginx, TLS og rollback.
- Byggeappen fra bunnen: guiden i `~/Projects/GET/ClaimTheSquarePostgres/README.md`
  (originalversjonen) og økt-repoene `H25_3.7`/`H25_3.9` hos GetAcademy.
