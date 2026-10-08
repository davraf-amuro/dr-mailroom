# Progetto

dr-mailroom: backend .NET 10 in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom` (branch `main`, remote `davraf-amuro/dr-mailroom`) e frontend Vue 3 in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe` (branch `main`, remote pubblico `davraf-amuro/dr-mailroom-fe`). Workspace VS Code: `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom.code-workspace`.

---

# Obiettivo

Realizzare `dr-mailroom-fe`, la SPA Vue 3 che implementa `docs/frontend-specifiche.md` (v1.1) con la direzione visiva "Hangar Federale".

- Piano: `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` (v1.2, `Stato: IN CORSO`, approvato dall'utente)
- Design: `.ai/plans/2026-10-08-dr-mailroom-fe/design.md` (v1.1)
- Nota di ripresa: `.ai/plans/2026-10-08-dr-mailroom-fe/handoff.md` (v1.1)

---

# Stato corrente

- Fasi 1–8 del piano `[x]`; Fasi 9, 9.1, 10, Checkpoint C1, 11–18 `[ ]`.
- Repo FE: 6 commit locali, ultimo `913c47a`. **Nessun push** su `origin`.
- Repo backend: ultimo commit `084ea00` (solo file del piano). Nessun push di questo commit verificato in sessione.
- Backend in esecuzione via `dotnet run --project src/dr-mailroom-api --launch-profile http`, avviato in background in questa sessione; `GET /api/v1/status` → `databaseReady: true`. Il cron Quartz è attivo e legge le caselle reali.
- `public/config.json` del FE presente in locale con la chiave API del backend, ignorato da git.

---

# Componenti completati

- Scaffold create-vue 3.24.0 (TS, router, Pinia, Vitest, Playwright, ESLint, Prettier)
- Linee guida installate: `dr-guidelines` (916b257) e `dr-fe` (6f66968)
- Dipendenze dello stack (versioni nella tabella del piano), script `lint:check`, `format:check`, `gen:api`; porta Vite fissa 5173
- Configurazione runtime da `config.json` e client API tipizzato (`src/services/api/`)
- Token di design, font self-hosted (Saira, JetBrains Mono), tema chiaro/scuro (`useTheme`, store `ui`)
- 11 componenti generici in `src/components/shared/`

---

# Componenti in lavorazione

- Nessuno (la Fase 8 è chiusa; la Fase 9 non è iniziata)

---

# Componenti mancanti

- Fase 9: guscio (sidebar, header), router per dominio, banner di stato del servizio
- Fase 9.1: icona dell'applicazione (favicon, PNG, manifest, `AppMark.vue`)
- Fase 10: vista Caselle; Checkpoint C1 con l'utente
- Fasi 11–13: viste Filtri, Esecuzioni, Statistiche
- Fase 14: test end-to-end Playwright
- Fase 15: container nginx
- Fase 16: profili di debug e compound nel workspace
- Fase 17: README FE e aggiornamento dei docs del backend
- Fase 18: gate di qualità e `/dr-audit-fe`

---

# File modificati

Repo `dr-mailroom` (backend):

- `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md`: v1.1 (allineamento alla specifica v1.1), v1.2 (fase icona), path del workspace, D-1/D-2/D-3 chiuse, stato delle fasi 1–8
- `.ai/plans/2026-10-08-dr-mailroom-fe/design.md`: etichetta "Esegui ora", token `--c-neutral`, `--c-control-border`, `--c-on-*`, tabella dei contrasti misurati, regola sulle icone
- `.ai/plans/2026-10-08-dr-mailroom-fe/handoff.md`: stato a fine sessione
- `.ai/plans/2026-10-08-dr-mailroom-fe/icon-prompt.md`: nuovo, prompt per generare l'icona con un generatore esterno
- `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md`: nota "da rivedere quando il frontend è pronto"
- `.ai/permessi-richiesti.md` (ignorato da git): 4 righe di permessi
- `HANDOFF.md`, `SESSION.md`, `DECISIONI.md`, `NEXT_PROMPT.md`: questo handoff

Repo `dr-mailroom-fe`: tutti i file sono nuovi; elenco per commit in `git log` (da `e799170` a `913c47a`).

---

# Decisioni progettuali

Dettaglio in `DECISIONI.md`. In sintesi:

- Repo FE pubblico
- TypeScript 6.0.x (non 7)
- `overrides` per il peer di `openapi-typescript`
- Chiave API solo in memoria, letta da `config.json`
- Font Saira confermato "per ora"
- Etichette operative dalla specifica, metafora solo come sopratitolo
- Commit locale per fase, Checkpoint C1 dopo la Fase 10
- Fase dedicata all'icona dell'applicazione
- Token aggiuntivi per bordi dei controlli e testo su fondi pieni

---

# Problemi aperti

- **Attività incompleta**: Fasi 9–18 del piano FE.
- **Limitazione**: `dr-guidelines-install.ps1 -Package dr-fe` non installa il core; segnalato in `davraf-amuro/dr-fe#1`.
- **Limitazione**: `typescript-eslint` 8.71 non supporta TypeScript 7 (`typescript-eslint/typescript-eslint#10940`).
- **Debito tecnico**: `openapi-typescript` 7.13 dichiara peer `typescript ^5.x`; usato con TS 6 tramite `overrides`.
- **Debito tecnico**: `npm audit` segnala 4 vulnerabilità high solo nelle dipendenze di sviluppo (`braces`, GHSA-vfj7-8cjw-p6xm); 0 in produzione.
- **Attività incompleta**: il piano backend `2026-10-02-mailroom-gmail-reader` è `INTERROTTO`, da rivedere a FE pronto.
- **Debito tecnico**: `src/views/HomeView.vue` e `src/router/index.ts` del FE sono provvisori.
- **Attività incompleta**: il riferimento "vuejs/language-tools #6121" nel piano non è risolvibile con `gh` (annotato nel piano).

---

# TODO

1. Fase 9: guscio, route `/mailboxes` (default), `/filters`, `/runs`, `/stats`, 404, banner di stato con `GET /status` ogni 60 s e `POST /status/retry-database`, ascolto di `onApiAlarm`
2. Fase 9.1: icona; usare `dr-mailroom-fe/design/icon-source.png` se l'utente la fornisce, altrimenti disegnarla da zero
3. Fase 10: vista Caselle con Pausa/Riprendi ed Elimina
4. Checkpoint C1: mostrare all'utente vista Caselle (light/dark), banner, stato vuoto, icona; raccogliere il giudizio su Saira
5. Fasi 11–18 nell'ordine del piano
6. Chiedere all'utente se fermare il backend avviato in background

---

# Validazione

Esiti osservati in sessione sul repo FE, dopo l'ultimo commit `913c47a`:

- build (`npm run build`): exit 0
- test (`npx vitest --run`): 21 passati su 21
- lint (`npm run lint:check`): exit 0
- type-check (`npm run type-check`): exit 0
- format (`npm run format:check`): exit 0
- prova dal vivo del client: 200 con chiave e `Access-Control-Allow-Origin: http://localhost:5173`, 401 senza chiave

Backend: build e test non eseguiti in questa sessione → Sconosciuto.

---

# Rischi

- Il backend in background esegue il cron sulle caselle Gmail reali finché non viene fermato.
- `public/config.json` contiene la chiave API in un repo pubblico: è ignorato da git; la Fase 18 prevede il controllo `git ls-files public/config.json`.
- Il CORS del backend ammette solo `http://localhost:5173`: la Fase 15 (container) richiede di allineare `Mailroom:Frontend:BaseUrl`.
- Gli id nello schema OpenAPI sono `number | string` (int64 di .NET 10): i componenti di dominio devono gestire entrambi.
- Lo script `lint` di create-vue usa `--fix`: il gate è `lint:check`.

---

# Prossimo passo consigliato

Eseguire la Fase 9 del piano `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` nel repo `dr-mailroom-fe`.

---

# Informazioni mancanti

- Se l'utente fornirà un'immagine sorgente per l'icona (Fase 9.1)
- Se il backend avviato in background va fermato o lasciato attivo
- Quando fare il push dei commit locali di entrambi i repo
