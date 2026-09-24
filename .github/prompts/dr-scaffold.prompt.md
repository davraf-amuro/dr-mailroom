---
agent: 'agent'
description: 'Scaffolding guidato: workspace VS Code, solution .NET 10, progetti e pacchetti dr-*'
tools: ['search/codebase']
---

# Prompt: Scaffolding dr-* (AI Agent)

Guida l'utente nella creazione di workspace, solution, progetti e installazione dei pacchetti `dr-*`. Raccogli tutte le risposte **prima** di scrivere, mostra l'albero completo, chiedi **una sola** conferma.

Le domande si pongono come **liste numerate con default esplicito**: l'utente risponde con i numeri o accetta i default. Non porre una domanda per messaggio: raggruppa per sezione.

Questo prompt è l'equivalente portabile delle skill `dr-scaffold*`. Il flusso, l'ordine dei comandi e i gate sono identici.

---

## Scegli la sezione

| Stato della cartella corrente | Vai a |
|---|---|
| Vuota, o con soli file di appoggio (README, .gitignore, LICENSE) o file del core già installati (`.github/`, `.claude/`, `.ai/`, `CLAUDE.md`, config radice) | **Sezione A** — solution da zero |
| Contiene già `*.slnx` o `*.sln` | **Sezione B** — aggiungi un progetto |
| Contiene `*.csproj` sciolti ma **nessuna solution** | **Sezione A**, dopo aver detto quali progetti hai trovato e che verranno agganciati alla solution nuova |
| Contiene solo `package.json` (repo frontend) e la richiesta è .NET | Chiedi se la parte .NET va in un repo separato, poi **Sezione A** in modalità multi-repo |
| Contiene progetti ma manca il manifest `.ai/dr-guidelines-packages.json` (o è incompleto) | **Sezione C** — installa i pacchetti |
| Contiene tutto | Elenca cosa c'è e chiedi cosa manca. Non procedere per inerzia |
| **Qualsiasi altro stato senza solution** — cartella non vuota, nessun `*.slnx`/`*.sln`, nessun `*.csproj`, nessun `package.json` (es. repo di docs o tooling) | **Sezione A**, che in A.3 elenca il contenuto inatteso e chiede prima di scrivere |

L'ultima riga è la rete: nessuno stato resta scoperto, quindi non c'è niente da improvvisare.

**Prerequisito mancante non è un vicolo cieco.** Se l'utente chiede una tipologia specifica ("aggiungi un worker", "crea una minimal api") e manca il contenitore che la regge — la solution, o il repository — non rispondere che serve un'altra procedura: **proponi di creare anche il contenitore**. Se accetta, prosegui nella sezione indicata portandoti dietro la tipologia già scelta, così l'utente non la ripete. Se rifiuta, allora fermati.

Ambiguità vera (due file solution nella stessa cartella, una solution qui e una in una sottocartella): mostra cosa hai trovato e chiedi. Non scegliere al posto dell'utente.

---

## Gate 0-pre — Dominio della richiesta

Prima dei prerequisiti: **quale dominio** sta chiedendo l'utente. Da questo dipende cosa va verificato.

1. Leggi il catalogo (percorsi nel Gate 0-bis qui sotto).
2. Confronta la richiesta con le `phrases` di ogni voce di `intentMap` e individua il `domain`.
3. Dal dominio ricava il `kind`, e da `kinds` l'elenco dei `prerequisites`.

Il catalogo è la fonte: non tenere in questo prompt un elenco parallelo di domini o frasi.

**Nessuna corrispondenza** → applica il blocco `fallback` del catalogo: dichiara che nessun pacchetto `dr-*` copre quel dominio, proponi di proseguire con il solo core generico oppure di aprire una issue di richiesta nuovo pacchetto sul repository indicato da `fallback.issueRepo`. Nessuna issue senza conferma esplicita dell'utente sul testo.

Richiesta generica senza indizi ("crea un progetto") → elenca le `label` dei domini disponibili e chiedi quale, invece di assumerne uno.

## Gate 0 — Prerequisiti del dominio (obbligatorio in tutte le sezioni)

Verifica **solo** i prerequisiti del `kind` risolto, più quelli comuni. Comandi di sola lettura:

```powershell
git --version        # sempre
pwsh --version       # sempre
gh auth status       # sempre
dotnet --list-sdks   # solo kind dotnet
node --version       # solo kind node, o frontend richiesto
npm --version        # solo kind node, o frontend richiesto
pio --version        # solo kind embedded
```

