# Piano: messaggio eliminato da Gmail non blocca più l'ingestione
Data: 2026-10-19
Stato: COMPLETATO

## Obiettivo
Un `messages.get` che risponde 404 (messaggio eliminato definitivamente dopo essere comparso in history o nella lista) salta quel messaggio con un warning, invece di far fallire il giro e lasciare la casella bloccata sullo stesso `HistoryId`.

## Analisi
- Sintomo: dopo il ricollegamento della casella ogni giro chiude `Failed` con "The service gmail has thrown an exception. HttpStatusCode is NotFound. Requested entity was not found."
- `history.list` elenca i `messageAdded` anche dei messaggi poi eliminati definitivamente (spam auto-cancellato, cestino svuotato, filtri Gmail). Con la casella ferma ~10 giorni la finestra è ampia.
- `MailboxIngestionService.FetchMessagesAsync` chiama `GetMetadataAsync` senza gestire il 404: l'eccezione risale, il giro fallisce e `HistoryId` resta invariato (salvato solo con i messaggi) → il giro successivo rilegge la stessa history e ricade sullo stesso 404.
- Stesso rischio, più raro, in backfill e scansione completa (messaggio eliminato tra `messages.list` e `messages.get`).
- Il 404 è già gestito in: `ListChangesSinceAsync` (→ `HistoryExpiredException`), `GmailTrashSession` (ripiego), `RepairUndatedMessagesAsync` (catch generico). I processi (`ContentArchiveProcess`, `AttachmentExtractionProcess`) intercettano già ogni eccezione per messaggio.
- `run.MessagesRead` continua a contare gli id ricevuti da Gmail, saltati compresi (nessun cambio di semantica).

## Scope
### File da modificare
- [x] `src/dr-mailroom-core/Gmail/GmailMessageNotFoundException.cs` — CREATE: eccezione di dominio con `GmailMessageId`, sullo stile di `HistoryExpiredException`
- [x] `src/dr-mailroom-core/Gmail/GmailSession.cs` — `GetMetadataAsync`: `GoogleApiException` 404 → `GmailMessageNotFoundException`
- [x] `src/dr-mailroom-core/Gmail/IGmailSession.cs` — summary di `GetMetadataAsync`: documenta l'eccezione su 404
- [x] `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs` — `FetchMessagesAsync`: su `GmailMessageNotFoundException` log warning strutturato e salto del messaggio
- [x] `test/dr-mailroom-tests/Ingestion/FakeGmail.cs` — nuova proprietà `MissingMetadataIds` (id per cui `GetMetadataAsync` lancia `GmailMessageNotFoundException`)
- [x] `test/dr-mailroom-tests/Gmail/GmailSessionTests.cs` — test: 404 su `messages.get` → `GmailMessageNotFoundException` con l'id
- [x] `test/dr-mailroom-tests/Ingestion/MailboxIngestionServiceTests.cs` — test: incrementale con un id sparito salva gli altri e fa avanzare `HistoryId`; backfill con un id sparito completa la pagina
- [x] `docs/architettura.md` — riallineamento: messaggio eliminato da Gmail durante la lettura viene saltato

### Perimetro negativo
- Non toccherò: `GetAttachmentPartsAsync`, `GetRawAsync`, `DownloadAttachmentAsync` e i processi (già protetti dal catch per messaggio), `GmailCallExecutor`, `RunOrchestrator`, `GmailTrashSession`, DB e migrazioni, frontend.
- Nessun cambio a `MessagesRead` / contatori del giro.
- Nessun commit e nessun push senza richiesta.

## Fasi

### Fase 1: Eccezione di dominio
- **Stato**: [x]
- **Precondizione**: `GmailMessageNotFoundException.cs` non esiste
- **File**: `src/dr-mailroom-core/Gmail/GmailMessageNotFoundException.cs`
- **Operazione**: CREATE
- **Azione**: `public sealed class GmailMessageNotFoundException(string gmailMessageId, Exception innerException) : Exception($"Messaggio Gmail {gmailMessageId} non trovato (eliminato)", innerException)` con proprietà `GmailMessageId`; summary XML in italiano
- **Tool ammessi**: nessuno
- **Verifica passo**: file riletto, stile identico a `HistoryExpiredException`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Mappatura 404 in GmailSession
- **Stato**: [x]
- **Precondizione**: Fase 1 `[x]`
- **File**: `src/dr-mailroom-core/Gmail/GmailSession.cs`, `src/dr-mailroom-core/Gmail/IGmailSession.cs`
- **Operazione**: EDIT
- **Azione**: in `GetMetadataAsync` racchiudi la sola chiamata `ExecuteAsync` in `try/catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)` → `throw new GmailMessageNotFoundException(gmailMessageId, ex)`; aggiorna i summary
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: Salto del messaggio nell'ingestione
- **Stato**: [x]
- **Precondizione**: Fase 2 `[x]`
- **File**: `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs`
- **Operazione**: EDIT
- **Azione**: `FetchMessagesAsync` riceve il logger (diventa metodo d'istanza); su `GmailMessageNotFoundException` scrive `LogWarning("Casella {MailboxId}: messaggio {GmailMessageId} eliminato da Gmail, saltato", ...)` e continua; aggiorna il summary
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0, nessuna interpolazione nel log
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: Test
- **Stato**: [x]
- **Precondizione**: Fase 3 `[x]`
- **File**: `test/dr-mailroom-tests/Ingestion/FakeGmail.cs`, `test/dr-mailroom-tests/Gmail/GmailSessionTests.cs`, `test/dr-mailroom-tests/Ingestion/MailboxIngestionServiceTests.cs`
- **Operazione**: EDIT
- **Azione**: aggiungi `MissingMetadataIds` al fake; test `GetMetadata_Http404_ThrowsGmailMessageNotFoundAsync`; test `Incremental_MissingMessage_IsSkipped_AndHistoryIdAdvancesAsync`; test `Backfill_MissingMessage_IsSkipped_AndPageCompletesAsync`
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet test` exit 0, 3 test nuovi verdi
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

### Fase 5: Riallinea il documento al codice
- **Stato**: [x]
- **Precondizione**: Fase 4 `[x]`
- **File**: `docs/architettura.md`
- **Operazione**: EDIT
- **Azione**: nella sezione sulla resilienza dei giri aggiungi che un messaggio eliminato da Gmail tra elenco e lettura (404 su `messages.get`) viene saltato con un warning e non blocca il giro; revisione e footer secondo `doc-versioning.instructions.md`
- **Tool ammessi**: nessuno
- **Verifica passo**: affermazione coerente col codice di Fase 2–3
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 6: Verifica finale
- **Stato**: [x]
- **Precondizione**: Fasi 1–5 `[x]`
- **File**: tutti quelli in Scope
- **Operazione**: nessuna
- **Azione**: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` su core, API e test; controllo a freddo con `/dr-verify-plan`
- **Tool ammessi**: nessuno
- **Verifica passo**: tutti exit 0, criteri soddisfatti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [x] 404 su `messages.get` (metadati) → `GmailMessageNotFoundException` (test)
- [x] Incrementale con un messaggio sparito: altri messaggi salvati, `HistoryId` avanzato, nessuna eccezione (test)
- [x] Backfill con un messaggio sparito: pagina completata, `BackfillPageToken` avanzato (test)
- [x] Warning con placeholder per ogni messaggio saltato
- [x] Test esistenti verdi, `dotnet format` pulito
- [x] `docs/architettura.md` allineato al codice
