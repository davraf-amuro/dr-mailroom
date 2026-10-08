# Piano: quota Gmail — download sequenziale e coda FIFO di elaborazione parallela
Data: 2026-10-08
Stato: COMPLETATO

## Obiettivo
Il backfill di una casella reale non deve più fallire per superamento della quota Gmail "Units per minute per user"; le mail si scaricano una per volta e si elaborano in parallelo da una coda FIFO.

## Contesto
Giro 1 (casella 1) fallito dopo 18 s: `GetMetadataAsync` → HTTP 403, reason `rateLimitExceeded`, quota `Units per minute per user` (15.000 unità/minuto, `messages.get` = 5 unità).
Cause: retry solo su 429/5xx; `FetchMessagesAsync` scarica 4 metadati in parallelo senza limite di unità.
Decisione dell'utente (2026-10-08): niente parallelismo verso Gmail in ingestione; una mail per volta, messa in una coda FIFO elaborata in parallelo.

## Disegno
- **Produttore** = ingestione: scarica i metadati **in sequenza**, salva per pagina (checkpoint invariato), scrive gli id dei messaggi nuovi nella coda. Backfill: non scrive nella coda (D12 invariata).
- **Coda** = `Channel<long>` bounded, FIFO, `FullMode = Wait` (contropressione verso il produttore).
- **Consumatori** = `MaxParallelMessages` task, ciascuno con il **proprio scope DI** (DbContext non thread-safe): ricarica il messaggio, valuta i filtri, esegue i processi. Contatori con `Interlocked`.
- **Riapplica**: il produttore è la lista dei candidati letta da DB.
- **Sessione Gmail unica per giro**, aperta dall'orchestratore e condivisa tra ingestione e consumatori: un solo limitatore di quota per casella.
- **Limitatore di quota** (token bucket, `QuotaUnitsPerMinute`, default 12000) e **retry** su 429 e 403 `rateLimitExceeded`/`userRateLimitExceeded` (backoff 10, 20, 40, 80 s): restano, perché anche i consumatori chiamano Gmail (allegati).
- **Errori**: errore del produttore → la coda si chiude, i consumatori finiscono i messaggi già in coda, poi il giro chiude Failed. `TokenRevokedException` o errore imprevisto di un consumatore → annulla tutti, giro Failed. Gli errori del singolo processo restano gestiti come oggi.

