# Fredag — fra `git push` til en server på internett

> **Økt 10 av 10 · Uke 2 · Arbeidsdag (~6 t) — kort intro, så jobber du.**
> **Dagens mål:** appen din svarer på `https://<dittnavn>.kursdomenet.no`, og en
> `git push` til `main` ruller den ut uten at du rører serveren.
> **Forutsetning:** økt 6–9. Du har en grønn pipeline og har deployet et
> CI-bygget image lokalt med `pull` + `up -d`.
> **Appen:** `ClaimTheSquarePostgres/` i kursrepoet. Den er ferdig — **ikke bygg
> den på nytt i dag.** Koden er ikke poenget; flyten er.
> **M0 flytter den inn i et eget repo først.** Du begynner i kursrepoet, men
> GitHub Actions finner bare workflows i repo-rota, så appen må ut på egen hånd.
> Se «Hvor du starter» — og felle nr. 0 der, som handler om nøyaktig det
> rotmappen-problemet.
> **Windows?** Kommandoene står i bash-form. Bruker du PowerShell, se
> erstatningene i [`../uke-1/00-setup-podman.md`](../uke-1/00-setup-podman.md) →
> «Kommandoer i kurset», og sett tagger i `.env` i stedet for i `$env:`.

---

## Hva som er nytt i dag

Mandag til torsdag kjørte alt på maskinen din. «Produksjon» var laptoppen, og
serveren var et hopp vi snakket om men ikke tok. I dag tar vi det hoppet.

Det er **tre** ting som endrer seg, og ingenting annet:

```
                          ┌─ ett hopp: SSH
git push → pipeline → GHCR ┤
                          └─ ett skap: hemmelighetene bor på serveren

                     + én inngang: nginx på 80/443, appen på 127.0.0.1
```

**Flyten er den samme.** Samme `Dockerfile`, samme tag, samme `pull` og
`up -d`, samme bevis. Det du lærer i dag er hva som skjer når maskinen du
deployer til ikke er din — og hvordan du gjør det uten å logge inn.

### Hvorfor denne appen og ikke `labApi`

`labApi` er en API uten frontend. ClaimTheSquarePostgres serverer **både** appen
og API-et fra samme container, på samme port, samme origin. Det gir en deploy
som er *enklere* å forstå, ikke vanskeligere:

| | `labApi` | ClaimTheSquarePostgres |
|---|---|---|
| Tjenester | `api` + `db` | `api` + `db` |
| Port | 8080 | 8080 |
| Origin | bare API | appen **og** API-et |
| Skjema eies av | appen (`MIGRATE_ON_STARTUP`) | databasen i prod |
| Identitetskort | `/health` → `version` | `/health` + `/version.json` |

Én container, én port, én origin. Det er arkitekturen de fleste små tjenester
faktisk har i produksjon — og det gjør at alt du lærte i går kan gjenbrukes
ord for ord.

---

## Dagens løype
| Milepæl | Bevis |
|---|---|
Intro: hva vi bygger (20 min) | — |
**M1** Repoet på GitHub, grønn pipeline med fire porter | Actions: fire grønne jobber |
**M2** Image i GHCR med to tagger | Packages viser `sha-…` og `latest` |
**M3** Prod-sim lokalt: CI-imaget kjører | fire kontroller, ord for ord enige |
**M4** Server og domene svarer (VPS, DNS, nginx, TLS) | `https://<navn>…` viser rutenettet |
**M5** Deploy-jobben ruller ut uten at du rører serveren | grønn `deploy`-jobb, ny tag i `/version.json` |
**M6** Rollback på under to minutter + feilscenario | tid tatt med stoppeklokke |




---

## M0 — før du begynner (15 min)

### Hvor du starter

**Du begynner i kursrepoet, og du flytter appen inn i et nytt, eget repo.** Appen
ligger i `ClaimTheSquarePostgres/` under kursrepoet, sammen med alt annet du har
gjort i kurset. I dag skal den bli **sitt eget** repo på GitHub.

Hvorfor? GitHub Actions leser `.github/workflows/` **bare i rota av repoet**. Den
leter ikke i undermapper. Derfor kan ikke appen bli liggende inne i kursrepoet
med en pipeline — den må ut til et eget repo der rotnivået er rotnivået.

> **Fell nr. 0, og den er den første du møter.** `cp -r KILDE MÅL` kopierer
> **mappen** når målet finnes fra før — og gir deg alt **én mappe dypt**:
>
> ```text
> ~/Prosjekter/claimthesquare/          ← repoet ditt, med README i
> └── ClaimTheSquarePostgres/          ← appen, her
>     └── .github/workflows/ci.yml     ← GitHub ser den aldri
> ```
>
> Du får et grønt lokalt `podman compose up`, ingenting som feiler, og en
> pipeline som aldri kjører. Den feilen er **stille** — den er den vanligste
> grunnen til at M1 bruker en time på å finne ut hvorfor Actions ikke vises.
> Har du en gang committet fra en mappe du ikke mente å, står rotmappen-feilen
> deg i hodet. Det er grunnen til at kontrollen i steg 2 finnes.
>
> Derfor: kopier **innholdet** av mappen, ikke mappen. `rsync` med en
> avsluttende `/` på kilden gjør det — uansett om målet finnes fra før eller
> ikke — og den har en `--exclude` for `bin/` og `obj/` som du ikke vil ha med.

### 0a. Få appen inn i ditt eget repo

```bash
# 1. Lag målmappen, og flytt INNHOLDET inn — ikke mappen.
#    Skillet er den avsluttende / på Kilden. (rsync følger med på Linux/macOS.)
mkdir -p ~/Prosjekter/claimthesquare
rsync -a --exclude 'bin/' --exclude 'obj/' \
      ~/kurs/ClaimTheSquarePostgres/ \
      ~/Prosjekter/claimthesquare/

cd ~/Prosjekter/claimthesquare

# 2. Sjekk at rotnivået faktisk er riktig, FØR du gjør git noe.
#    ls alene skriver bare «cannot access» og fortsetter. Med -l stopper den
#    på første mangel — og du ser hva som mangler, ikke at noe gjorde det.
ls -l .github/workflows/ci.yml compose.yml compose.prod.yml \
      ClaimTheSquare.slnx .env.example

#    Og en til: rotnivået skal IKKE inneholde appmappen.
ls -d ClaimTheSquarePostgres 2>/dev/null && echo "FEIL: appen ligger i en undermappe"

# 3. Nå git.
git init -b main
git add -A
git commit -m "ClaimTheSquare: app, compose og pipeline"
gh repo create claimthesquare --public --source . --push
# uten gh: lag repoet på github.com og
#   `git remote add origin … && git push -u origin main`
```

> **Hvis du allerede har et repo på GitHub med navnet `claimthesquare`** (fordi
> du prøvde 0a en gang før): ikke kjør `gh repo create` — den feiler med «already
> exists». Lag repoet på github.com i stedet, tomt og uten README, og kjør:
>
> ```bash
> git remote add origin git@github.com:<deg>/claimthesquare.git
> git push -u origin main
> ```

