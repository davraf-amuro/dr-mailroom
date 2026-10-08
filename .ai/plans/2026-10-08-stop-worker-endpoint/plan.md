# Piano: endpoint per fermare il worker — annullamento giri e pausa casella
Data: 2026-10-08
Stato: COMPLETATO

## Obiettivo
Fermare un giro in corso via API e mettere in pausa una casella in modo persistente, senza dover fermare il container.

## Contesto
- 2026-10-08: backfill della casella 1 (giro 4) fermato con `docker stop`. Quartz ha scritto `gave up waiting for its running jobs` e il container è uscito con 137 (SIGKILL).
- Causa: `AddQuartzHostedService(WaitForJobsToComplete = true)` attende i job **senza** cancellarne il token, quindi il giro non riceve mai la richiesta di stop.
- I giri rimasti `Running` dopo un arresto non pulito si chiudono già all'avvio (`RunProvider.FailInterruptedRunsAsync`), quindi non serve una correzione sul giro 4.
- Decisione dell'utente (2026-10-08): annullamento dei giri + pausa della casella salvata su DB.
- Piano `IN CORSO` [2026-10-02-mailroom-gmail-reader](../2026-10-02-mailroom-gmail-reader/plan.md), fermo alla Fase 19 (verifica end-to-end): questo piano è un suo prerequisito operativo, come [2026-10-08-gmail-quota-rate-limit](../2026-10-08-gmail-quota-rate-limit/plan.md).