## Scope
### File da modificare
- [x] `src/dr-mailroom-core/Gmail/GmailConstants.cs` — costi in unità e reason di quota
- [x] `src/dr-mailroom-core/Configuration/GmailOptions.cs` — `QuotaUnitsPerMinute`
- [x] `src/dr-mailroom-core/Gmail/GmailRetrySettings.cs` — `RateLimitBaseDelay`
- [x] `src/dr-mailroom-core/Gmail/GmailReader.cs` — passa il limite alla sessione
- [x] `src/dr-mailroom-core/Gmail/GmailSession.cs` — limitatore, costo per chiamata, retry di quota
- [x] `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs` — riceve sessione e `ChannelWriter<long>?`; download sequenziale; non apre più la sessione
- [x] `src/dr-mailroom-core/Runs/MessageProcessingQueue.cs` (nuovo) — coda FIFO e consumatori paralleli con scope propri
- [x] `src/dr-mailroom-core/Runs/RunOrchestrator.cs` — apre la sessione, avvia produttore e consumatori, chiude la sessione
- [x] `src/dr-mailroom-core/Configuration/ProcessingOptions.cs` — descrizione di `MaxParallelMessages` (consumatori della coda)
- [x] `src/dr-mailroom-core/MailroomCoreExtensions.cs` — registrazione della coda
- [x] `src/dr-mailroom-api/appsettings.json`, `.env.example`, `docs/avvio-e-configurazione.md`, `docs/architettura.md` — nuova chiave e nuovo flusso
- [x] `test/dr-mailroom-tests/Gmail/FakeGoogleHttp.cs`, `test/dr-mailroom-tests/Gmail/GmailSessionTests.cs` — test di quota
- [x] `test/dr-mailroom-tests/Ingestion/MailboxIngestionServiceTests.cs`, `FakeGmail.cs` — nuova firma; verifica download sequenziale (concorrenza massima 1); fake thread-safe
- [x] `test/dr-mailroom-tests/Runs/RunOrchestratorTests.cs` — elaborazione via coda; messaggi in coda lavorati anche se il produttore fallisce dopo
- [x] `src/dr-mailroom-core/dr-mailroom-core.csproj` — pacchetto `System.Threading.RateLimiting` 10.0.12 (aggiunto in Fase 1)
- [x] `src/dr-mailroom-core/Processes/AttachmentExtractionProcess.cs` — retry della rinomina su nome conteso (divergenza Fase 5, approvata dall'utente)

### Perimetro negativo
- Non toccherò: schema DB e migration, endpoint, processi (`AlertProcess`, `AttachmentExtractionProcess`), filtri, scheduler Quartz, lock
- Nessun commit né push
- Nessuna modifica al piano `2026-10-02-mailroom-gmail-reader`

## Fasi

### Fase 1: quota e retry nel client Gmail
- **Stato**: [x]
- **Nota**: aggiunto il pacchetto `System.Threading.RateLimiting` 10.0.12 a `dr-mailroom-core.csproj` (la class library non ha il framework ASP.NET Core)
- **File**: `GmailConstants.cs`, `GmailOptions.cs`, `GmailRetrySettings.cs`, `GmailReader.cs`, `GmailSession.cs`, test Gmail
- **Verifica passo**: build verde

### Fase 2: ingestione sequenziale come produttore
- **Stato**: [x]
- **Precondizione**: Fase 1 completata
- **File**: `MailboxIngestionService.cs`
- **Operazione**: EDIT
- **Azione**: firma `IngestAsync(mailboxId, runId, session, ChannelWriter<long>? newMessages, ct)`; `FetchMessagesAsync` sequenziale; dopo ogni insert non di backfill scrive gli id salvati nel writer; rimuove le dipendenze da `IGmailReader`, `ITokenProtector` e `ProcessingOptions` se inutilizzate
- **Verifica passo**: build verde
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md

### Fase 3: coda FIFO e consumatori
- **Stato**: [x]
- **Precondizione**: Fase 2 completata
- **File**: `Runs/MessageProcessingQueue.cs`, `MailroomCoreExtensions.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: classe con `RunAsync(session, filters, allowed, producer, ct)` che crea il canale bounded, avvia `MaxParallelMessages` consumatori con `IServiceScopeFactory`, esegue il produttore, chiude il canale, attende i consumatori e restituisce processati/errori
- **Verifica passo**: build verde
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md

### Fase 4: orchestratore
- **Stato**: [x]
- **Precondizione**: Fase 3 completata
- **File**: `RunOrchestrator.cs`
- **Operazione**: EDIT
- **Azione**: sessione unica per giro; giro normale = filtri letti prima, ingestione come produttore (writer null se backfill o nessun filtro); riapplica = candidati come produttore; rimuove `ApplyProcessesAsync` sequenziale
- **Verifica passo**: build verde
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md

### Fase 5: test
- **Stato**: [x]
- ⚠️ Divergenza Fase 5: con i consumatori in parallelo `AttachmentExtractionProcess` ha una race sul nome file: `SafeFileName.GetAvailablePath` (controllo `File.Exists`) + `File.Move(overwrite: false)`; due allegati con lo stesso nome (stesso giorno, mittente, file) scelgono lo stesso percorso e il secondo Move fallisce. Falliscono `ManyNewMessages_AreAllProcessedByParallelConsumersAsync` (6 errori su 12) e `Reapply_AttachmentsOnly_CreatesNoAlerts_AndIsIdempotentAsync`. La correzione tocca un processo (perimetro negativo): in attesa della decisione dell'utente.
- ✅ Risolta: l'utente ha scelto "Retry sul Move". `MoveToAvailablePath` ricalcola il nome e riprova (max `MaxRenameAttempts` = 10); `finalPath` si assegna solo dopo un Move riuscito, così il ramo di errore non cancella più il file di un altro consumatore. 95/95 test verdi su 3 esecuzioni.
- **Precondizione**: Fase 4 completata
- **File**: test di ingestione e giri
- **Operazione**: EDIT
- **Azione**: adeguare alla nuova firma; aggiungere i test "download sequenziale" e "messaggi in coda lavorati anche se il produttore fallisce"
- **Verifica passo**: `dotnet test dr-mailroom.slnx` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md

### Fase 6: configurazione e documentazione
- **Stato**: [x]
- **Precondizione**: Fase 5 completata
- **File**: `appsettings.json`, `.env.example`, `docs/avvio-e-configurazione.md`, `docs/architettura.md`
- **Operazione**: EDIT
- **Azione**: `QuotaUnitsPerMinute`; nuovo significato di `MaxParallelMessages`; flusso produttore/coda/consumatori
- **Verifica passo**: chiavi e flusso presenti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md

### Fase 7: verifica su casella reale
- **Stato**: [x]
- **Precondizione**: Fasi 1–6 completate
- **Azione**: `dotnet format dr-mailroom.slnx --verify-no-changes`; `docker compose up -d --build`; `POST /api/v1/runs`
- **Tool ammessi**: dotnet, docker, curl
- **Verifica passo**: `messagesRead` cresce, nessun giro Failed per quota

## Criteri di verifica finale
- [x] `dotnet build` 0 avvisi e 0 errori, `dotnet test` exit 0 (95 superati, 1 ignorato)
- [x] `dotnet format --verify-no-changes` exit 0 (dopo rinomina `_quotaLimiter`)
- [x] In ingestione nessuna chiamata Gmail concorrente (test `Ingestion_ReadsMetadataOneAtATimeAsync`, `MaxConcurrentMetadataReads` = 1)
- [x] Logging strutturato con placeholder nei nuovi log
- [x] Backfill della casella 1 avanza senza fallire per quota (giro 4: 1000 messaggi in ~4 min, nessun avviso di quota)

## Esito
- Issue alle linee guida: https://github.com/davraf-amuro/dr-dotnet-backend/issues/2