> **Hvorfor `--exclude 'bin/' 'obj/'`?** De to mappene er maskin-generert og
> inneholder absolute stier fra den maskinen de ble bygget på. De er ubrukelige
> på en annen maskin, de fyller repoet med 200+ filer, og de gjør hver commit
> urolig. `dotnet build` lager dem på nytt uansett. (I en `Dockerfile` og i
> `.dockerignore` er de samme ting — bare der teller de for bildet.)

- [ ] `ls -l` i steg 2 fant alle fem filene i **rota** — ingen av dem i en undermappe
- [ ] Rotnivået inneholder **ikke** mappen `ClaimTheSquarePostgres/`
- [ ] Repoet på GitHub viser `ClaimTheSquare/`, `compose.yml` og `.github/` i rota
- [ ] `.env` er **ikke** med (`git status` skal ikke nevne den — den står i `.gitignore`)
- [ ] `git ls-files | wc -l` er et tall i størrelsesorden 20, ikke 200+

> **Hvis du gjorde 0a med `cp` fra en tidligere kjøring:** du har sannsynligvis
> et repo der alt ligger i en undermappe. Rett det sånn — og kjør hele blokken,
> den har fire forbehold som alle er uttestet:
>
> ```bash
> cd ~/Prosjekter/claimthesquare
>
> # (1) Har du en .git INNE I undermappen? Da ser git hele mappen som én
> #     «embedded repository», og du får 1 fil i stedet for 14. Fjern den først.
> rm -rf ClaimTheSquarePostgres/.git
> git rm -r -q --cached ClaimTheSquarePostgres 2>/dev/null
>
> # (2) Flytt med `mv`, ikke `git mv` — git mv stopper på tomme underkataloger
> #     («source directory is empty»), og du har nesten alltid en.
> mv ClaimTheSquarePostgres/* . && mv ClaimTheSquarePostgres/.github .
> rm -rf ClaimTheSquarePostgres
>
> # (3) Har bin/obj blitt committet? Da må de ut av index.
> git rm -r -q --cached $(git ls-files | grep -E '(^|/)(bin|obj)/') 2>/dev/null
>
> # (4) Sjekk rotnivået FØR du committer — samme kontroll som steg 2.
> ls -l .github/workflows/ci.yml compose.yml compose.prod.yml \
>       ClaimTheSquare.slnx .env.example
> git ls-files | wc -l          # et tall i 20-årene, ikke 200+
>
> git add -A && git commit -m "flytt appen til repo-rota" && git push
> ```
>
> Etterpå: se M1 og sjekk at Actions faktisk har startet en kjøring. Har den ikke,
> er rotmappen-feilen ennå ikke helt borte.

### 0b. Se at appen virker lokalt

```bash
cp .env.example .env
chmod 644 db/init/*.sql              # se felle nr. 1 nedenfor
podman compose up -d --build
curl --fail http://localhost:8080/health          # {"status":"ok","version":"dev"}
curl --fail http://localhost:8080/text-objects    # []
```

Åpne <http://localhost:8080>, fyll en rute, og bekreft at den overlever
`podman compose down && podman compose up -d`.

> **Felle nr. 1, og den koster et kvarter:** `db/init/01_schema.sql` er
> bind-mountet inn i Postgres-containeren. Bind-mount beholder **filrettighetene
> fra maskinen din**, så har fila mode `600`, får ikke containeren lest den — og
> da kjører init-skriptet aldri. Symptomet er vondt: `/health` er grønn, men
> `/text-objects` svarer **500**. Fiks: `chmod 644 db/init/*.sql` og `down -v`
> før `up` igjen. Se etter `running /docker-entrypoint-initdb.d/01_schema.sql` i
> `podman compose logs db`.

---

## Hvem eier skjemaet? (les dette, det dukker opp igjen i kveld)

Appen lager tabellen sin selv i dev (`MIGRATE_ON_STARTUP=true`), men **ikke** i
prod. Det er med vilje, og det er en av dagens ekte arkitekturleksjoner:

| | dev | prod |
|---|---|---|
| Skjemaet lages av | appen ved oppstart | `db/init/01_schema.sql`, kjørt av postgres-imaget |
| Hvorfor | en ny database skal virke uten at noen husker rekkefølgen | appen skal ikke endre skjemaet sitt selv |
| Risiko | to instanser som migrerer samtidig | skriptet kjører bare mot et **tomt** volum |
| På ekte | greit med én instans | migreringer som **eget steg** i pipelinen: én runner, én lås, ingen kappløp |

Det siste er den ærlige innrømmelsen: skriptet vårt er *init*, ikke *migrering*.
Endrer du `01_schema.sql` etter at serveren har data, skjer ingenting før du
sletter datavolumet — og det sletter dataene. På jobben heter svaret
migrasjonsverktøy, og det kjører før containeren byttes.

---

## M1 — fire porter, grønn pipeline (09:20–10:15)

Workflowen ligger ferdig i `.github/workflows/ci.yml`. Du skal ikke skrive den
fra bunnen i dag — du skal **forstå hver port**, og se at den biter.

| Port | Spørsmålet | Kommandoen |
|---|---|---|
| `format` | Er koden formatert etter maskinens regler? | `dotnet format ClaimTheSquare.slnx --verify-no-changes` |
| `build` | Bygger den, og er testene grønne? | `dotnet restore` → `build` → `test` → `list package --vulnerable` |
| `compose` | Henger konfigurasjonen sammen? | `docker compose config` på begge filene |
| `image` | **Virker det som ble bygget?** | bygg → start → `curl /health` + `/version.json` + `/` |

Legg merke til rekkefølgen i `needs:`: `image` kan ikke starte før alle tre
portene er grønne. Og legg merke til at **røyk-testen kjører før push** — et
image som ikke svarer, kommer seg aldri inn i registret. I `labApi` pushet vi
først og testet etterpå. Her er porten strengere.

```bash
cd ~/Prosjekter/claimthesquare
git add -A && git commit -m "ci: fire porter for ClaimTheSquare" && git push
```

Følg kjøringen i Actions. Alle fire jobbene skal bli grønne.

**Verifiser — og vit hva du ser på:**

- [ ] `format` er grønn (den ville blitt rød av ett ekstra mellomrom)
- [ ] `build` kjørte fire tester
- [ ] `compose`-porten fant `image:`-referansen i `compose.prod.yml`
- [ ] `image`-jobben kjørte **etter** de tre andre, og røyk-testen skrev ut `{"status":"ok",...}`

> **Prøv porten, ikke bare les den.** Fjern en `}` i `Program.cs`, push, og se at
> `image`-jobben aldri kjører. Fiks, push, grønt. Det tar fem minutter og er
> forskjellen på å ha sett en port og å ha *sett den bite*.

---

## M2 — image til GHCR (10:15–11:00)

`docker/metadata-action` lager to tagger: `sha-<kort>` — den uforanderlige — og
`latest`, som er ei svingdør. **Du deployer alltid sha.**

