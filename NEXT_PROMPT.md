# Prompt di ripresa

Lavori sul progetto **dr-mailroom** in `E:\Davide\Progetti\dr-mailroom`, branch `tentativo-1`. Rispondi in italiano. Segui `CLAUDE.md` e `.github/copilot-instructions.md`; per il ciclo di lavoro `.github/instructions/dev-cycle.instructions.md` e `plan-tracking.instructions.md`.

## Contesto

dr-mailroom è un servizio .NET 10 (Minimal API + Quartz + EF Core su SQL Server):
- legge caselle Gmail in sola lettura con OAuth (scope `gmail.readonly`);
- indicizza le mail per le statistiche per mittente;
- applica filtri (mittente + StartsWith/Contains/Regex sull'oggetto);
- esegue i processi: estrazione allegati, allerta.

## Obiettivo

Completare il piano `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (`Stato: IN CORSO`).

## Stato

- Fasi 1–18 `[x]`; manca la **Fase 19** (verifica end-to-end su Gmail reale).
- Build: 0 avvisi; test: 87 verdi e 1 ignorato; `dotnet format --verify-no-changes` exit 0.
- DB `dr-mailroom` su `spectre` migrato.
- Container `dr-mailroom-api` attivo sulla porta 5180, configurato tramite `.env`.
- Nessuna casella collegata.
- Lavoro non committato.

## File rilevanti

- Piano: `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (sezioni "Fase 19" e "Criteri di verifica finale")
- Documenti: `docs/architettura.md`, `docs/avvio-e-configurazione.md`, `docs/setup-google-oauth.md`, `docs/frontend-specifiche.md`
- Richieste di esempio: `src/dr-mailroom-api/dr-mailroom-api.http`
- Codice: `src/dr-mailroom-core/Runs/RunOrchestrator.cs`, `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs`, `src/dr-mailroom-api/Program.cs`
- Segreti locali (non leggerli in output, non committarli): `src/dr-mailroom-api/appsettings.local.json`, `.env`, `Gmail/`

## Problemi aperti

- Redirect URI in Google Console da portare a `http://localhost:5180/api/v1/mailboxes/connect/callback`: l'utente deve confermarlo.
- Limiti noti in `docs/architettura.md`, sezione "Limiti noti".
- Issue su davraf-amuro/dr-guidelines #9, #10, #11 (non bloccanti).

## Prossimo passo

1. Chiedi all'utente:
   - la conferma del redirect URI in Google Console;
   - la casella Gmail da collegare;
   - un mittente campione con il conteggio atteso.
2. Esegui la Fase 19 del piano:
   - `GET /api/v1/mailboxes/connect` con l'header `x-api-key` (chiave in `.env`, non mostrarla), consenso nel browser;
   - backfill con `POST /api/v1/runs` e polling su `GET /api/v1/runs/{id}`;
   - verifica dello stato letto/non letto;
   - confronto delle statistiche;
   - filtro di test;
   - riapplica solo allegati eseguita due volte;
   - doppio avvio → 409.
3. Verifica i criteri finali, esegui `/dr-verify-plan`, poi imposta `Stato: COMPLETATO`.

Nessun `git push`. Commit solo su richiesta dell'utente.