## Disegno
- **Registro di cancellazione** `RunCancellationRegistry` (core, singleton): `runId → CancellationTokenSource` collegato al token del job. `Register`, `TryCancel(runId)`, `CancelAll()`.
- **Orchestratore**: registra il giro **prima** di leggerne lo stato (chiude la corsa con un annullamento arrivato prima dell'avvio). Lavora con il token del registro. Su `OperationCanceledException` chiude il giro `Cancelled` con `CancellationToken.None`, senza toccare `Mailbox.LastError`. Non rilancia. Il backfill riparte dalla pagina salvata.
- **Casella in pausa**: si riusa `MailboxStatus.Disabled`, già presente e mai usato. Nessuna migration: lo stato è salvato come stringa. L'orchestratore salta con `Skipped` anche le caselle `Disabled`. Il cron le esclude già (`GetActiveMailboxIdsAsync` filtra `Active`).
- **Nuovo stato** `RunStatus.Cancelled`, salvato come stringa: nessuna migration.
- **Arresto del servizio**: `ApplicationStopping` → `registry.CancelAll()`. I giri si chiudono `Cancelled` prima che Quartz smetta di aspettare.
- **Endpoint** (tutti con `RequireAuthorization`, metadati OpenAPI completi):

| Endpoint | Esito |
|---|---|
| `POST /api/v1/runs/{id}/cancel` | 202 annullamento richiesto; 404 giro inesistente; 409 giro non in corso. Se il giro è `Running` su DB ma non è registrato (orfano o non ancora partito), lo chiude `Cancelled` direttamente |
| `POST /api/v1/runs/cancel` | 202 con `RunsCancelledDto` (id annullati); 409 nessun giro in corso |
| `POST /api/v1/mailboxes/{id}/pause` | 204; stato → `Disabled` e annulla i giri in corso della casella. 404 inesistente; 409 se `Deleted` o `TokenRevoked`. Idempotente su `Disabled` |
| `POST /api/v1/mailboxes/{id}/resume` | 204; `Disabled` → `Active`. 404; 409 se non è `Disabled`. Idempotente su `Active` |

- **Riapplica** su una casella `Disabled` → 409 (`FilterService.CheckMailboxAsync`). Il giro manuale dà già 409, perché lo stato non è `Active`.

## Scope
### File da modificare
- [x] `src/dr-mailroom-core/Enums/RunStatus.cs` — valore `Cancelled`
- [x] `src/dr-mailroom-core/Runs/RunCancellationRegistry.cs` + `RunRegistration.cs` (nuovi) — registro dei giri annullabili e handle (una classe per file)
- [x] `src/dr-mailroom-core/MailroomCoreExtensions.cs` — registrazione singleton del registro
- [x] `src/dr-mailroom-core/Runs/RunOrchestrator.cs` — registrazione, salto `Disabled`, chiusura `Cancelled`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/RunProvider.cs` — `CancelRunIfRunningAsync`, `GetRunningRunIdsAsync(mailboxId?)`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/MailboxProvider.cs` — `SetStatusAsync` (pausa/ripresa)
- [x] `src/dr-mailroom-api/Dto/RunsCancelledDto.cs` (nuovo) — esito dell'annullamento multiplo
- [x] `src/dr-mailroom-api/Services/ServiceMessages.cs` — messaggi nuovi
- [x] `src/dr-mailroom-api/Services/RunService.cs` — `CancelAsync`, `CancelAllAsync`
- [x] `src/dr-mailroom-api/Services/MailboxService.cs` — `PauseAsync`, `ResumeAsync`
- [x] `src/dr-mailroom-api/Services/FilterService.cs` — 409 riapplica su casella in pausa
- [x] `src/dr-mailroom-api/Endpoints/RunsMapping.cs` — `POST /{id}/cancel`, `POST /cancel`
- [x] `src/dr-mailroom-api/Endpoints/MailboxesMapping.cs` — `POST /{id}/pause`, `POST /{id}/resume`
- [x] `src/dr-mailroom-api/Program.cs` — `ApplicationStopping` → `CancelAll`
- [x] `src/dr-mailroom-api/dr-mailroom-api.http` — richieste di esempio
- [x] `test/dr-mailroom-tests/Runs/RunOrchestratorTests.cs` — annullamento e casella in pausa
- [x] `test/dr-mailroom-tests/Api/EndpointTests.cs` — codici di risposta dei nuovi endpoint
- [x] `test/dr-mailroom-tests/Ingestion/FakeGmail.cs` — aggiunto in Fase 7: aggancio `MetadataReached` per simulare un giro in corso da annullare
- [x] `docs/architettura.md` — decisione, stati, flusso di annullamento e arresto
- [x] `docs/frontend-specifiche.md` — azioni "Annulla giro", "Pausa", "Riprendi"; badge `Cancelled`

### Perimetro negativo
- Non toccherò: migration EF, `MailboxIngestionService`, `MessageProcessingQueue`, `GmailSession` (rispettano già il token), configurazione Quartz (`WaitForJobsToComplete` resta), Dockerfile/compose, piano `2026-10-02-mailroom-gmail-reader`, `.env`

## Fasi (formato atomico)

### Fase 1: Stato `Cancelled`
- **Stato**: [x]
- **Precondizione**: `RunStatus` ha Running, Completed, Failed, Skipped
- **File**: `src/dr-mailroom-core/Enums/RunStatus.cs`
- **Operazione**: EDIT
- **Azione**: aggiungere `Cancelled` in coda
- **Tool ammessi**: nessuno
- **Verifica passo**: enum con 5 valori, `Cancelled` ultimo
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Registro di cancellazione
- **Stato**: [x]
- **Precondizione**: Fase 1 completata
- **File**: `src/dr-mailroom-core/Runs/RunCancellationRegistry.cs`, `src/dr-mailroom-core/MailroomCoreExtensions.cs`
- **Operazione**: CREATE + EDIT
- **Azione**: classe thread-safe (`ConcurrentDictionary`) con `Register(runId, token)` che restituisce un handle `IDisposable` con `Token` (all'uscita rimuove e libera il CTS), `TryCancel(runId)`, `CancelAll()`; registrazione `AddSingleton`
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: Provider
- **Stato**: [x]
- **Precondizione**: Fase 2 completata
- **File**: `RunProvider.cs`, `MailboxProvider.cs`
- **Operazione**: EDIT
- **Azione**: `RunProvider.CancelRunIfRunningAsync(runId, reason)` (chiude solo se ancora `Running`, restituisce bool) e `GetRunningRunIdsAsync(int? mailboxId)`; `MailboxProvider.SetStatusAsync(mailboxId, status)`. Commento inline su ogni operazione DB
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: Orchestratore
- **Stato**: [x]
- **Precondizione**: Fase 3 completata
- **File**: `src/dr-mailroom-core/Runs/RunOrchestrator.cs`
- **Operazione**: EDIT
- **Azione**: `Register` all'inizio di `ExecuteAsync`; token del registro per lock, ingestione e coda; casella `Disabled` → `Skipped` con messaggio dedicato; `catch (OperationCanceledException)` → `FinishRunAsync(Cancelled, …, CancellationToken.None)` con motivo (richiesta utente o arresto del servizio), log strutturato, nessun rilancio
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0; test esistenti verdi
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere
- **Nota di esecuzione**: il catch chiama `RunProvider.CancelRunIfRunningAsync` invece di `FinishRunAsync`. Chiude solo un giro ancora `Running` e non sovrascrive un giro già chiuso dall'API. `Register` rifiuta un `runId` già registrato: così una seconda esecuzione dello stesso giro non annulla la prima.

### Fase 5: Servizi e DTO
- **Stato**: [x]
- **Precondizione**: Fase 4 completata
- **File**: `RunsCancelledDto.cs`, `ServiceMessages.cs`, `RunService.cs`, `MailboxService.cs`, `FilterService.cs`
- **Operazione**: CREATE + EDIT
- **Azione**: logica e codici della tabella del Disegno; la pausa annulla anche i giri in corso della casella; riapplica su `Disabled` → Conflict
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 6: Endpoint e arresto
- **Stato**: [x]
- **Precondizione**: Fase 5 completata
- **File**: `RunsMapping.cs`, `MailboxesMapping.cs`, `Program.cs`, `dr-mailroom-api.http`
- **Operazione**: EDIT
- **Azione**: quattro endpoint con `WithName` (`PostRunCancel`, `PostRunCancelAll`, `PostMailboxPause`, `PostMailboxResume`), `WithSummary`, `WithDescription`, `Produces` per ogni esito; `app.Lifetime.ApplicationStopping.Register(registry.CancelAll)`; esempi `.http`. La rotta `/cancel` va mappata in modo da non collidere con `/{id:long}`
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0; endpoint visibili in `/openapi/v1.json`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

### Fase 7: Test
- **Stato**: [x]
- **Precondizione**: Fase 6 completata
- **File**: `RunOrchestratorTests.cs`, `EndpointTests.cs`
- **Operazione**: EDIT
- **Azione**: test — annullamento durante l'ingestione → `Cancelled`, lock rilasciato, giro successivo possibile; casella `Disabled` → `Skipped`; annullamento di un giro orfano → `Cancelled`; codici 202/404/409 per cancel, 204/404/409 per pause/resume
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet test` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere

### Fase 8: Documentazione
- **Stato**: [x]
- **Precondizione**: Fase 7 completata
- **File**: `docs/architettura.md`, `docs/frontend-specifiche.md`
- **Operazione**: EDIT
- **Azione**: nuova decisione D (annullamento e pausa), stati `Cancelled` e `Disabled` = in pausa, comportamento all'arresto; azioni e badge nel frontend; riga di versione aggiornata secondo `doc-versioning`
- **Tool ammessi**: nessuno
- **Verifica passo**: i quattro endpoint sono citati in entrambi i documenti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere

### Fase 9: Verifica sul container
- **Stato**: [x]
- **Precondizione**: Fase 8 completata
- **File**: nessuno
- **Operazione**: nessuna
- **Azione**: `docker compose up -d --build`; avvio di un giro manuale, `POST /runs/{id}/cancel` → giro `Cancelled` entro pochi secondi; `pause` → lo scatto del cron non crea giri; `resume`; `docker stop` durante un giro → log senza `gave up waiting`, giro `Cancelled`
- **Tool ammessi**: docker, curl
- **Verifica passo**: tutti i comportamenti osservati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere
- **Esito (2026-10-08)**: giro 6 annullato via API → `Cancelled` in meno di 1 s, secondo cancel 409; giro 7 + `docker stop` → uscita 0 in 1,2 s, `Cancelled` con motivo di arresto, nessun `gave up waiting`; `pause` 204 (idempotente), giro manuale 409, cron delle 22:30 → "0 caselle attive". `resume` non eseguito sul container per scelta: la casella resta in pausa come chiesto dall'utente; il comportamento è coperto da `EndpointTests`

## Criteri di verifica finale
- [x] `dotnet build dr-mailroom.slnx` e `dotnet test` exit 0
- [x] `dotnet format <progetto>.csproj --verify-no-changes` exit 0 su ogni progetto
- [x] Nessuna migration aggiunta
- [x] `POST /runs/{id}/cancel` chiude il giro `Cancelled` senza riavviare il container
- [x] Casella in pausa: nessun giro dal cron, giro manuale e riapplica → 409; dopo `resume` torna a girare (resume verificato dai test, non sul container)
- [x] `docker stop` durante un giro: giro `Cancelled`, nessun `gave up waiting for its running jobs`
- [x] Backfill annullato e poi ripreso dalla pagina salvata (log del giro 7: "ripresa backfill dalla pagina salvata" dopo il giro 6 annullato)
- [x] Logging strutturato con placeholder; metadati OpenAPI completi sui quattro endpoint
- [x] Verifica a freddo con `/dr-verify-plan`
