Riprendi il lavoro sul progetto dr-mailroom. Rispondi in italiano.

Contesto:
- Backend .NET 10 Minimal API + EF Core (SQL Server) in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom`; frontend Vue 3 + TS + Pinia + Tailwind v4 in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe`.
- Segui `CLAUDE.md` e `.github/copilot-instructions.md` del repo backend (piani su disco, dichiarazione di scope, gate prima del push).
- API in container `dr-mailroom-api` su http://localhost:5180 (chiave nel campo `apiKey` di `dr-mailroom-fe/public/config.json`, header `x-api-key`); FE in container dietro http://mailroom.spectre.local; il dev server `npm run dev` (5173) è ammesso dal CORS.

Obiettivo: completare il brief `briefs/20261009-01.md` seguendo il piano coordinatore `.ai/plans/2026-10-09-completamento-fe/plan.md` (`Stato: IN CORSO`, approvato).

Stato:
- Fatte le Fasi 1, 2, 3, 3b, 4, 5b del piano coordinatore; nel piano FE `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` (v1.3) sono `[x]` Checkpoint C1 e Fase 11.
- Nessun commit nei due repo; non committare senza richiesta esplicita dell'utente.
- Leggi `HANDOFF.md` e `DECISIONI.md` nella radice del backend.

File rilevanti:
- `.ai/plans/2026-10-09-completamento-fe/plan.md`, `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` (Fasi 12 e 13), `.ai/plans/2026-10-08-dr-mailroom-fe/design.md`
- `docs/frontend-specifiche.md` (Vista 3 — Esecuzioni), `src/dr-mailroom-api/Endpoints/RunsMapping.cs`
- FE: `src/views/RunsView.vue`, modelli in `src/components/mailboxes/*`, `src/components/filters/*`, `src/composables/filters/useFilterReapply.ts` (produce `/runs?runIds=…&skippedMailboxIds=…`)

Problemi aperti:
- DbContext `NoTracking` di default: usa `AsTracking()` per ogni aggiornamento.
- Cartelle `src/dr-mailroom-core/Gmail/` e `test/dr-mailroom-tests/Gmail/` mai committate.
- Container FE senza la vista Filtri (va ricostruito).
- Nuovo brief `briefs/20261011-01.md` non letto: non eseguirlo senza indicazione dell'utente.

Prossimo passo: esegui la Fase 5 del piano coordinatore = Fase 12 del piano FE (vista Esecuzioni, compresa la lettura di `?runIds=` e `?skippedMailboxIds=`), con i test e i gate FE (`type-check`, `lint:check`, `format:check`, `vitest run`, `build`), poi segna `[x]` in entrambi i piani.
