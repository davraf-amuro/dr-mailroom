# Handoff — creazione di dr-mailroom-fe

Per chi riprende il lavoro sul frontend in una nuova sessione aperta dal workspace `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom.code-workspace`. Questa nota basta da sola: non serve la conversazione che l'ha prodotta.

## Stato al 2026-10-08, fine sessione

| Cosa | Stato |
|---|---|
| Piano | [plan.md](plan.md) v1.2, **`Stato: IN CORSO`**. Fasi **1–10 `[x]`** (2026-10-09). **In attesa del Checkpoint C1** (OK dell'utente su vista Caselle, icona, font Saira), poi Fase 11 |
| Design | [design.md](design.md) v1.1, con i contrasti misurati. Lessico: etichetta della specifica + metafora come sopratitolo ("Esegui ora" / "Sortita") |
| Repo FE | `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe`, remote pubblico https://github.com/davraf-amuro/dr-mailroom-fe, **nessun push ancora fatto**. Commit: `e799170` scaffold · `34d8e99` linee guida · `e875f9f` dipendenze · `d5498a0` client API · `dc6b13e` token e tema · `913c47a` componenti shared · `ea074dc` guscio · `15e93e4` icona · `083dc8b` vista Caselle |
| Icona | Fase 9.1. L'utente non vuole icone di default. Prompt per ChatGPT in [icon-prompt.md](icon-prompt.md): se l'utente fornisce `dr-mailroom-fe/design/icon-source.png`, ridisegnarla in SVG; altrimenti disegnarla da zero |
| Backend | Avviato in background in una sessione precedente con `dotnet run --project src/dr-mailroom-api --launch-profile http` (il cron legge le caselle reali). Se non risponde su `http://localhost:5180/api/v1/status`, chiedere all'utente prima di riavviarlo |
| Issue aperte | [davraf-amuro/dr-fe#1](https://github.com/davraf-amuro/dr-fe/issues/1): `-Package dr-fe` non installa il core |
| Altri piani | `mailroom-gmail-reader` `INTERROTTO`, da rivedere quando il frontend è pronto |

## Cosa c'è già nel codice FE

- `src/services/api/`: `runtimeConfig.ts` (legge `config.json` prima del mount), `client.ts` (`initApiClient`, `getApiClient`, `callApi(target, run)` → `ApiOutcome`), `problem.ts` (`interpretResponse`: 404 su lista → `empty`, 400 → `fieldErrors` camelCase, 401/403 → `unauthorized`, 503 → `databaseUnavailable`, errore di rete → `networkError`), `apiEvents.ts` (`onApiAlarm` per i segnali globali 401/403/503), `schema.d.ts` generato con `npm run gen:api`
- `src/assets/styles/{tokens,main}.css`: token come utility Tailwind (`bg-surface`, `text-ink-muted`, `border-control-border`, `text-on-federal`…), classi `.stencil`, `.tabular`, `.font-expanded`, `.visually-hidden`; tema scuro con `[data-theme='dark']`
- `src/composables/shared/useTheme.ts`, `src/stores/ui.ts`
- `src/components/shared/`: BaseButton, BasePanel, BaseBadge, ConfirmDialog (Reka AlertDialog, `v-model:open`, emette `confirm`, si chiude dal chiamante), SortableTable (generica, `v-model:sort`, slot `cell-<key>`), ProgressMeter, LiveRegion, EmptyState, FieldError, HazardStripe, SerialTag
- `src/views/{Filters,Runs,Stats}View.vue`: **provvisori**, riempiti nelle fasi 11–13
- Le date `…Utc` senza fuso si leggono con `parseUtc` (`src/utils/formatDate.ts`)
- `public/config.json`: locale, ignorato da git, con la chiave del backend (`Authentication:ApiKey:ApiKeyValue` in `dr-mailroom/src/dr-mailroom-api/appsettings.local.json`)

## Comandi utili (dalla radice di dr-mailroom-fe)

`npm run dev` · `npm run lint:check` · `npm run format:check` · `npm run type-check` · `npx vitest --run` · `npm run build` · `npm run gen:api` (backend avviato). Screenshot: `npx playwright screenshot [--color-scheme dark] http://localhost:5173 out.png` (Chromium installato).

## Insidie note

- **npm globale aggiornato alla 11.21**: la 10.7.0 falliva con `edgesOut`.
- **TypeScript resta alla 6.0.x**: `typescript-eslint` 8.71 accetta solo `<6.1.0` (typescript-eslint#10940). `openapi-typescript` ha peer `^5`: risolto con `overrides` in `package.json`.
- **Il gate è `lint:check`** (`oxlint . && eslint .`): `lint` usa `--fix`.
- **`schema.d.ts` è escluso da prettier** (`.prettierignore`).
- **Usa `@lucide/vue`**, tratto 1.75, dimensioni 16/20/24. Niente `@vueuse/motion` né `@headlessui/vue`.
- **`autoRefetch` di Pinia Colada non mette in pausa il polling a scheda nascosta**: va combinato con `enabled` legato a `useDocumentVisibility`. Il polling si ferma su `Completed`, `Failed`, `Skipped` e `Cancelled`.
- **`motion-v` ha `reducedMotion="never"` di default**: va impostato `"user"` in `MotionConfig`.
- **Il giallo `#FCE300` non va mai usato come colore del testo** (`--c-on-caution` sopra). `--c-panel-line` è solo decorativo.
- **Nessun nome, logo o sagoma protetti** in `src/`.
- **Commit locale a fine fase, nessun `git push`**: la fa l'utente oppure `/dr-promote-to`.

## Fonti di verità

- Cosa deve fare il frontend: `dr-mailroom/docs/frontend-specifiche.md` (v1.1)
- Organizzazione del codice: `dr-mailroom-fe/.github/instructions/frontend-organization.instructions.md`
- Contratto API: `http://localhost:5180/openapi/v1.json`, documentazione interattiva su `/scalar/v1`

*Handoff v1.2 — 2026-10-09 — claude-opus-5-5*