| Requisito | Quando | Esito atteso | Se manca |
|---|---|---|---|
| git | sempre | qualsiasi versione | STOP |
| PowerShell 7+ | sempre | `7.*` | STOP |
| gh autenticato | sempre | `Logged in to github.com` | STOP — i repo `dr-*` sono Private |
| .NET SDK 10.x | kind `dotnet` | almeno una riga `10.*` | STOP |
| node + npm | kind `node`, o frontend richiesto | qualsiasi versione | STOP se il progetto è solo frontend, altrimenti blocca solo quel pezzo |
| PlatformIO Core | kind `embedded` | `PlatformIO Core, version ...` | STOP |

Su un progetto .NET l'install di `dr-dotnet-backend` copia un `global.json` che pinna l'SDK (`10.0.100`, `rollForward: latestMinor`). Con un SDK che non lo soddisfa, **ogni** comando `dotnet` in quella cartella fallisce dopo l'install: per questo il gate va prima di scrivere.

Un dominio di soli contenuti (`kind: content`) non richiede toolchain: verifica git e fermati lì.

## Gate 0-bis — Catalogo

Leggi il catalogo delle tipologie e dei pacchetti dal primo percorso disponibile:

1. `.ai/dr-scaffolding-catalog.json` nel progetto corrente
2. `scaffolding-catalog.json` nella root del clone di `dr-guidelines`
3. Nessuno dei due → chiedi all'utente il percorso del repo `dr-guidelines` e fermati

Tipologie e pacchetti si prendono **da quel file**. Non elencarli a memoria: il catalogo cresce, questo prompt no.

Stabilisci anche **come raggiungere gli installer**. Due vie, entrambe valide:

1. `gh api` — non serve nessun clone locale e funziona anche a repo Private. Richiede `gh auth status` autenticato (già verificato nel Gate 0).
2. Percorso locale dei repo `dr-*` — la cartella che contiene `dr-guidelines`, `dr-minimalapi`, … se il workspace li ha già.

`irm ... | iex` funziona solo a repo Public: oggi risponde `404`.

---

## Sezione A — Solution da zero

### A.1 Domande (tutte insieme)

Riporta prima **in quale cartella ti trovi**: tutto nascerà lì dentro.

1. **Workspace multi-repo?** (serve se frontend e backend andranno in repository git separati)
   - `1` Sì — indica il nome (default: nome della cartella corrente)
   - `2` No *(default)*
2. **Solution .NET?**
   - `1` Sì — indica il nome (default: nome della cartella corrente in lowercase)
   - `2` No — solo progetti non-.NET
