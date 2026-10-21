# Piano: integrare le proposte `*.new` dell'installer dr-*
Data: 2026-10-21
Stato: COMPLETATO
Approvazione: utente in chat, 2026-10-21 — "procedi su main"
Branch: `main` (scelta dell'utente)

## Obiettivo
Prendere dalle proposte `*.new` dell'installer solo ciò che serve davvero e cancellarle.

## Analisi
- `dr-mailroom/.gitignore.new`: è il template generico. L'unica voce nuova è `.claude/settings.json.bak` (copia di sicurezza dell'installer). Tutto il resto del `.gitignore` attuale è specifico del progetto (`/Gmail/`, `client_secret*.json`, `.env`, `/data/`, `docs/*-wiki.md`, `TODO/`, log) e **resta**.
- `dr-mailroom/Directory.Build.props.new`: identico all'attuale salvo i segnaposto `YourCompany`/`YourProduct` → nulla da integrare, si cancella.
- `dr-mailroom-fe/.gitignore.new`: è il template **Visual Studio/.NET**, non adatto a un progetto Vue. Servono solo `.claude/settings.local.json` e `.claude/settings.json.bak`. `.mcp.json` non va aggiunto: nel FE è **tracciato di proposito** (solo `pdf-reader`, nessun segreto) e ignorarlo non avrebbe effetto.

## Scope
- [x] `dr-mailroom/.gitignore` — aggiungi `.claude/settings.json.bak`
- [x] `dr-mailroom-fe/.gitignore` — aggiungi `.claude/settings.local.json` e `.claude/settings.json.bak`
- [x] cancella `dr-mailroom/.gitignore.new`, `dr-mailroom/Directory.Build.props.new`, `dr-mailroom-fe/.gitignore.new`
- [x] commit nei due repo (+ questo piano)

### Perimetro negativo
- Nessun'altra riga dei `.gitignore` cambia; `Directory.Build.props` non si tocca; nessun file di codice.
- Push/PR/merge solo se richiesti.

## Fasi
### Fase 1: branch — [x] (non serve: commit su `main`)
### Fase 2: `.gitignore` dei due repo — [x]
### Fase 3: cancellazione dei `*.new` e commit — [x]

## Criteri di completamento
- [x] Nessun `*.new` nei due repo
- [x] `git check-ignore .claude/settings.json.bak` positivo in entrambi
- [x] Nessun file prima tracciato diventa ignorato (`git ls-files -ci --exclude-standard` vuoto)

## Esito verifica (2026-10-21)
- `git check-ignore`: `.claude/settings.json.bak` e `.claude/settings.local.json` ignorati in entrambi i repo.
- `git ls-files -ci --exclude-standard`: vuoto in entrambi (nessun file tracciato diventa ignorato).
- Nessun `*.new` rimasto.
