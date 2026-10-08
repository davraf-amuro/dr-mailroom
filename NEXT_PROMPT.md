# Prompt di ripresa

Lavori sul frontend **dr-mailroom-fe** (Vue 3 + Vite + TypeScript 6.0 + Pinia + Tailwind v4) in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe`, che consuma l'API .NET 10 di `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom`. Rispondi in italiano. Segui `CLAUDE.md` e `.github/copilot-instructions.md` di entrambi i repo; per il ciclo di lavoro `dev-cycle.instructions.md` e `plan-tracking.instructions.md`; per la struttura del codice FE `dr-mailroom-fe/.github/instructions/frontend-organization.instructions.md`.

## Contesto

- dr-mailroom legge caselle Gmail in sola lettura, applica filtri, estrae allegati e produce statistiche per mittente.
- Il frontend implementa `dr-mailroom/docs/frontend-specifiche.md` (v1.1) con la direzione visiva "Hangar Federale" di `dr-mailroom/.ai/plans/2026-10-08-dr-mailroom-fe/design.md`.
- Autenticazione: header `x-api-key`, chiave letta a runtime da `public/config.json` (locale, ignorato da git) e tenuta solo in memoria.

## Obiettivo

Completare il piano `dr-mailroom/.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` (v1.2, `Stato: IN CORSO`, approvato).

## Stato

- Fasi 1–8 `[x]`. Repo FE: ultimo commit locale `913c47a`, nessun push.
- Esistono già: `src/services/api/` (`runtimeConfig`, `client` con `callApi`, `problem` con `interpretResponse`, `apiEvents` con `onApiAlarm`, `schema.d.ts` generato), token e tema (`src/assets/styles/`, `useTheme`, store `ui`), 11 componenti in `src/components/shared/`.
- `src/views/HomeView.vue` e `src/router/index.ts` sono provvisori.
- Ultimi controlli: build, lint:check, type-check, format:check verdi; 21 test verdi.
- Backend avviato in una sessione precedente su `http://localhost:5180`. Se `GET /api/v1/status` non risponde, chiedi all'utente prima di avviarlo: il cron legge le caselle reali.

## File rilevanti

- `dr-mailroom/.ai/plans/2026-10-08-dr-mailroom-fe/plan.md`, `design.md`, `handoff.md`, `icon-prompt.md`
- `dr-mailroom/docs/frontend-specifiche.md`
- `dr-mailroom-fe/src/services/api/*.ts`, `src/components/shared/*.vue`, `src/assets/styles/*.css`
- `dr-mailroom/HANDOFF.md`, `DECISIONI.md`

## Problemi aperti

- `typescript-eslint` non supporta TS 7: restare su 6.0.x.
- `openapi-typescript` usato con TS 6 tramite `overrides` in `package.json`.
- Il gate di lint è `npm run lint:check`, non `lint` (che usa `--fix`).
- Gli id dell'API sono `number | string`.
- Icona (Fase 9.1): niente icona di default. Se esiste `dr-mailroom-fe/design/icon-source.png`, ridisegnala in SVG; altrimenti disegnala da zero.
- Nessun `git push`: lo fa l'utente o `/dr-promote-to`.

## Prossimo passo

Esegui la **Fase 9** del piano (guscio con sidebar e header, route `/mailboxes` default, `/filters`, `/runs`, `/stats`, 404, banner "Comunicazioni" con `GET /api/v1/status` ogni 60 s e "Riprova connessione" su `POST /api/v1/status/retry-database`, `aria-live="assertive"` solo per DB giù, ascolto di `onApiAlarm`, `MotionConfig reducedMotion="user"`). Verifica con i comandi della fase, fai il commit locale, segna `[x]` nel piano e prosegui con la Fase 9.1.