1. Gå til profilen din på GitHub → **Packages** → pakken (repo-navnet, små
   bokstaver). Begge taggene skal ligge der.
2. Er pakken privat, svarer `podman pull` `denied` senere. Gjør den public:
   *Package settings* → *Change visibility*. (På jobben gjør du det motsatte og
   logger inn med PAT — se torsdagsleksjonen.)
3. **Skriv ned sha-taggen** fra den grønne kjøringen. Den er valutaen resten av
   dagen.

- [ ] Begge taggene synes i Packages
- [ ] Pakken er public
- [ ] `sha-…` står i notatboken din, bokstav for bokstav

> **Felle nr. 2, og den er den mest nesten:** det er lett å tro at
> `steps.meta.outputs.version` er sha-taggen. Den er **ikke** det. Den er den
> *første* taggen i metadata-actions prioritetsrekkefølge, og `type=raw,value=latest`
> vinner over `type=sha` — så den er `latest`, uansett hvilket bygg du lagde.
>
> Bruker du den til `APP_VERSION`, brer du `latest` inn i identitetskortet, og
> `/version.json` svarer `{"version":"latest"}` for *ethvert* bygg, alltid. Da
> kan røyk-testen heller ikke bevise at porten kjører det du nettopp bygde: gre-en
> `grep -q "$version"` passerer fordi appen sier `latest`, ikke fordi versjonen
> stemmer. Og deploy-jobbens `needs.image.outputs.version` peker på `latest` —
> du ruller ut `latest` i stillhet, og tror du deployer sha.
>
> Løsningen i `ci.yml` er å regne sha-taggen ut selv i et eget steg, og bruke
> den samme verdien i build-arg, røyk-test, push og jobbens `outputs`.
>(metadata-action beholdes for å lage tag-lista — den gjør det fortsatt riktig.)
> Merk at oppgaveteksten opprinnelig pekte på `steps.meta.outputs.version` her;
> det er nettopp feilen.

### Push fra maskinen, manuelt

Pipelinen i `ci.yml` pusher selv (M2 er lagt til *etter* en grønn `image`-port).
Men det er nyttig å kunne gjøre det samme for hånden — første gang du gjør det
lærer du tre ting samtidig: **hvem du er** i registret, **hva et image egentlig
er** (et lokalt tagget navn), og **at taggen er en peker, ikke innhold**.

Dette er også den raskeste veien til et image å pushe hvis du vil prøve M3 før
pipelinen er grønn.

**Steg 1 — logg inn mot GHCR.** Registret godtar en token med `write:packages`,
ikke ditt GitHub-passord. Sett `BRUKER` til ditt GitHub-brukernavn først — så
 slipper du å skrive det inn ni steder:

```bash
BRUKER=$(gh api user --jq .login)   # f.eks. geokkjer
podman login ghcr.io -u "$BRUKER" --password-stdin <<< "$(gh auth token)"
```

> `gh auth token` henter tokenet `gh` allerede har. Scopes må inneholde
> `write:packages` — sjekk med `gh auth status`. Token havner i
> `~/.config/containers/auth.json`; det er samme fil som Docker bruker, og
> samme som `docker login` ville skrevet.

**Steg 2 — bygg lokalt med samme tagger som CI bruker.** CI tagger med den korte
commit-SHA-en. Samme regel lokalt:

```bash
SHA=$(git rev-parse --short HEAD)          # f.eks. 30700a8
podman build -f ClaimTheSquare/Dockerfile \
  --build-arg APP_VERSION="sha-$SHA" -t "ghcr.io/$BRUKER/claimthesquare:sha-$SHA" .
podman tag "ghcr.io/$BRUKER/claimthesquare:sha-$SHA" ghcr.io/$BRUKER/claimthesquare:latest
```

> **Hvorfor `--build-arg APP_VERSION`?** Uten den blir versjonen `dev`, og da
> svarer `/health` med `dev` selv om taggen sier `sha-…`. I M3 skal alle fire
> kontrollene være enige — og de kan bare være enige hvis versjonen ble brent
> inn i imaget. `compose.prod.yml` setter riktignok `APP_VERSION=${IMAGE_TAG}`
> som miljøvariabel, som vinner over `ENV` i Dockerfile-en; men i M1/M2 er det
> bare imaget som har sannheten.
>
> *Felle:* `APP_VERSION` må deklareres på **begge** stagene i Dockerfile-en.
> `ARG` er stage-skopet, så en `ARG` i build-stagen arves ikke av
> runtime-stagen — og sluttimaget får ingen versjon uansett hva du bygger med.
> Det er akkurat feilen `ci.yml` hadde da denne leksjonen ble skrevet.

**Steg 3 — push.** Ett image, to tagger. `podman push` sender lagene over
nettverket; det andre tagget er gratis, fordi lagene allerede er lastet opp.
`$BRUKER` og `$SHA` er satt i steg 1 og 2 — kjør dem i samme terminal, eller sett
dem på nytt:

```bash
podman push ghcr.io/$BRUKER/claimthesquare:sha-$SHA
podman push ghcr.io/$BRUKER/claimthesquare:latest
```

**Steg 4 — se at det kom.** Lokalt, uten å røre noe:

```bash
podman pull ghcr.io/$BRUKER/claimthesquare:sha-$SHA
podman inspect "ghcr.io/$BRUKER/claimthesquare:sha-$SHA" --format '{{.Id}}'
```

Samme image-ID på begge er bevis på at de to taggene peker på samme ting.

Start det, og sjekk at identitetskortet svarer med sha og ikke `latest`:

```bash
podman run -d --name t -p 8080:8080 \
  -e MIGRATE_ON_STARTUP=false \
  -e ConnectionStrings__Postgres="Host=db;Database=test;Username=test;Password=test" \
  "ghcr.io/$BRUKER/claimthesquare:sha-$SHA"
sleep 5
curl --fail --silent http://127.0.0.1:8080/version.json   # {"version":"sha-…"}
podman rm -f t
```

> **Felle:** pakken arver visningen fra repoet. Er repoet **privat**, arver
> pakken privat synlighet, og `podman pull` svarer `denied` — selv om pushen
> lyktes, fordi pushen bruker *din* autorisasjon og pull gjør det ikke.
> Gjør pakken public i *Package settings* → *Change visibility*, eller bruk et
> PAT når du skal hente noe som er privat.

---

## M3 — prod-sim lokalt: kjør imaget du ikke bygde (11:00–11:45)

Dette gjorde du onsdag med `labApi`. Nå gjør du det med denne appen, og det er
siste gang i dag at noe kjører på maskinen din.

```bash
podman compose down                      # dev-stacken må slippe 8080
# .env: sett API_IMAGE til ghcr.io/<deg>/claimthesquare, og IMAGE_TAG til sha-en din
podman compose -f compose.prod.yml pull
podman compose -f compose.prod.yml up -d
```

**Er ikke dette bare onsdag en gang til?** Nesten. To ting er nye:

1. `compose.prod.yml` binder porten til `127.0.0.1`, ikke `0.0.0.0`. Det er
   forberedelsen til serveren: på en maskin på internett skal bare nginx være
   eksponert. På din egen maskin gjør det ingen praktisk forskjell — men det er
   den samme fila som skal kjøre i kveld.
2. Denne appen har **to** helseendepunkter, og de svarer på hvert sitt spørsmål:
   `/health` sier «prosessen lever», `/text-objects` sier «og den snakker med
   Postgres». En app som er grønn på den første og rød på den andre, er en app
   som har glemt skjemaet sitt.

**De fire kontrollene — de skal være enige, ord for ord:**

```bash
curl --fail  http://localhost:8080/health
# {"status":"ok","version":"sha-…"}

curl --silent http://localhost:8080/version.json
# {"version":"sha-…"}

curl --fail  http://localhost:8080/text-objects
# []  (tom første gang — tabellen er ny)

podman inspect "$(podman compose -f compose.prod.yml ps -q api)" \
  --format '{{.Config.Image}}'
# ghcr.io/<deg>/claimthesquare:sha-…
```

- [ ] Prod-simen kjører et image som **ikke** er bygget på din maskin
- [ ] Alle fire kontrollene viser samme tag
- [ ] Prøv å POST-e en rute via `curl`, og se at den havner i `/text-objects`
- [ ] Du kan si hvorfor `/health` alene ikke er nok bevis på at appen virker

---

## M4 — serveren og domenet (11:45–13:00)

Nå forlater vi laptoppen. **Denne delen gjør du én gang per maskin**, og den er
den eneste i kurset som ikke er `pull` og `up -d`.

### 4a. VPS-en

Lag en billig Ubuntu 24.04-instans hos en europeisk leverandør (GandiCloud VPS:
`V-R4`, 2 vCPU / 4 GB, holder i massevis), med **SSH-nøkkel, ikke passord**:

Opprett den i `admin.gandi.net` under **Cloud → VPS → Create server**: velg
Ubuntu 24.04 og lim inn den **offentlige** nøkkelen i `SSH key`-feltet. Gandi
krever at nøkkelen ligger i keyring-en før serveren kan lages.

```bash
ssh-keygen -t ed25519 -C "kurs-vps"     # om du ikke har en
cat ~/.ssh/id_ed25519.pub               # lim inn ved "Create server"
```

Så: **logg inn med standardbrukeren på imaget** (på Ubuntu heter den `ubuntu`),
lag brukeren `deploy`, og flytt nøkkelen over. Dette er tre små steg, og det
viktigste er det andre.

```bash
ssh ubuntu@<VPS-IP>

# Steg 1 — lag brukeren. --disabled-password låser innlogging med passord.
sudo adduser --disabled-password --gecos "" deploy
sudo passwd deploy                              # ← ikke hopp over denne

# Steg 2 — FLYTT NØKKELEN. Dette er linja som gjør at deploy-jobben virker.
sudo install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
sudo install -m 600 -o deploy -g deploy \
  /home/ubuntu/.ssh/authorized_keys \
  /home/deploy/.ssh/authorized_keys

# Steg 3 — gi deploy sudo, og la prosessene leve uten innlogging
sudo usermod -aG sudo deploy
sudo loginctl enable-linger deploy
exit
```

#### Steg 2 er hele poenget med «ett hopp»

`authorized_keys` er filen som bestemmer **hvem som får komme inn**. Gandi la
nøkkelen din der ved oppretting — under `ubuntu`. Nå kopierer du den til
`deploy`. Tre ting må være riktige, og alle tre er i den ene `install`-linja:

| Hva | Hvorfor | Hvis det er feil |
|---|---|---|
| `-o deploy -g deploy` | eierskapet må være `deploy` | SSH nekter nøkkelen: «bad ownership» |
| `-m 600` på `.ssh`-katalogen | ellers godtar SSH den ikke | «bad ownership or modes» |
| `-m 600` på `authorized_keys` | filen er en adgangsliste — ingen andre skal kunne skrive til den | «bad ownership or modes» |

> **Hvorfor kopiere framfor å bruke `ssh-copy-id`?** `ssh-copy-id` ville også
> virket, men den skriver filen som `ubuntu`, og så må du rette eierskapet med en
> ekstra `chown` og en ekstra `chmod`. `install` gjør alle tre tingene på én
> linje, og den er idempotent — du kan kjøre den igjen.
>
> **Hvorfor ikke bare gi `ubuntu` sudo og stoppe der?** Du *kan* det. Men da er
> `deploy`-steget i M5 meningsløst, og brukeren som kjører containerne på
> internett er den samme som har full sudo. `deploy` er den eneste veien inn,
> og det er den som gjør at en pipeline som logger inn med en nøkkel har minst
> mulig rettigheter.

**Verifiser med en ny terminal — ikke med den du allerede er logget inn med.**
En åpen SSH-sesjon har allerede godtatt nøkkelen, så den beviser ingenting:

```bash
ssh deploy@<VPS-IP> 'echo "nøkkelen virket: $(whoami)" && sudo -n true && echo "sudo krever passord"'
# nøkkelen virket: deploy
# sudo krever passord
```

> **Hvis du får «Permission denied (publickey)»:** kjør
> `sudo ls -la /home/deploy/.ssh/` på serveren som `ubuntu`, og se at eierskapet
> er `deploy deploy` på begge filene. Nesten alltid er det det.

> **Hvorfor går *den private* nøklen aldri til serveren?** Den skal aldri. Den
> ligger på maskinen din, og senere i dag legger du den inn som GitHub-secret
> `VPS_SSH_KEY` (M5) — fordi det er *pipelinen* som skal logge inn, ikke du.
> Serveren får bare den offentlige halvdelen, som ikke kan brukes til noe som
> helst uten den private. Det er hele poenget med nøkkelpar: den ene kan deles,
> den andre kan det ikke.

> **Hvorfor ikke `root`?** På Ubuntu-imagene hos Gandi er innlogging som `root`
> stengt, og `ubuntu` er brukeren med `sudo`.

> **Hvorfor `passwd deploy`?** `--disabled-password` låser passordet. SSH-nøkkelen
> får deg inn, men `sudo` krever *autentisering* — og med et låst passord får du
> «sudo: a password is required» på hver eneste `sudo`-kommando resten av dagen.
> Alternativet er en `NOPASSWD`-linje i `/etc/sudoers.d/`, men da har du laget en
> bruker uten passord *og* uten sperre. Passordet er det ærlige valget.

> **Hvorfor `loginctl enable-linger`?** Podman kjører rootless: containerne er
> *dine* prosesser, ikke systemets. Logger du ut, river systemd ned
> brukerøkta — og da dør containerne som pipelinen startet. `enable-linger` lar
> brukerøkta leve uten innlogging. Det er den ene linja som gjør at en
> automatisk deploy faktisk blir stående.

Så inn som `deploy` og installer det som trengs:

```bash
ssh deploy@<VPS-IP>
sudo apt update
sudo apt install -y podman podman-compose ufw
podman --version && podman compose version
```

> **Sjekk versjonen av compose-provideren.** Kurset krever
> `podman-compose >= 1.3.0` (eller Docker Compose v2), fordi eldre versjoner
> **ignorerer** `depends_on: condition: service_healthy` i stillhet. Da starter
> `api` før Postgres er klar, og du får «connection refused» på første kall —
> akkurat feilen torsdag gjorde pensum av. Er versjonen gammel:
> `sudo apt install -y docker-compose-v2`, eller `pipx install podman-compose`.

Swap, så en liten maskin ikke dør av en `dotnet`-prosess som vil ha minne:

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab    # overlever omstart
```

**Brannmuren: bare to porter.**

```bash
sudo ufw default deny incoming
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

Legg merke til hva som **ikke** står der: ingen 8080, ingen 5432. Appen lytter på
`127.0.0.1` (det er derfor `compose.prod.yml` binder til loopback), og Postgres
skal aldri være på internett noe sted.

- [ ] `ssh deploy@<VPS-IP>` virker uten passord
- [ ] `sudo` virker (den spør om passordet du satte)
- [ ] `loginctl show-user deploy | grep Linger` sier `Linger=yes`
- [ ] `ufw status` viser `22, 80, 443` — og ingenting annet

### 4b. Domenet

Du trenger et domene, eller et navn under et domene kurset kontrollerer. Legg
sonen inn hos Cloudflare og bruk **de to navneserverne Cloudflare faktisk viser
deg** på DNS-siden (`xxx.ns.cloudflare.com` — de er tildelt per konto og kan
ikke velges). Det tar minutter til timer å propagere, så gjør det først.

Så en `A`-record for `<dittnavn>` mot VPS-IP-en, og **skyen skal være grå**
(DNS only). Hvorfor grå: Let's Encrypts HTTP-01-utfordring legges ut *på
maskinen din* og hentes utenfra. Med oransje sky går den via Cloudflare, og da
er det flere ting som kan velte. Grått er forutsigbart.

Verifiser med DNS, ikke med `curl` — nginx er ikke installert ennå, så en
`curl` vil bare time ut og lure deg til å feilsøke brannmuren:

```bash
dig +short <dittnavn>.kursdomenet.no
# skal gi VPS-IP-en din
```

- [ ] `dig` gir VPS-IP-en
- [ ] Skyen er grå i Cloudflare

### 4c. nginx på serveren

```bash
ssh deploy@<VPS-IP>
sudo apt install -y nginx
sudo tee /etc/nginx/sites-available/claimthesquare >/dev/null <<'EOF'
server {
    listen 80;
    listen [::]:80;
    server_name <dittnavn>.kursdomenet.no;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF
sudo ln -sf /etc/nginx/sites-available/claimthesquare /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
```

**Den ene skråstreken i hele leksjonen.** `proxy_pass http://127.0.0.1:8080;`
**uten** skråstrek på slutten sender forespørselen uendret videre: `/text-objects`
kommer frem som `/text-objects`. Skriver du `http://127.0.0.1:8080/;`, erstatter
nginx det som matchet — og da blir `/text-objects` til `/`, og du får 404 fra
appen mens det ser ut som om det er appen som er ødelagt. Forskjellen er ett
tegn, og nginx har to helt ulike regler for `proxy_pass`.

#### Hvorfor nginx *ikke* er en tjeneste i compose

Intensjonen er god, og den er faktisk den compose ble laget for: én fil, én
`up -d`, ingen rørdraging. Her kjøres jo allerede `api` og `db` i compose, og
utrullingen er `pull && up -d`. Så hvorfor er nginx holdt utenfor?

**1. Port 80 er under 1024, og rootless podman får ikke binde den.** Prøv det:

```bash
podman run --rm -p 80:80 docker.io/library/nginx:alpine
# Error: pasta failed with exit code 1:
# Listen failed for HOST TCP port */80: Permission denied
```

Og merk at `--cap-add=NET_BIND_SERVICE` **ikke** hjelper:

```bash
podman run --rm --cap-add=NET_BIND_SERVICE -p 80:80 docker.io/library/nginx:alpine
# Listen failed for HOST TCP port */80: Permission denied
```

Rootless-containeren får ikke slike capabilities. Så det er realistisk to veier:
kjøre containeren som `root` (da får `api` og `db` også root, og hele
`loginctl enable-linger`-argumentet forsvinner), eller sette

```bash
sudo sysctl net.ipv4.ip_unprivileged_port_start=0
```

Den siste er den vanlige løsningen — og den er en **maskinomfattende** endring.
Den åpner port 80 for *alle* prosesser som kjører som din bruker, også utenfor
containere, og den overlever omstart før du legger den i `/etc/sysctl.d/`. Det er
en reell sikkerhetskostnad for å spare én fil.

**2. `certbot` skriver inn i nginx sin egen katalog.** `certbot --nginx` endrer
konfigurasjonen der den ligger. Ligger den i en container, ligger endringen i
containerens lag — og neste `up -d` erstatter containeren og tar den med seg.
Sertifikatet fornyes to ganger i døgnet av en systemd-timer; med nginx på verten
skriver den til disk og nginx plukker det opp. Med nginx i en container må
cert-fila mountes inn, og noe må kjøre `nginx -s reload` etter hver fornyelse.
Det er to ekstra bevegelige deler i den mest tidskritiske jobben på maskinen.

**3. Feilsøkingen blir dårligere, og det er det som rammer deg først.** Med
nginx på verten har du `sudo nginx -t` (gyldig konfig?) og
`sudo journalctl -u nginx -n 50` (hva skjedde?) — verktøy som finnes på maskinen
uansett hva som kjører i den. Med nginx i en container må du først finne ut
hvilken container det gjelder. En `502` betyr i begge tilfeller «proxy_pass nådde
ikke appen», men i det andre tilfellet er det tre ting til som kan ha gått galt:
nginx startet ikke, nginx startet for tidlig, eller appen er nede.

> **Når du ville valgt nginx i compose:** når noe annet eier ingressen — en
> Traefik, en nginx-Ingress i Kubernetes, en Cloudflare-tunnel. Da er nginx en
> av mange containere bak en felles inngang, og konfigurasjonen hører til
> *appen*, ikke til maskinen. Så lenge nginx er den eneste veien inn på en maskin
> du rører selv, er den en del av maskinen.

> **Ett argument til for verten, som er litt flinkt:** når oppgaven ber deg slette
> VPS-en til slutt, forsvinner alt du har laget i compose med én kommando.
> nginx-konfigurasjonen og `certbot` ligger i `/etc` og `~/.config/letsencrypt` —
> og en maskin du tør slette er en maskin du kan bygge på nytt uten å tenke på
> hva som hang igjen.

### 4d. Filene og hemmelighetene på serveren

Filen `compose.prod.yml` trenger en `.env` ved siden av seg på serveren. Den
lager du **én gang, for hånd**, og pipelinen rører den aldri:

```bash
sudo install -d -m 755 -o deploy -g deploy /opt/stack
sudo -u deploy tee /opt/stack/.env >/dev/null <<'EOF'
POSTGRES_DB=claimthesquare
POSTGRES_USER=claimuser
POSTGRES_PASSWORD=<finn på noe sterkt>
API_IMAGE=ghcr.io/<deg>/claimthesquare
IMAGE_TAG=sha-0000000
API_PORT=8080
EOF
sudo chmod 600 /opt/stack/.env
```

> **Hvor `.env` skal ligge: ved siden av `compose.prod.yml`, i `/opt/stack`.**
> Ikke et annet sted, og ikke bare «i en katalog ved siden av».
>
> Grunnen er at `podman compose -f <fil>` slår opp `.env` **relativt til
> compose-fila**, ikke relativt til katalogen du står i. Verifisert:
>
> | Du kjører fra | `podman compose -f /opt/stack/compose.prod.yml config` |
> |---|---|
> | `/opt/stack` | finner `.env` ✅ |
> | `/opt` | finner `.env` (den ligger i katalogen over) ✅ |
> | `/` eller hjemmemappen din | finner **ingenting** ❌ |
>
> I siste tilfelle blir `${API_IMAGE}` tom, og du får `image: :latest` — en
> syntaktisk gyldig, men verdiløs referanse. Det er nøyaktig samme feil
> `compose`-porten i M1 sjekker for med `grep -q "image: …"`.
>
> Konklusjonen er enkel: **kjør alltid `cd /opt/stack` før `podman compose`.**
> Da er det ingen ting å huske på. Deploy-jobben gjør allerede dette
> (`cd /opt/stack` i `bash -s`-blokken), så M5 og denne linja blir samme
> kommando.
>
> **Men `cd` er ikke det som gjør at deploy-jobben virker.** Den `export`er
> `API_IMAGE` og `IMAGE_TAG` selv — og eksporterte varianter **vinner over**
> `.env`. Verifisert: med `API_IMAGE=… podman compose … config` blir bildet
> `ghcr.io/FRA-EXPORT/…`, ikke det som står i `.env`.
>
> Det som *må* komme fra `.env`, er `POSTGRES_PASSWORD` — jobben rører den ikke.
> Så det er derfor fila må være lesbar **for `deploy`**: en fil `deploy` ikke kan
> åpne, gir en container som starter med tomt passord og en Postgres som nekter
> alle koblinger. Det er verdt å vite hvilken variabel som bærer hvilken
> bekymring:

| Variabel | Kommer fra | Hvis den mangler |
|---|---|---|
| `API_IMAGE`, `IMAGE_TAG` | `export` i deploy-jobben | `image: :latest` |
| `POSTGRES_*` (inkl. passord) | `.env` på serveren | Postgres nekter alle koblinger |

> **Hvorfor `sudo -u deploy tee`, og ikke bare `cat > .env`?** Du er logget inn
> som `ubuntu`. `cat > .env` lager fila som **`ubuntu`**, og så kjører
> `chmod 600` — og da kan **`deploy` ikke lese den**. `deploy` er brukeren som
> faktisk kjører containerne, så du får en feilmelding som ser ut som om
> Postgres er ødelagt.
>
> Det er verdt å se en gang, fordi feilmeldingen ikke peker på årsaken. Fra
> `ssh deploy@…`:
>
> ```bash
> head -1 /opt/stack/.env
> # cat: /opt/stack/.env: Permission denied
> ```
>
> Riktig eierskap er derfor hele poenget med `sudo -u deploy tee` — filen
> havner som `deploy` fra første byte. Sjekk den når du er ferdig:
>
> ```bash
> ls -l /opt/stack/.env     # -rw------- 1 deploy deploy …
> ```

> **Hvorfor `IMAGE_TAG=sha-0000000` her og ikke en ekte tag?** Fordi pipelinen
> setter `IMAGE_TAG` selv når den deployer. Det som bor i denne fila, er
> *hemmelighetene* — de skal aldri gjennom Git eller gjennom en pipeline.

Så kan du gjøre den første utrullingen for hånd, akkurat som hjemme:

```bash
# fra din egen maskin:
scp compose.prod.yml deploy@<VPS-IP>:/opt/stack/
# `scp` med en sti som ikke finnes, ville falt på den som oppretter den —
# så lag katalogen først, med eierskap til deploy:
ssh deploy@<VPS-IP> 'mkdir -p /opt/stack/db/init'
scp db/init/01_schema.sql deploy@<VPS-IP>:/opt/stack/db/init/

# på serveren, som deploy:
cd /opt/stack
podman compose -f compose.prod.yml pull
podman compose -f compose.prod.yml up -d
curl --fail http://127.0.0.1:8080/health
```

> **Hvorfor `ssh deploy@… 'mkdir -p …'` og ikke `mkdir -p db/init` lokalt på
> serveren?** Du er logget inn som `ubuntu`. `mkdir` lager da katalogen som
> `ubuntu`, og når `deploy` senere skal lese `db/init/01_schema.sql` får den
> feilmeldingen fra Postgres om at init-skriptet ikke kjører — altså felle nr. 1
> en gang til, denne gangen på serveren. Ved å kjøre `mkdir` gjennom `ssh
> deploy@` blir den eiert av `deploy` som en sideeffekt.
>
> Det er nøyaktig samme trikk deploy-jobben bruker — den har
> `ssh "<user>@<host>" "mkdir -p /opt/stack/db/init"` rett før `scp`-ene. Se M5.
>
> Og **alltid `cd /opt/stack` før `podman compose`** — jmf. `.env`-tabellen
> over. Uten `cd` kan du få `image: :latest` i stedet for imaget du mente, og
> du mister i alle fall `POSTGRES_PASSWORD`.

### 4e. TLS

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx --redirect -d <dittnavn>.kursdomenet.no
```

Spørsmålene: e-postadressen din, `Y` til å godta betingelsene, og om du vil
**redirecte** HTTP → HTTPS (ja — det er derfor `--redirect` står der; uten den
svarer port 80 fortsatt i klartekst).

```bash
curl -I https://<dittnavn>.kursdomenet.no          # 200, nå med TLS
sudo certbot renew --dry-run                       # fornyelsen er automatisert
systemctl list-timers | grep certbot
```

> **Om fornyelsen.** Timeren kjører to ganger i døgnet og fornyer når det er
> under 30 dager igjen til utløp. Et utløpt sertifikat er den vanligste måten en
> «ferdig» server blir liggende død i helgen på — derfor `--dry-run` i en rolig
> stund, ikke den dagen det brenner.

Åpne `https://<dittnavn>.kursdomenet.no`. Du skal se rutenettet. **Fyll en rute
og last siden på nytt — den skal fortsatt være der.** Nå kjører appen din på
internett, bak TLS, og databasen din er ikke eksponert.

