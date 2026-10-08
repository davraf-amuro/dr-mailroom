# Decisioni

Fonte completa: `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (D1–D14 e T1–T12 prese prima di questa sessione) e `docs/architettura.md`. Qui ci sono le decisioni prese **in questa sessione**.

## Convenzioni dei pacchetti dr-* (C1–C12)

- **Contesto:** le istruzioni installate in Fase 2 imponevano struttura e stack diversi dai percorsi indicativi del piano.
- **Decisione:** URL `/api/v1`, persistenza in `Infrastructure/Mailroom/`, layer Service, `IValidator<T>`, endpoint di salute, SimpleAuthentication, stack obbligatorio, `appsettings.local.json`, Data Protection su file, `.vscode`.
- **Motivazione:** conformità a minimal-api-architecture, database-provider, input-validation e sensitive-data.
- **Conseguenze:** scope del piano aggiornato in Fase 3.

## Serilog attivo

- **Contesto:** l'istruzione chiede di decidere.
- **Decisione:** console + file giornaliero `logs/`, con ThreadId e TaskId.
- **Motivazione:** servizio autonomo, serve la diagnosi dei giri notturni.
- **Conseguenze:** `TaskIdEnricher`, `logs/` ignorata da git.

## Callback OAuth versionato

- **Contesto:** regola 2 della minimal-api (URL versionati).
- **Decisione:** `http://localhost:5180/api/v1/mailboxes/connect/callback`.
- **Motivazione:** conformità.
- **Conseguenze:** redirect URI in Google Console da aggiornare.

## EnableSensitiveDataLogging attivo

- **Contesto:** l'istruzione database-provider lo prevede; contrasta con T6.
- **Decisione:** attivo come da istruzione (scelta dell'utente).
- **Motivazione:** scelta esplicita dell'utente.
- **Conseguenze:** i log EF possono contenere il refresh token cifrato.

## Regole di validazione dei body (D15)

- **Contesto:** input-validation obbliga a chiedere le regole campo per campo.
- **Decisione:** regole proposte e approvate (dettaglio nel piano, D15).
- **Motivazione:** obbligo dell'istruzione.
- **Conseguenze:** validatori in `src/dr-mailroom-api/Validators/`.

## Riapplica di un filtro globale e giro su tutte le caselle (D16, D17)

- **Contesto:** un filtro con `MailboxId` null; `POST /runs` senza casella.
- **Decisione:** un giro per ogni casella attiva; le occupate si saltano; 409 solo se lo sono tutte.
- **Motivazione:** scelta dell'utente.
- **Conseguenze:** risposta `RunsStartedDto { runIds, skippedMailboxIds }`.

## Rimozione logica della casella (D18)

- **Contesto:** DELETE di una casella.
- **Decisione:** stato `Deleted`, token rimosso, nessun giro né modifica ai filtri (409), statistiche conservate; ricollegare un indirizzo eliminato è rifiutato.
- **Motivazione:** l'utente non vuole cancellazioni fisiche; il rifiuto del ricollegamento è una deduzione dell'agente comunicata all'utente.
- **Conseguenze:** nuovo valore di enum, guardia nell'orchestratore.

## Esito del giro scritto con un contesto dedicato

- **Contesto:** dopo un errore il DbContext del giro può contenere modifiche non salvate.
- **Decisione:** `FinishRunAsync` e `RecordRunOutcomeAsync` usano `IDbContextFactory`.
- **Motivazione:** evitare di persistere per errore un `HistoryId` avanzato.
- **Conseguenze:** test `FailedIncremental_DoesNotPersistAdvancedHistoryIdAsync`.

## Soppressione degli avvisi AV0029/AV0030

- **Contesto:** l'analyzer di Asp.Versioning 10.2 suggerisce un'API diversa dal pattern dell'istruzione.
- **Decisione:** mantenere il pattern dell'istruzione e sopprimere gli avvisi nel csproj.
- **Motivazione:** `/openapi/v1.json` verificato completo.
- **Conseguenze:** `NoWarn` motivato in `dr-mailroom-api.csproj`.

## `appsettings.local.json` solo in Development

- **Contesto:** nei test e nel container non deve sovrascrivere la configurazione.
- **Decisione:** caricato solo con `ASPNETCORE_ENVIRONMENT=Development`.
- **Motivazione:** isolamento dei segreti.
- **Conseguenze:** nel container i valori arrivano da `.env`.

## README snello + guida operativa in docs (Fase 18)

- **Contesto:** divergenza tra `readme-structure` e `readme-generator`.
- **Decisione:** README conforme a readme-generator; nuovo `docs/avvio-e-configurazione.md`.
- **Motivazione:** scelta dell'utente.
- **Conseguenze:** issue dr-guidelines#9.

## Documento di architettura come standard

- **Contesto:** l'utente apprezza `docs/architettura.md`.
- **Decisione:** proporre lo standard nel core.
- **Motivazione:** richiesta dell'utente.
- **Conseguenze:** issue dr-guidelines#10 e #11.
