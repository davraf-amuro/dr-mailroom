# Piano: frontend dr-mailroom-fe (SPA Vue 3, direzione "Hangar Federale")
Data: 2026-10-08
Stato: IN CORSO
Revisione: v1.1 del 2026-10-08 — allineato alla specifica v1.1 (pausa casella, annullamento giri), vedi "Registro revisioni"

Allegati: [design.md](design.md) — direzione visiva, token, lessico, motion · [handoff.md](handoff.md) — come riprendere in una nuova sessione.

## Obiettivo
Creare il repo `dr-mailroom-fe` con la SPA che implementa [frontend-specifiche.md](../../../docs/frontend-specifiche.md) con il design di [design.md](design.md), pronta da avviare in locale e da rilasciare in un container nginx.

## Decisioni di stack (ricerca npm del 2026-10-08)

| Area | Scelta | Versione | Motivo |
|---|---|---|---|
| Scaffold | `create-vue` | 3.24.0 | flag `--ts --router --pinia --vitest --playwright --eslint --prettier` |
| Framework | vue / vite / vue-router / pinia | 3.5.x / 8.3.x / 5.4.x / 4.0.x | stabili; Vue 3.6 ancora RC, escluso |
| TypeScript | **6.0.x** (pin `~6.0.0`) | — | ricontrollato il 2026-10-08 su richiesta dell'utente: TS 7.0.2 è uscito, ma `typescript-eslint` 8.71 ha peer `typescript <6.1.0` (supporto TS 7 aperto: typescript-eslint/typescript-eslint#10940). Il riferimento originale "vuejs/language-tools #6121" non è risolvibile via `gh`. 6.0.3 = ultima 6.x |
| Test | vitest 5 + @vue/test-utils + @playwright/test | 5.0.x / 2.5.x / 1.64.x | vitest 5 richiede Node ≥ 22.12 (installato: 26.5.0) |
| Client API | `openapi-typescript` (dev) + `openapi-fetch` | 7.13.x / 0.17.x | tipi generati da `/openapi/v1.json` (OpenAPI 3.1), client sottile, nessun lock-in |
| Data fetching | `@pinia/colada` + `@pinia/colada-plugin-auto-refetch` | 1.4.x / 0.2.x | stesso autore di Pinia/vue-router; polling con `autoRefetch` + `enabled` legato a `useDocumentVisibility` |
| Componenti accessibili | `reka-ui` (headless) | 2.11.x | Dialog con focus trap, Select, Switch, Toast; **niente shadcn-vue**: il suo aspetto predefinito combatte la direzione EFSF |
| Stile | Tailwind CSS v4 via `@tailwindcss/vite` | 4.3.x | token in `@theme` come CSS custom properties |
| Motion | `motion-v` con `MotionConfig reducedMotion="user"` + View Transitions API | 2.6.x | `@vueuse/motion` è fermo dal 2025 |
| Grafici | `echarts` + `vue-echarts`, import modulari, `aria` attivo | 6.1.x / 8.3.x | barre + heatmap con accessibilità integrata |
| Icone | `@lucide/vue` | 1.53.x | `lucide-vue-next` è deprecato |
| Utility | `@vueuse/core` | 15.0.x | `useDocumentVisibility`, `useDark`, `usePreferredReducedMotion` |
| Font | `@fontsource-variable/saira`, `@fontsource-variable/jetbrains-mono` | 5.3.0 | self-hosted, niente chiamate esterne |

Schema di autenticazione: deciso lato API — header `x-api-key` (specifica, sezione "Autenticazione con chiave API"). Nel repo FE `dr-minimalapi` non è installato, quindi si applica il ripiego di `frontend-organization.instructions.md` Regola 7: **chiave in memoria** (variabile di modulo del client), letta a runtime da `config.json`, mai in `localStorage`. Nessuna login, nessuna route guard.

## Scope