- [ ] `https://…` viser rutenettet, med hengelås
- [ ] `http://…` redirecter til https
- [ ] En rute du fyller, overlever en refresh
- [ ] `curl -I http://<VPS-IP>:8080` fra din egen maskin **timer ut** — porten er
      ikke åpen; det er bare nginx som svarer

---

## M5 — deploy-jobben (13:00–14:00)

Nå skal den som gjorde jobben for hånd, gå bort. Men først: to ting i GitHub.

**Secrets** (Settings → *Secrets and variables* → *Actions* → *Secrets*):

| Secret | Innhold |
|---|---|
| `VPS_HOST` | IP-adressen eller vertsnavnet — **ikke** `deploy@1.2.3.4` |
| `VPS_USER` | `deploy` |
| `VPS_SSH_KEY` | innholdet i `~/.ssh/id_ed25519` (den **private** nøkkelen) |

**Variables** (samme sted → *Variables*):

| Variable | Innhold |
|---|---|
| `APP_HOSTNAME` | `<dittnavn>.kursdomenet.no` |

> **Hvorfor vertsnavnet er en variabel og ikke en secret.** En secret er skjult i
> loggene; en variabel er lesbar. Vertsnavnet er ikke en hemmelighet — det står i
> sertifikatet og i DNS. Hemmeligheter skal være hemmelige, konfigurasjon skal
> være lesbar, og begge skal være versjonerte et sted du kan vise frem.

> **Felle nr. 3:** `ssh-keyscan` tar et **vertsnavn**, ikke `user@host`. Limer du
> inn `deploy@1.2.3.4` i `VPS_HOST`, feiler første steg i deploy-jobben med
> `getaddrinfo … Name or service not known` — og feilen ser ut som et
> nettverksproblem. Derfor er brukeren skilt ut i `VPS_USER`.

Deploy-jobben ligger allerede nederst i `.github/workflows/ci.yml`. Les den før
du pusher, og stopp ved disse fire tingene:

1. **`environment: prod`** — første kjøring kan kreve godkjenning i
   repo-innstillingene. Det er en funksjon, ikke en feil: det er der
   «Continuous Delivery» og «Continuous Deployment» skiller lag.
2. **`outputs: version`** på `image`-jobben. Jobber arver ikke `steps`-outputs fra
   hverandre — bare det jobben eksplisitt eksporterer. Uten den linja er
   `needs.image.outputs.version` tom, og da deployer du `latest` i stillhet.
   Og merk: linja må peke på **sha-taggen**, ikke på `steps.meta.outputs.version`
   — se felle nr. 2 i M2. Ellers deployer du `latest` i stillhet med en linje som
   *ser* riktig ut, og jobben blir grønn mens den gjør feil ting.
3. **`inputs.tag || needs.image.outputs.version`** — samme jobb gjør både vanlig
   deploy (push) og rollback (manuell kjøring med en tagg).
4. **Rekkefølgen i jobben:** `pull` → `up -d` → helseport. Gaten kjører *etter* at
   containeren er byttet. Den **oppdager** en dårlig deploy, den forhindrer den
   ikke. Det er derfor det neste steget finnes.

Push, og følg med:

```bash
git add -A && git commit -m "ci: deploy til VPS over SSH" && git push
```

- [ ] `deploy`-jobben er grønn
- [ ] Helseporten skrev ut `{"status":"ok",...}` og fant den nye taggen i `/version.json`
- [ ] Åpne `https://<dittnavn>.kursdomenet.no/version.json` i nettleseren — der står taggen fra den grønne kjøringen

### Gjør en synlig endring, og se at flyten bærer den

Endre noe du kan se: en farge i `wwwroot/index.html`, eller en ekstra seed-rad.
Push. Følg pipelinen. Se at appen på internett har endret seg — **uten at du
rørte serveren**.

Det er hele kurset i én bevegelse: koden går inn i venstre ende lokalt, og kommer
ut som en kjørende tjeneste på den andre siden av internett, med fire porter den
måtte passere på veien.

---

## M6 — rollback, og feil du lager med vilje (14:00–15:00)

### 6a. Rollback på under to minutter

Sjefen ringer: den nye versjonen oppfører seg rart. Du har to veier tilbake, og
de er ikke det samme:

| | `git revert` | Rollback |
|---|---|---|
| Hva ruller tilbake | **koden** — ved å gå fremover | **deployen** |
| Resultat | ny commit, nytt image, ny grønn kjøring | gammelt image, ingen ombygg |
| Tid | hele pipelinen | to minutter |

Koden er den trygge veien når du har tid. Rollback er den som virker klokka fem
på en tirsdag. **Gjør rollback nå, med stoppeklokke:**

1. Finn forrige sha-tagg (Packages, eller `git log --oneline -5`).
2. Actions → **ci** → *Run workflow* → lim inn den gamle taggen → Run.
3. Stopp klokka når `https://…/version.json` viser den gamle taggen igjen.

Skriv tiden i runbooken. Er du over to minutter: hva var flaskehalsen?

> Å rulle tilbake her er bare mulig fordi imaget er **uforanderlig** og databasen
> er et **volum**. Det gamle bygget ligger fortsatt i registret, og dataene ble
> aldri rørt. En rollback som krever et ombygg er ikke en rollback — det er en
> fix.

### 6b. Feilscenarioene

Lag feilen, **les hele feilmeldingen fra toppen**, skriv den ordrett i
runbooken, fiks. Fire varianter, fra billig til lærerik:

**A. Feil tagg i `.env` på serveren.** `ssh` inn, sett `IMAGE_TAG=sha-finnes-ikke`,
kjør `pull`. Les feilmeldingen: hvilket ledd svarte — runtime eller registry? Så
`ps` og `curl --fail http://127.0.0.1:8080/health`: **hva kjører fortsatt, og
hvorfor ble ingenting byttet?** (Feilen skjer *før* noen container røres.)

**B. Manglende skjema.** `podman compose -f compose.prod.yml down -v` på
serveren, og bytt `db/init/01_schema.sql` mot en tom fil. `up -d`. Nå er
`/health` grønn og `/text-objects` rød — nøyaktig diagnosen fra M0, men på en
server. Bytt fila tilbake, `down -v`, `up -d`.

**C. nginx peker feil.** Endre `proxy_pass` til `http://127.0.0.1:8080/;` (med
skråstrek). `sudo nginx -t && sudo systemctl reload nginx`. Hva sier
`https://…/text-objects` nå? Hva sier `/health`? Hvorfor rammes den ene og ikke
den andre? Rett den.

**D. Gaten som ikke kan stoppe alt.** Tenk etter før du prøver: hva skjer hvis
`up -d` lykkes men appen krasjer ved oppstart? Hvilken jobb blir rød, og **hva
kjører på serveren mens det står på?** Svaret er ubehagelig og viktig: gaten
oppdager at du nettopp rullet ut noe ødelagt — den redder deg ikke. Det er derfor
profesjonelle oppsett har `if: failure()`-rollback som eget steg, eller
blågrønn utrulling med to miljøer.