3. **Formato solution:** `1` slnx *(default, formato dell'SDK 10)* · `2` sln (compatibilità Visual Studio più vecchio)
4. **Tipologie di progetto** (selezione multipla, numeri separati da virgola). Costruisci la lista da `projectTypes[]` del catalogo e per ognuna proponi il nome `<solution><defaultNameSuffix>`, modificabile.

**Validazione nomi (obbligatoria):** ogni nome deve corrispondere a `^[A-Za-z][A-Za-z0-9._-]{0,63}$` (campo `defaults.namePattern` del catalogo). Nome non conforme → rifiuta e richiedi: finisce interpolato in percorsi e in `dotnet new`.

Se un nome non è ricavabile dal contesto né dalle risposte, **fermati e chiedi**. Mai inventare `MyApi`, `WebApi`, `Progetto1`.

### A.2 Pacchetti per repository

L'unità di installazione è il **repository**, non il progetto: i file del core (`.github/`, `.claude/`, `CLAUDE.md`, config radice) vengono letti solo dalla root del repo, dentro `src/<progetto>/` sono invisibili.

Per ogni repository che nascerà, proponi come pre-selezionati il core `dr-guidelines` più i `suggestedPackages[]` delle tipologie scelte per quel repo; mostra `optionalPackages[]` come non selezionati. **Non elencare le dipendenze**: le risolve l'installer dal proprio registry.

### A.3 Dry-run e conferma unica

Mostra l'albero completo, i comandi in ordine e i pacchetti per repo. Poi **una sola** domanda di conferma. Dopo la conferma non chiedere più nulla, salvo divergenze reali.

Due livelli, con regole diverse. **Root del repository**: i file del core già installati (`.git/`, `.github/`, `.claude/`, `.ai/`, `CLAUDE.md`, `README.md`, `LICENSE`, `.gitignore`, `.gitattributes`, `.editorconfig`, `.vscode/`, un `*.code-workspace`, config radice dei pacchetti `dr-*`) sono attesi — lasciali intatti e prosegui. **Cartelle di progetto** (`src/<nome>`, `test/<nome>`, frontend): devono non esistere; se esistono, anche vuote → STOP, non sovrascrivere, non fondere. I contenitori `src/` e `test/` possono esistere. Altro contenuto nella root (sorgenti, cartelle sconosciute, un `*.slnx`/`*.sln` già presente) → elenca cosa hai trovato e chiedi: una solution nella root significa Sezione B, non Sezione A.

### A.4 Esecuzione, in questo ordine

L'ordine mette prima ciò che è recuperabile con git, così il rollback resta `git clean -fd` e non `Remove-Item`.

**1. File workspace** (se richiesto) — `<nome>.code-workspace` nella cartella corrente, percorsi relativi al file:

```json
{
  "folders": [
    { "path": "backend" },
    { "path": "frontend" }
  ],
  "settings": {}
}
```

La cartella che contiene il `.code-workspace` non è un repo git: i repo sono le folder elencate.

**2. Solution:**

```powershell
Set-Location <root-repo-backend>
dotnet new sln -n <solution>            # default: <solution>.slnx
# dotnet new sln -n <solution> -f sln   # solo se richiesto il formato legacy
```

**3. Progetti .NET** — `dotnetTemplate` e `targetPath` dal catalogo:

```powershell
dotnet new <template> -n <nome> -o <targetPath>\<nome> --framework net10.0 --no-restore
```

La tipologia `minimal-api` usa il template `web` (ASP.NET Core vuoto), **non** `webapi`: la struttura `Endpoints/` delle guide parte dal template vuoto.

**4. Aggancio alla solution:**

```powershell
dotnet sln <solution>.slnx add src\<nome>\<nome>.csproj test\<nome>\<nome>.csproj
```

Le solution folder `/src/` e `/test/` le genera `dotnet sln add` da sé: non crearle a mano.

Se nella cartella c'erano già `*.csproj` sciolti (caso di arrivo dalla Sezione B), agganciali con lo stesso comando **senza spostarli**: la posizione sul disco è già quella che la solution rispecchia, e muoverli romperebbe i `ProjectReference` esistenti.

**5. Frontend — prima dell'install dei pacchetti:**

```powershell
Set-Location <root-repo-frontend>
npm create vue@latest <nome-fe> -- --ts --router --pinia --eslint --prettier
Set-Location <nome-fe>
npm install
```

I flag di `create-vue` sono **booleani puri**: `--vitest=false` fa fallire il comando con `ERR_PARSE_ARGS_INVALID_OPTION_VALUE`; le opzioni non desiderate si omettono. `create-vue` scrive un proprio `.editorconfig`/`.gitignore`/`.gitattributes`, quindi va eseguito **prima** dell'install del core (che poi farà `[SKIP]` su quei file).

**6. git init e commit iniziale — uno per repo:**

```powershell
Set-Location <root-repo>
git init
git add -A
git commit -m "chore: scaffolding iniziale"
```

Dopo la creazione dei progetti, non prima. Nel caso multi-repo: un `git init` per ogni folder del workspace, mai nella cartella che contiene il `.code-workspace`.

**7. Pacchetti dr-*:**

```powershell
Push-Location <root-repo>
& <path-locale>\dr-guidelines\dr-guidelines-install.ps1      # il core sempre per primo
& <path-locale>\dr-minimalapi\dr-minimalapi-install.ps1
Pop-Location
```

Senza clone locale, stessa sequenza via `gh`:

```powershell
Push-Location <root-repo>
& ([scriptblock]::Create((gh api repos/davraf-amuro/dr-guidelines/contents/dr-guidelines-install.ps1 -H "Accept: application/vnd.github.raw" | Out-String)))
& ([scriptblock]::Create((gh api repos/davraf-amuro/dr-minimalapi/contents/dr-minimalapi-install.ps1 -H "Accept: application/vnd.github.raw" | Out-String)))
Pop-Location
```

L'installer usa la directory corrente come root dell'host: `Push-Location`/`Pop-Location` sono obbligatori e il target è la **root del repo**. `Directory.Build.props` e `global.json` non arrivano dal core: li porta `dr-dotnet-backend` tramite i suoi `rootFiles`, quindi un repo frontend non li riceve semplicemente perché quel pacchetto non è tra i suoi.

**8. Formattazione — non è opzionale:**

```powershell
dotnet format <solution>.slnx
```

I template `dotnet new` scrivono UTF-8 **con BOM** mentre `.editorconfig` del core impone `charset = utf-8`: senza questo passo `dotnet format --verify-no-changes` esce `2` e il gate di push del progetto blocca il primo `git push`. Va eseguito **dopo** l'install del core.

Per il frontend: lo script `lint` generato da `create-vue` usa `--fix` (corregge invece di verificare), quindi non vale come gate; per verificare serve `oxlint .` / `eslint .` senza `--fix`.

**9. Passi finali:**

- Se tra i pacchetti c'è `dr-efdb`, **proponi** di registrare il server MCP `db-schema` in `.mcp.json`: senza quel tool lo scaffolding CRUD (`docs/scaffolding-crud.md`) deve chiedere i campi a mano.
- Genera `launch.json`/`tasks.json` seguendo `dr-create-launch-profiles` (o la procedura equivalente), non a mano qui.
- Se è stato creato un `.code-workspace`, ricorda di aprirlo: le istruzioni e le skill installate vengono caricate all'apertura.

### A.5 Verifica

| Dopo | Comando | Atteso |
|---|---|---|
| 2 | `Get-ChildItem *.slnx, *.sln` | un solo file, nome corretto |
| 4 | `dotnet sln <solution>.slnx list` | tutti i `.csproj` elencati |
| 4 | `dotnet build <solution>.slnx --nologo` | `Avvisi: 0  Errori: 0` |
| 5 | `npm run build` nella cartella FE | build completata |
| 7 | `Get-Content .ai\dr-guidelines-packages.json` | una voce per pacchetto, data odierna |
| 7 | `git status --short` | `.mcp.json` assente (ignorato dal `.gitignore` del core) |
| 8 | `dotnet format <solution>.slnx --verify-no-changes` | exit code `0` |

Verifica fallita → fermati e riportala, non proseguire.

---

## Sezione B — Aggiungi un progetto a una solution esistente

1. Individua il file solution (`*.slnx` o `*.sln`). Più di uno → elencali e chiedi quale usare.

   Nessuno → **non fermarti**: distingui cosa c'è davvero e proponi il recupero.

   | Cosa trovi | Cosa proponi |
   |---|---|
   | Cartella vuota, o soli file di appoggio | "Non c'è nessuna solution qui. Ne creo una e ci aggiungo `<tipologia>`?" |
   | `*.csproj` sciolti, nessun file solution | "Trovo `<N>` progetti senza solution: `<elenco>`. Creo la solution, aggancio quelli esistenti e poi aggiungo `<tipologia>`?" |
   | Solo `package.json` e la tipologia chiesta è .NET | "Questo è un repo frontend: la solution .NET va in un repo separato. Procedo con il workspace multi-repo?" |
   | Una sottocartella di primo livello contiene una solution | "La solution è in `<sottocartella>`, non qui. Lavoro lì dentro?" — se sì, prosegui da quel percorso |

   Accettato → **Sezione A**, con tipologia e nome già raccolti qui: non richiederli. Rifiutato → fermati: senza solution non c'è niente a cui agganciare un progetto.

2. Leggi `dotnet sln <solution> list` per sapere cosa c'è già.
3. Chiedi **tipologia** (lista da `projectTypes[]`) e **nome** (default `<solution><defaultNameSuffix>`), validando il nome con il pattern del catalogo.
4. Dry-run + una conferma. La cartella del nuovo progetto (`<targetPath>\<nome>`) deve non esistere: se esiste, anche vuota → STOP, mai `--force` su `dotnet new`. Il resto del repository qui è popolato per definizione — c'è una solution: non è un motivo per fermarsi.
5. Esegui:

```powershell
dotnet new <template> -n <nome> -o <targetPath>\<nome> --framework net10.0 --no-restore
dotnet sln <solution> add <targetPath>\<nome>\<nome>.csproj
dotnet build <solution> --nologo
dotnet format <solution>
```

6. Se serve un riferimento tra progetti, **chiedi** quale progetto deve referenziare quale — non dedurlo:

```powershell
dotnet add <targetPath>\<consumatore>\<consumatore>.csproj reference <targetPath>\<nome>\<nome>.csproj
```

7. Confronta i `suggestedPackages[]` della nuova tipologia con il manifest: se manca qualcosa, segnalalo e proponi la Sezione C. Non installare da qui.

---

## Sezione C — Installa i pacchetti dr-* in un progetto esistente

1. **Guard:** se la cartella corrente contiene sia `dr-guidelines-install.ps1` sia `dr-guidelines-install-lib.ps1` (o è un pacchetto dominio), fermati: "Sei in un repo sorgente dr-*: i pacchetti non si installano dentro se stessi."
2. Verifica di essere alla **root del repository** (`.git` nella cartella corrente). In una sottocartella → fermati: i file del core sono letti solo dalla root.
3. Rileva lo stack per filtrare i pacchetti per `appliesTo`:

| Segnale | Stack |
|---|---|
| `*.csproj` / `*.sln` / `*.slnx` | `dotnet` |
| `package.json` senza `.csproj` | `node` |
| Entrambi | multi-stack |
| `Workers/*.cs` | orientato a `dr-winsvc` |
| `Endpoints/*.cs` | orientato a `dr-minimalapi` |

4. Leggi `.ai/dr-guidelines-packages.json` (assente = nessun pacchetto installato, normale al primo giro) e marca ogni pacchetto come: già installato · consigliato · opzionale. Il core `dr-guidelines` va sempre incluso se manca.
5. Mostra la selezione, chiedi una conferma, poi:

```powershell
Push-Location <root-repo>
& <path-locale>\dr-guidelines\dr-guidelines-install.ps1      # il core sempre per primo
& <path-locale>\<pacchetto>\<pacchetto>-install.ps1
Pop-Location
```

Senza clone locale, `gh api` legge anche i repo Private:

```powershell
Push-Location <root-repo>
& ([scriptblock]::Create((gh api repos/davraf-amuro/<pacchetto>/contents/<pacchetto>-install.ps1 -H "Accept: application/vnd.github.raw" | Out-String)))
Pop-Location
```

Un pacchetto che fallisce (rete, credenziali): riporta l'errore e **continua** con i successivi. Nessun `-Update` qui: in questa sezione si **aggiunge**. Per aggiornare pacchetti già installati si usa `dr-get-latest` (install con `-Update`).

6. Riepiloga: pacchetti installati, falliti con motivo, e — quando pertinente — la proposta `db-schema` in `.mcp.json` se è entrato `dr-efdb` e il promemoria di riavviare l'IDE perché le skill appena installate vengano caricate.

---

## Rollback (tutte le sezioni)

```powershell
# repo che ha già il commit iniziale
Set-Location <root-repo>
git clean -fd          # rimuove i file non tracciati aggiunti dopo il commit
git checkout -- .      # ripristina i tracciati modificati

# repo senza commit
Set-Location <cartella-padre>
Remove-Item <root-repo> -Recurse -Force
```

Prima di un `Remove-Item -Recurse -Force`: verifica il percorso, avvisa che l'operazione è irreversibile e cancella anche `.git`, chiedi conferma esplicita. Su un repo appena inizializzato non esiste remote: nessuna copia di sicurezza.

Per annullare il workspace: rimuovi la voce `{ "path": "..." }` dall'array `folders` del `.code-workspace`.

In `CLAUDE.md` l'installer inserisce solo la sezione tra `<!-- dr-guidelines -->` e `<!-- /dr-guidelines -->`, preservando il resto: per annullare quella, rimuovi il blocco tra i marker.

---

## Regole

- Una sola conferma per sezione, dopo il dry-run. Prima: nessuna scrittura, nemmeno una cartella.
- Nessun `git commit` oltre a quello iniziale dichiarato, nessun `git push`: lo scaffolding non pubblica niente.
- Non inventare nomi di solution o progetti.
- Non modificare a mano `.ai/dr-guidelines-packages.json` né `dr-guidelines-install-lib.ps1`.
- Non duplicare i `docs/scaffolding-*.md`: per la struttura interna dei progetti (Dto, Endpoints, Workers, Validators) rimanda a quei documenti.
- Root del repository: i file del core (`.github/`, `.claude/`, `.ai/`, `CLAUDE.md`, config radice) sono attesi e si lasciano intatti. Cartella di progetto (`src/<nome>`, `test/<nome>`, frontend) già esistente, anche vuota → STOP, mai sovrascrivere, mai `--force` su `dotnet new`. Contenuto inatteso nella root → elenca e chiedi.

## ✅ Checklist Post-Generazione

- [ ] Gate 0 eseguito e riportato prima di qualsiasi scrittura
- [ ] Tipologie e pacchetti presi dal catalogo, non da memoria
- [ ] Nomi validati con il pattern del catalogo
- [ ] Dry-run mostrato e conferma unica ottenuta
- [ ] Install eseguiti dalla root del repository, core per primo
- [ ] `dotnet format` eseguito dopo l'install del core
- [ ] Verifiche della sezione eseguite, esiti riportati

*Template v1.3 — 2026-09-22 — claude-opus-5 — root del repository distinta dalle cartelle di progetto (issue #3)*