Radice: `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe\` (nuovo repo, accanto a `dr-mailroom`). Struttura conforme a `frontend-organization.instructions.md` Regola 3 (`dr-fe`).

### File da creare / modificare
- [ ] `dr-mailroom-fe/` — scaffold create-vue (file generati)
- [ ] `dr-mailroom-fe/package.json` — dipendenze, script `gen:api`, `lint:check`, `format:check`
- [ ] `dr-mailroom-fe/vite.config.ts` — plugin Tailwind, porta 5173 fissa
- [ ] `dr-mailroom-fe/public/config.example.json` — modello della configurazione runtime
- [ ] `dr-mailroom-fe/.gitignore` — esclude `public/config.json`
- [ ] `dr-mailroom-fe/src/main.ts` — carica la configurazione runtime prima del mount
- [ ] `dr-mailroom-fe/src/App.vue` — guscio applicativo
- [ ] `dr-mailroom-fe/src/assets/styles/tokens.css`, `main.css` — token e base
- [ ] `dr-mailroom-fe/src/services/api/schema.d.ts` — generato
- [ ] `dr-mailroom-fe/src/services/api/client.ts`, `problem.ts`, `runtimeConfig.ts`
- [ ] `dr-mailroom-fe/src/services/{status,mailboxes,filters,runs,stats,messages}Service.ts`
- [ ] `dr-mailroom-fe/src/stores/{ui,status,mailboxes,filters,runs,stats}.ts`
- [ ] `dr-mailroom-fe/src/router/index.ts`, `src/router/routes/{mailboxes,filters,runs,stats}.ts`
- [ ] `dr-mailroom-fe/src/components/shared/*` — generici (vedi Fase 10)
- [ ] `dr-mailroom-fe/src/components/{shell,status,mailboxes,filters,runs,stats}/*`
- [ ] `dr-mailroom-fe/src/composables/shared/*`, `src/composables/{mailboxes,filters,runs,stats}/*`
- [ ] `dr-mailroom-fe/src/views/{Mailboxes,Filters,Runs,Stats,NotFound}View.vue`
- [ ] `dr-mailroom-fe/src/utils/{formatDate,formatYearMonth,formatSerial}.ts`
- [ ] `dr-mailroom-fe/src/**/__tests__/*`, `e2e/*` — test
- [ ] `dr-mailroom-fe/Dockerfile`, `nginx/default.conf`, `.dockerignore`
- [ ] `dr-mailroom-fe/.vscode/launch.json`, `tasks.json` — via `/dr-create-launch-profiles`
- [ ] `dr-mailroom-fe/README.md` — via `/dr-professor`
- [ ] `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom.code-workspace` — compound di debug API + FE
- [ ] `dr-mailroom/docs/architettura.md`, `dr-mailroom/docs/frontend-specifiche.md` — da "futuro" a "esistente", link al repo FE e al design

### Perimetro negativo
- Non toccherò: codice del backend (`dr-mailroom/src/**`), configurazione CORS, schema DB, piani del backend (`mailroom-gmail-reader` `INTERROTTO`, `stop-worker-endpoint` `COMPLETATO`).
- Nessun endpoint nuovo: se una vista richiede dati non esposti → `⚠️ Divergenza`, non si aggira.
- Niente gestione delle allerte nel frontend (le legge il software esterno).
- Nessun `git push` dall'esecutore (regola esecutore 6): la push la fa l'utente o `/dr-promote-to`.
- Nessun asset, nome o logo di Gundam/Sunrise/Bandai (design.md, "Proprietà intellettuale").

## Regole di esecuzione
- **Commit locale a fine fase** (dalla Fase 3 in poi), messaggio Conventional Commits; **nessun push**.
- **Checkpoint C1** dopo la Fase 10: si mostra all'utente la vista Caselle funzionante (guscio, banner, tema, font) prima di proseguire.
- Etichette operative = testi della specifica; la metafora di design.md è solo sopratitolo (es. "Esegui ora" con sopratitolo "Sortita").

## Fasi (formato atomico — obbligatorio)

### Fase 1: Verifica prerequisiti
- **Stato**: [x] — node v26.5.0, npm 10.7.0, git 2.46.2, gh su davraf-amuro (scope repo), cartella e repo FE assenti
- **Precondizione**: nessuna
- **File**: nessuno
- **Operazione**: —
- **Azione**: verifica `node -v` ≥ 22.12, `npm -v`, `git --version`, `gh auth status` attivo su `davraf-amuro`, assenza di `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe\`
- **Tool ammessi**: shell (sola lettura)
- **Verifica passo**: tutti i comandi rispondono; la cartella non esiste
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Scaffold create-vue
- **Stato**: [x] — scaffold ok, `npm run build` → 0. Note: (a) l'npm globale 10.7.0 (`AppData\Roaming\npm`, usato anche da `Program Files\nodejs\npm.cmd`) fallisce `npm install` con `Cannot read properties of null (reading 'edgesOut')` (bug arborist): installazione fatta con `npx -y npm@11 install` (11.21.0), stesso approccio per le fasi successive finché l'npm globale non viene aggiornato; (b) create-vue 3.24.0 genera `vitest ^4.1.11` (non 5) e lo script `lint` = oxlint + eslint, entrambi con `--fix`: si allinea nella Fase 5; (c) `npm audit`: 0 vulnerabilità in produzione, 4 high solo dev (`braces`, GHSA-vfj7-8cjw-p6xm)
- **Precondizione**: Fase 1 `[x]`
- **File**: `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe\`
- **Operazione**: CREATE
- **Azione**: da `E:\Davide\Progetti\dr-mailroom-workspace\` esegui `npm create vue@3.24.0 dr-mailroom-fe '--' --ts --router --pinia --vitest --playwright --eslint --prettier`, poi `npm install` nella cartella creata. Va fatto **prima** del core `dr-guidelines` (nota catalogo `vue-fe`)
- **Tool ammessi**: shell
- **Verifica passo**: esistono `package.json`, `src/main.ts`, `src/router/index.ts`, `src/stores/`; `npm run build` esce con 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: Repository git locale e remoto
- **Stato**: [x] — commit `e799170 chore: scaffold create-vue`; repo pubblico https://github.com/davraf-amuro/dr-mailroom-fe creato, `origin` impostato, nessun push
- **Precondizione**: Fase 2 `[x]`; repo pubblico (D-1 chiusa)
- **File**: `dr-mailroom-fe/.git`
- **Operazione**: CREATE
- **Azione**: `git init -b main`, commit `chore: scaffold create-vue`; `gh repo create davraf-amuro/dr-mailroom-fe --public --source . --remote origin` **senza** `--push`
- **Tool ammessi**: shell, gh
- **Verifica passo**: `git log --oneline` mostra 1 commit; `git remote -v` punta a `davraf-amuro/dr-mailroom-fe`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: Installazione linee guida (core + dr-fe)
- **Stato**: [x] — commit `34d8e99`. Nota: `-Package dr-fe` installa **solo** dr-fe (il core non è una dipendenza nel catalogo), contrariamente a quanto lascia intendere il README di dr-fe ("Via core, un solo installer"); il core è stato installato con un secondo lancio senza parametri. `.editorconfig`/`.gitignore`/`.gitattributes` → `[SKIP]`, nessun file esistente modificato. Candidato a `/dr-file-feedback` su dr-fe
- **Precondizione**: Fase 3 `[x]`; cwd = radice `dr-mailroom-fe`
- **File**: `.github/`, `.claude/`, `CLAUDE.md`, `.ai/dr-guidelines-packages.json`
- **Operazione**: CREATE
- **Azione**: esegui l'installer del core con `-Package dr-fe` (README di `dr-fe`, "Via core, un solo installer"); file già scritti da create-vue → `[SKIP]` atteso
- **Tool ammessi**: shell (pwsh), gh
- **Verifica passo**: il manifest elenca `dr-guidelines` e `dr-fe`; esiste `.github/instructions/frontend-organization.instructions.md`; `.editorconfig`/`.gitignore` di create-vue invariati (`git diff --stat` solo file nuovi)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

### Fase 5: Dipendenze e script
- **Stato**: [x] — commit `e875f9f`. typescript 6.0.3, vitest 5.0.3; `lint:check` (= `oxlint . && eslint .`, senza `--fix`), `type-check`, `build`, `test:unit --run`, `format:check` → 0. Nota: `openapi-typescript` 7.13 ha peer `typescript ^5.x` → `overrides` mirato `{"openapi-typescript":{"typescript":"$typescript"}}` (niente `--legacy-peer-deps`); da validare con `gen:api` in Fase 6
- **Precondizione**: Fase 4 `[x]`
- **File**: `package.json`, `package-lock.json`, `vite.config.ts`
- **Operazione**: EDIT
- **Azione**: installa le dipendenze della tabella "Decisioni di stack" con le versioni minori indicate; fissa `typescript ~6.0.0`; aggiungi gli script `lint:check` (`eslint .` senza `--fix`), `format:check` (`prettier --check src/`), `gen:api` (`openapi-typescript http://localhost:5180/openapi/v1.json -o src/services/api/schema.d.ts`); in `vite.config.ts` aggiungi `tailwindcss()` e `server.port: 5173, strictPort: true`
- **Tool ammessi**: shell (npm)
- **Verifica passo**: `npm ls typescript` → 6.0.x; `npm run lint:check`, `npm run type-check`, `npm run build` → exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 6: Configurazione runtime e client API
- **Stato**: [x] — commit `d5498a0`. `gen:api` ok con l'override TS 6 (25 path, `cancel`/`pause` inclusi); 16 test verdi (`problem.ts` 200/202/204/400/401/403/404 lista e risorsa/409/503 + `runtimeConfig`); 0 occorrenze di storage in `src/services/`; prova dal vivo: 200 con chiave e CORS `http://localhost:5173`, 401 senza chiave. Aggiunte rispetto al piano: `apiEvents.ts` (segnali globali 401/403/503), `.prettierignore` per `schema.d.ts` generato, `callApi` che trasforma gli errori di rete in `networkError`. `public/config.json` locale creato dalla chiave del backend, ignorato da git
- **Precondizione**: Fase 5 `[x]`; backend avviato su `http://localhost:5180`
- **File**: `public/config.example.json`, `.gitignore`, `src/services/api/runtimeConfig.ts`, `src/services/api/schema.d.ts`, `src/services/api/client.ts`, `src/services/api/problem.ts`, `src/main.ts`
- **Operazione**: CREATE / EDIT
- **Azione**: `config.example.json` = `{ "apiBaseUrl": "http://localhost:5180", "apiKey": "" }`; `public/config.json` in `.gitignore`; `main.ts` fa `fetch('/config.json')` prima di `app.mount`; `npm run gen:api`; `client.ts` crea il client `openapi-fetch` con middleware che aggiunge `x-api-key` dalla variabile di modulo; `problem.ts` traduce le risposte nella tabella "Come interpretare le risposte" della specifica (404 su lista → vuoto, 400 → errori per campo camelCase, 409 → `detail`, 503 → evento globale DB giù, **401 → avviso globale "Chiave API mancante o non valida: controlla `config.json`"**, senza retry)
- **Tool ammessi**: shell (npm)
- **Verifica passo**: `schema.d.ts` contiene i path `/api/v1/mailboxes`, `/api/v1/runs`, `/api/v1/runs/{id}/cancel`, `/api/v1/mailboxes/{id}/pause`; test unitari di `problem.ts` per 200/400/401/404/409/503 verdi; nessuna occorrenza di `localStorage` in `src/services/`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

### Fase 7: Token di design, font e tema
- **Stato**: [x] — commit `dc6b13e`. Contrasti misurati e annotati in design.md: testo ≥ 4.5:1 in entrambi i temi; aggiunti `--c-control-border` (≥ 3:1, il `--c-panel-line` è solo decorativo) e i token `--c-on-*`. `main.css` spostato in `src/assets/styles/`; demo create-vue rimossa; `HomeView` provvisoria come campionario fino alla Fase 9. Verifica visiva con screenshot Playwright (light e dark). Chromium di Playwright installato (serve alla Fase 14)
- **Precondizione**: Fase 5 `[x]`
- **File**: `src/assets/styles/tokens.css`, `src/assets/styles/main.css`, `src/stores/ui.ts`, `src/composables/shared/useTheme.ts`
- **Operazione**: CREATE
- **Azione**: implementa i token di [design.md](design.md) "Token di design" (light + dark) esposti a Tailwind con `@theme`; importa Saira e JetBrains Mono da Fontsource; anello di focus globale; `useTheme` con `useDark` di VueUse; rimuovi gli stili demo di create-vue
- **Tool ammessi**: shell
- **Verifica passo**: ogni coppia testo/fondo dei token misurata con un checker di contrasto ≥ 4.5:1 (testo) e ≥ 3:1 (UI/focus); risultati annotati in design.md
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere

### Fase 8: Componenti generici (`components/shared/`)
- **Stato**: [x] — commit `913c47a`. 11 componenti; `ConfirmDialog` su Reka **AlertDialog** (conferma non chiude da sola: la chiude il chiamante, per gestire `busy`); `SortableTable` generica con slot `cell-<key>` e `v-model:sort`; 0 import da services/stores/schema; 21 test verdi (Esc + focus restituito, ordinamento, aria-sort); icone Lucide tratto 1.75, 16/20 px
- **Precondizione**: Fase 7 `[x]`
- **File**: `src/components/shared/{BaseButton,BasePanel,BaseBadge,ConfirmDialog,SortableTable,ProgressMeter,LiveRegion,EmptyState,FieldError,HazardStripe,SerialTag}.vue`
- **Operazione**: CREATE
- **Azione**: un componente per file, props tipizzate, zero tipi di dominio; `ConfirmDialog` su Reka UI Dialog; `SortableTable` con `<button>` + `aria-sort`; `ProgressMeter` su `<progress>` nativo (indeterminato senza `max`); `BaseButton` con stato `aria-busy`; `BasePanel` con panel lines e angoli smussati
- **Tool ammessi**: nessuno
- **Verifica passo**: nessun import da `@/services`, `@/stores` o tipi di `schema.d.ts` in `components/shared/` (grep); test unitari di `SortableTable` e `ConfirmDialog` (Esc, focus restituito) verdi
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere

### Fase 9: Guscio, router e banner di stato
- **Stato**: [ ]
- **Precondizione**: Fasi 6 e 8 `[x]`
- **File**: `src/App.vue`, `src/components/shell/{AppSidebar,AppHeader}.vue`, `src/router/index.ts`, `src/router/routes/*.ts`, `src/composables/shared/useViewTransition.ts`, `src/services/statusService.ts`, `src/stores/status.ts`, `src/components/status/ServiceStatusBanner.vue`, `src/views/NotFoundView.vue`
- **Operazione**: CREATE / EDIT
- **Azione**: route `/mailboxes` (default), `/filters`, `/runs`, `/stats`, 404; transizioni di vista con View Transitions API disattivate sotto `prefers-reduced-motion`; banner "Comunicazioni" con `GET /status` ogni 60 s e "Riprova connessione" (`POST /status/retry-database`), regione `aria-live="assertive"` solo per DB giù; `MotionConfig reducedMotion="user"` in `App.vue`
- **Tool ammessi**: nessuno
- **Verifica passo**: navigazione tra le 4 viste funziona da tastiera; con backend fermo o DB giù il banner compare; `npm run type-check` → 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere

### Fase 9.1: Icona dell'applicazione
- **Stato**: [ ]
- **Precondizione**: Fase 9 `[x]`
- **File**: `public/favicon.svg`, `public/favicon.ico` (sostituisce quello di Vue), `public/apple-touch-icon.png`, `public/icon-192.png`, `public/icon-512.png`, `public/site.webmanifest`, `index.html`, `src/components/shell/AppMark.vue`
- **Operazione**: CREATE / EDIT
- **Azione**: disegnare da zero un marchio coerente con design.md (richiesta esplicita dell'utente: niente icona di ripiego su un'interfaccia curata): forma a pannello smussato, monogramma "MR" stencil, accento tricolore e tacca gialla d'avvertimento; leggibile a 16 px (variante semplificata per la favicon) e grande; varianti light/dark in `favicon.svg` via `prefers-color-scheme`; PNG generati dal SVG; stesso marchio in `AppMark.vue` nella barra laterale; `<title>` "dr-mailroom" e `theme-color` in `index.html`
- **Tool ammessi**: shell (Playwright per rasterizzare)
- **Verifica passo**: nessun file di Vue residuo in `public/`; screenshot della favicon a 16, 32 e 180 px mostrati all'utente al Checkpoint C1; nessuna sagoma o emblema protetto
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9.1: <cosa>` in plan.md, non procedere

### Fase 10: Vista Caselle ("Hangar")
- **Stato**: [ ]
- **Precondizione**: Fase 9.1 `[x]`
- **File**: `src/views/MailboxesView.vue`, `src/components/mailboxes/*`, `src/composables/mailboxes/*`, `src/services/mailboxesService.ts`, `src/stores/mailboxes.ts`, `src/utils/formatSerial.ts`, `src/utils/formatDate.ts`
- **Operazione**: CREATE
- **Azione**: implementa la specifica "Vista 1 — Caselle" con il layout di design.md (cella eroe di sintesi, card per casella con matricola `MR-0000`, stato vuoto "Nessuna unità in hangar", collega/ricollega via `authorizationUrl`, ritorno `?oauth=&reason=` con i messaggi della specifica, **Pausa/Riprendi** (`POST /mailboxes/{id}/pause` e `/resume`, 204 → `Disabled` "In pausa" / `Active`, 409 con `detail`), eliminazione con `ConfirmDialog` + strisce d'avvertimento, vista tabella alternativa)
- **Tool ammessi**: nessuno
- **Verifica passo**: ogni riga della tabella `reason` della specifica ha un test; 409 su DELETE mostra "Attendi la fine del giro in corso"; test di Pausa (Active → Disabled), Riprendi (Disabled → Active) e del 409 su entrambe; ogni stato (`Active`, `Disabled`, `TokenRevoked`, `Deleted`) ha badge con testo; nessuna logica di business nella view (solo store/composable)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 10: <cosa>` in plan.md, non procedere

### Checkpoint C1: revisione visiva con l'utente
- **Stato**: [ ]
- **Precondizione**: Fase 10 `[x]`
- **File**: nessuno
- **Operazione**: —
- **Azione**: avvia FE + API, mostra all'utente la vista Caselle (light e dark), il banner, lo stato vuoto e l'icona dell'applicazione (favicon a 16/32/180 px); raccogli il giudizio su direzione visiva, icona e font Saira (D-3)
- **Tool ammessi**: shell (npm run dev)
- **Verifica passo**: OK esplicito dell'utente; eventuali correzioni registrate qui come nuova fase prima della Fase 11
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Checkpoint C1: <cosa>` in plan.md, non procedere

### Fase 11: Vista Filtri ("Regole d'ingaggio")
- **Stato**: [ ]
- **Precondizione**: Checkpoint C1 `[x]`
- **File**: `src/views/FiltersView.vue`, `src/components/filters/*`, `src/composables/filters/*`, `src/services/filtersService.ts`, `src/services/messagesService.ts`, `src/stores/filters.ts`
- **Operazione**: CREATE
- **Azione**: implementa "Vista 2 — Filtri": elenco come card-frase con cancellazione (`DELETE /filters/{id}` con `ConfirmDialog`), costruttore a frase con modalità form classico, validazioni lato client allineate a D15, errori 400 accanto al campo, anteprima dry-run in pannello affiancato con `aria-live`, link "Allegati salvati", modale Riapplica con processi del filtro, 202 → navigazione a `/runs` filtrata sui `runIds`
- **Tool ammessi**: nessuno
- **Verifica passo**: test del builder (campi condizionali `subjectValue`/`attachmentSubfolder`, almeno un processo); test della mappatura errori 400 per chiave camelCase; 409 mostra il testo della specifica
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 11: <cosa>` in plan.md, non procedere

### Fase 12: Vista Esecuzioni ("Ponte di lancio" + "Diario di bordo")
- **Stato**: [ ]
- **Precondizione**: Fase 11 `[x]`
- **File**: `src/views/RunsView.vue`, `src/components/runs/*`, `src/composables/runs/useRunPolling.ts`, `src/services/runsService.ts`, `src/stores/runs.ts`
- **Operazione**: CREATE
- **Azione**: implementa "Vista 3 — Esecuzioni": "Esegui ora" (sopratitolo "Sortita") con animazione catapulta (< 600 ms, ramo reduced-motion), avviso backfill per caselle con `backfillCompleted = false`, polling 3–5 s con `autoRefetch` che si ferma su `Completed`/`Failed`/`Skipped`/**`Cancelled`** e `enabled` legato alla visibilità della scheda, annunci `aria-live` max ogni 10 s, timbro "Missione completata", **"Annulla" per giro `Running` e "Annulla tutti" sopra la cronologia** (`POST /runs/{id}/cancel`, `POST /runs/cancel`, `ConfirmDialog` con il testo della specifica, 202 → il polling vede `Cancelled`, 409/404 come avviso), badge grigio `Cancelled` con motivo da `errorSummary`, cronologia con filtri e `SortableTable`, nomi casella da `GET /mailboxes/summary`, nome filtro dei giri di riapplica da `GET /filters/summary`, widget "ultimi giri" da `GET /runs/summary` nella cella eroe
- **Tool ammessi**: nessuno
- **Verifica passo**: test di `useRunPolling` (stop su ognuno dei 4 stati finali, `Cancelled` incluso; pausa con scheda nascosta; throttling annunci); test di Annulla e Annulla tutti (202, 409); 409 reso come avviso non rosso
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 12: <cosa>` in plan.md, non procedere

### Fase 13: Vista Statistiche ("Intelligence")
- **Stato**: [ ]
- **Precondizione**: Fase 12 `[x]`
- **File**: `src/views/StatsView.vue`, `src/components/stats/*`, `src/composables/stats/*`, `src/services/statsService.ts`, `src/stores/stats.ts`, `src/utils/formatYearMonth.ts`
- **Operazione**: CREATE
- **Azione**: implementa "Vista 4 — Statistiche": frase-titolo calcolata, classifica a barre orizzontali e heatmap mittente × mese con ECharts (import modulari, `aria` attivo, palette dai token), tabella equivalente ordinabile, clic su mittente → mensile filtrata, 404 → "Nessuna mail per i filtri scelti"
- **Tool ammessi**: nessuno
- **Verifica passo**: `formatYearMonth(202610)` → "ottobre 2026" (test); ogni grafico ha tabella alternativa raggiungibile da tastiera
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 13: <cosa>` in plan.md, non procedere

### Fase 14: Test end-to-end
- **Stato**: [ ]
- **Precondizione**: Fase 13 `[x]`
- **File**: `e2e/*.spec.ts`
- **Operazione**: CREATE
- **Azione**: smoke Playwright con API simulata (`page.route`): stato vuoto caselle, creazione filtro con errore 400, lancio giro con polling fino a `Completed`, banner DB giù; un passaggio con `reducedMotion: 'reduce'`
- **Tool ammessi**: shell (npx playwright)
- **Verifica passo**: `npm run test:e2e` → exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 14: <cosa>` in plan.md, non procedere

### Fase 15: Container
- **Stato**: [ ]
- **Precondizione**: Fase 14 `[x]`
- **File**: `Dockerfile`, `nginx/default.conf`, `.dockerignore`
- **Operazione**: CREATE
- **Azione**: build multi-stage (node 22 alpine → nginx alpine), fallback SPA `try_files $uri /index.html`, `config.json` montato come volume e servito con `Cache-Control: no-store`, nessuna chiave nell'immagine; documenta nel README la porta pubblicata e che `Mailroom:Frontend:BaseUrl` del backend deve coincidere con l'origine del container (CORS), **senza toccare il codice del backend**
- **Tool ammessi**: shell (docker build)
- **Verifica passo**: `docker build` → 0; container avviato con `config.json` montato serve `/mailboxes` e `/config.json`; `docker history` non contiene la chiave; con `BaseUrl` allineato la vista Caselle carica i dati dal container (nessun errore CORS in console)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 15: <cosa>` in plan.md, non procedere

### Fase 16: Debug combinato e workspace
- **Stato**: [ ]
- **Precondizione**: Fase 15 `[x]`
- **File**: `dr-mailroom-fe/.vscode/launch.json`, `dr-mailroom-fe/.vscode/tasks.json`, `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom.code-workspace`
- **Operazione**: CREATE / EDIT
- **Azione**: `/dr-create-launch-profiles` nel repo FE (profilo Vue); nel workspace aggiungi `launch.compounds` "API + FE" con `{ "folder": "api · dr-mailroom", "name": "Debug dr-mailroom-api (http)" }` e il profilo Vue della cartella `fe · dr-mailroom-fe`
- **Tool ammessi**: skill `dr-create-launch-profiles`
- **Verifica passo**: dal workspace il compound avvia API (5180) e FE (5173) e la vista Caselle carica i dati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 16: <cosa>` in plan.md, non procedere

### Fase 17: Documentazione
- **Stato**: [ ]
- **Precondizione**: Fase 16 `[x]`
- **File**: `dr-mailroom-fe/README.md`, `dr-mailroom/docs/architettura.md`, `dr-mailroom/docs/frontend-specifiche.md`
- **Operazione**: CREATE / EDIT
- **Azione**: `/dr-professor` per il README del FE (avvio, `config.json`, `gen:api`, container, rischio chiave API in SPA) e per aggiornare nei docs del backend lo stato del frontend da "futuro" a esistente, con link al repo e a design.md
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: README conforme a `readme-structure.instructions.md`; nei docs del backend nessuna occorrenza residua di "non esiste ancora"
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 17: <cosa>` in plan.md, non procedere

### Fase 18: Gate di qualità e audit
- **Stato**: [ ]
- **Precondizione**: Fase 17 `[x]`
- **File**: nessuno (solo lettura)
- **Operazione**: —
- **Azione**: `npm run lint:check`, `npm run format:check`, `npm run type-check`, `npm run test:unit -- --run`, `npm run build`; `git ls-files public/config.json` (atteso vuoto: repo pubblico); grep nomi protetti su `src/`; poi `/dr-audit-fe`; finding `ERROR` → nuova fase correttiva prima di chiudere
- **Tool ammessi**: shell, skill `dr-audit-fe`
- **Verifica passo**: tutti i comandi → exit 0; `config.json` non tracciato; grep nomi protetti → 0; nessun `ERROR` nel report di audit
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 18: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [ ] Repo `dr-mailroom-fe` creato, con `dr-guidelines` + `dr-fe` nel manifest
- [ ] Le 4 viste e il banner coprono tutti i 26 endpoint della "Mappa degli endpoint" della specifica v1.1 (alerts esclusi), pausa/ripresa e annullamento giri inclusi
- [ ] Ogni risposta 200/202/400/401/404/409/503 gestita come da tabella della specifica (401 con avviso su `config.json`)
- [ ] Struttura cartelle conforme a `frontend-organization.instructions.md` (shared vs dominio, uno store per dominio, nessuna logica nelle view)
- [ ] Chiave API solo in memoria; `config.json` non committato (`git ls-files` vuoto, repo pubblico); nessun `localStorage` per dati sensibili
- [ ] Contrasti misurati ≥ AA; ogni animazione ha il ramo `prefers-reduced-motion`; nessuno stato espresso solo col colore
- [ ] Nessun nome/logo/asset protetto (grep case-insensitive per "Gundam", "EFSF", "Minovsky", "Federation", "Federazione", "White Base", "RX-78", "Sunrise", "Bandai" su `src/` → 0)
- [ ] Icona dell'applicazione originale al posto di quella di Vue, approvata al Checkpoint C1
- [ ] Checkpoint C1 approvato dall'utente
- [ ] Un commit locale per fase, nessun push dall'esecutore
- [ ] Gate di Fase 18 tutti verdi; container funzionante
- [ ] Verifica indipendente con `/dr-verify-plan`

## Domande (chiuse il 2026-10-08)
- **D-1** ✅ Repo `davraf-amuro/dr-mailroom-fe` **pubblico**. Conseguenza: `public/config.json` e qualunque chiave restano fuori dal repo (già previsto)
- **D-2** ✅ Superata: il backend ha già `origin` su `davraf-amuro/dr-mailroom`
- **D-3** ✅ Saira confermato **per ora**; rivalutabile a FE visibile (cambio limitato a `tokens.css` e al pacchetto Fontsource)

## Fonti
- [docs/frontend-specifiche.md](../../../docs/frontend-specifiche.md), [docs/architettura.md](../../../docs/architettura.md)
- `.ai/dr-scaffolding-catalog.json` (tipologia `vue-fe`), `davraf-amuro/dr-fe` → `frontend-organization.instructions.md` v1.2, README
- Ricerca stack (registry npm, 2026-10-08) e ricerca di design (2026-10-08), subagenti

## Registro revisioni
- **v1.1 (2026-10-08)** — revisione con l'utente: pausa/ripresa casella (Fase 10) e annullamento giri con stop del polling su `Cancelled` (Fase 12) dalla specifica v1.1; gestione 401 (Fase 6); endpoint `filters/summary`, `runs/summary`, `DELETE /filters/{id}` assegnati (Fasi 11–12); etichetta "Esegui ora" allineata alla specifica; grep nomi protetti esteso; nota CORS sul container (Fase 15); controllo `config.json` non tracciato (Fase 18); commit per fase; Checkpoint C1 dopo la Fase 10.
- **v1.2 (2026-10-08)** — richiesta dell'utente: Fase 9.1 "Icona dell'applicazione" (marchio originale, favicon, PNG, manifest) e regola sulle icone d'interfaccia in design.md.

*Piano v1.1 — 2026-10-08 — claude-opus-5-5*