- [ ] Rollback gjennomført, tid tatt med stoppeklokke, skrevet i runbooken
- [ ] Minst tre av feilscenarioene gjennomført med **ordrett** feiltekst notert
- [ ] Du kan forklare hvorfor en grønn helseport ikke er det samme som en trygg utrulling

---

## Runbook og peer-test (fra 15:00)

Skriv inn i `README.md` i repoet ditt, slik at noen andre kan ta over:

````markdown
## Deploy og rollback

Appen: ett image (API + frontend), én port, én origin. Postgres som egen tjeneste.

Forutsetninger: GHCR-pakken er public. På serveren: `deploy` med sudo, linger på,
ufw med 22/80/443, nginx som proxy til 127.0.0.1:8080, TLS fra Let's Encrypt.
`.env` i /opt/stack eies av deg — pipelinen rører den ikke.

Deploy:   git push main → fire grønne porter → deploy-jobben over SSH
Rollback: Actions → ci → Run workflow → skriv inn forrige sha-tagg
          (timet til: __ s)
Kontroller etterpå:
  curl --fail  https://<vert>/health          # prosessen lever
  curl --silent https://<vert>/version.json   # hvilket bygg
  curl --fail  https://<vert>/text-objects    # hele kjeden + Postgres
  podman inspect "$(podman compose -f compose.prod.yml ps -q api)" \
    --format '{{.Config.Image}}'              # fasit: hva serveren startet

Feilsøk:
  pull blokkert (manifest unknown) → taggen finnes ikke i registret
  /health grønn, /text-objects 500 → skjemaet mangler
    (sjekk: podman compose logs db | grep initdb)
  alt grønt lokalt, 502 fra nginx   → proxy_pass peker feil, eller API-et er nede
  `sudo nginx -t` og `sudo journalctl -u nginx -n 50`
  Postgres nekter alle koblinger     → POSTGRES_PASSWORD kom ikke fram
    (sjekk: `head -1 /opt/stack/.env` som deploy — "Permission denied" betyr
     at .env ble laget av en annen bruker, se 4d)
  image: :latest i `compose config` → .env ble ikke lest, kjør fra /opt/stack

Skjema: init-skriptet kjører BARE mot et tomt volum. Skjemaendring i prod =
down -v (sletter data) eller en egen migreringsjobb i pipelinen.
````

**Peer-testen — dagens ærligste måling:** be en gruppevenn gjøre en rollback på
*sin* maskin, ut fra din runbook, mens du **holder munn**. Må de spørre deg om
noe som ikke står skrevet, er avsnittet ikke ferdig.

Og helt til slutt, når alt virker: **slett VPS-en.** Med timebasert fakturering
har den kostet deg det den kostet.

- [ ] Deploy, rollback med målt tid og feilsøk står i README
- [ ] Gruppevenn gjennomførte en rollback uten ett spørsmål til deg
- [ ] VPS-en er slettet (eller du vet nøyaktig hva den koster i måneden)

---

## Den muntlige sjekken

Jeg kommer rundt. Kort svar og en rask demonstrasjon er nok — dette er ikke en
presentasjon og ikke en innlevering.

1. **Sentralspørsmålet: hva er forskjellen på det du gjorde i uke 1 og i uke 2?**
   *Ingenting av det som kjører endret seg — bare hvem som bygde det, og hvor det
   kjører.* Samme Dockerfile, samme image, samme `pull` og `up -d`.
2. Hvorfor er appen **én** container? Hva hadde måttet endre seg om frontenden
   lå i sitt eget image på en annen port? *(kan forklare — dette er CORS og
   reverse proxy)*
3. Vis meg: bevis at serveren kjører nøyaktig det imaget den siste grønne
   kjøringen pushet. *(kan vise)*
4. Hva er forskjellen på `/health` og `/text-objects` i denne appen, og hvorfor
   trenger du begge? *(kan forklare)*
5. Gjør en rollback nå, mens jeg tar tid. Hvor hentet du taggen, og hvorfor
   slipper du å bygge noe? *(kan gjøre)*
6. Hvem eier skjemaet i prod — appen eller databasen? Hva går galt hvis appen
   eier det og du kjører to instanser? *(kan forklare)*
7. **Hvilket grep fra uke 2 ville reddet Knight Capital?** Svarform: *automatisert,
   verifiserbar utrulling* — hver versjon inn gjennom en grønn pipeline med
   porter, og rollback som en vanlig, timet kommando. De 45 minuttene som kostet
   440 millioner dollar skjedde fordi utrullinga verken var automatisert eller
   reverserbar.

---

## Hvis du står helt fast

1. **Les feilmeldingen. Hele. Fra toppen.** Pipelinen, `pull`-feilen, `nginx -t`
   og `curl` sier nesten alltid hvor leddet svikter.
2. **Sjekk at du ikke har hoppet over et ledd:**

   ```bash
   podman compose -f compose.prod.yml ps          # hvem er oppe på serveren?
   podman compose -f compose.prod.yml logs --tail 30 api
   podman compose -f compose.prod.yml config | grep -A2 'image:'
   sudo nginx -t && sudo journalctl -u nginx -n 50
   sudo ufw status
   ```

3. **Er rekkefølgen riktig?** Finnes taggen i Packages? Pulla serveren imaget?
   Startet API-et? Er skjemaet der? Svarer `/health` *på serveren*
   (`curl 127.0.0.1:8080/health`)? Svarer nginx (`curl -I http://<vert>`)?
4. **Bytt plass med partneren din:** forklar høyt hva du prøver å gjøre. Halve
   feilen er at du ser på feil maskin — din egen i stedet for serverens.
5. **Rekk opp hånda.** Det er derfor jeg er her i dag.

---

## Hele kursets bukt

```text
UKE 1:  din kode ─► podman build ─► ditt image ─► compose opp ─► appen i nettleseren
UKE 2:  din kode ─► git push ─► fire porter ─► GHCR ─► SSH ─► pull opp ─► https://…
```

Den andre linja er ikke en annen verden — den er **samme verden med en annen
bygger, på en annen maskin**. Imaget som kjører på serveren nå, kunne ha kjørt i
uke 1, og omvendt. Derfor er dev og prod like, derfor er rollback billig, og
derfor kan du bevise hvilken versjon som kjører: **imaget er kontrakten.**

Det du har lagt til i dag, er de tre tingene som skiller en øvelse fra drift:
ett hopp (SSH), ett skap (hemmeligheter utenfor Git) og én inngang (nginx på
80/443). Resten — porter, tagger, helseport, rollback — kunne du fra før.

- [ ] Alt fra milepælene er committet — commits og kjøringene er dokumentasjonen din
- [ ] Runbooken har deploy, rollback med målt tid og feilsøk — og en gruppevenn brukte den
- [ ] `.env` er fortsatt utenfor Git, og `.env.example` forklarer variablene
- [ ] Én setning fra to uker: **det er ikke viktig hvem som bygger imaget — det
      viktige er at det er samme image, og at du kan bevise versjonen som kjører.**
