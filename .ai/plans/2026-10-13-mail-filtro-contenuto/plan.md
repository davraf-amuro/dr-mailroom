# Piano: Mail di un filtro con download del contenuto + "Elimina tutte" fino a una data
Data: 2026-10-13
Stato: IN CORSO

## Obiettivo
Per ogni filtro: tabella paginata delle mail corrispondenti, con download per riga del contenuto (.eml originale e .txt leggibile) salvato in lavorazione da un nuovo processo "Salva contenuto". In più, "Elimina tutte" del mittente accetta una data facoltativa: cestina le mail ricevute fino a quel giorno compreso e tiene le successive.

## Decisioni (utente + tavolo warroom, 2026-10-13)
- **D1 — Sorgente**: il contenuto si salva in lavorazione con il nuovo processo `ProcessType.ContentArchive` ("Salva contenuto"), non si legge da Gmail al clic.
- **D2 — Un solo file su disco**: si salva il `.eml` (Gmail `format=raw`). Il `.txt` si ricava dal `.eml` al momento del download con MimeKit: `TextBody` se c'è, altrimenti `HtmlBody` convertito con `MimeKit.Text.HtmlToText`. Così un miglioramento della conversione vale anche per le mail già archiviate.
- **D3 — Modello dati**: nuova entità `ArchivedMessageContent`, una per mail (`MessageId` unico, FK in cascata). Niente colonne su `Message`.
- **D4 — Cartella**: `Attachments:RootPath` + `Filter.AttachmentSubfolder`, come gli allegati. Nome file da `SafeFileName.Build(data, mittente, oggetto + ".eml")`. Limite di dimensione `Attachments:MaxSizeBytes`.
- **D5 — Purge**: quando la purga elimina una mail cestinata, elimina anche il suo `.eml` su disco: prima legge i percorsi, poi cancella le righe, infine i file. Gli allegati estratti restano come oggi (fuori scope).
- **D6 — Download**: una mail alla volta, niente zip. L'API richiede l'header `x-api-key`, quindi il FE scarica con `fetch` → blob → link temporaneo, non con `<a href download>`.
- **D7 — Copertura delle mail vecchie**: la pagina mostra quante mail del filtro non sono ancora archiviate. Il pulsante "Archivia le mancanti" lancia la riapplica esistente con solo `ContentArchive`, ed è attivo solo se il filtro ha quel processo.
- **D8 — Paginazione**: si fa in memoria dopo `FilterMatcher` (mittente filtrato in SQL, oggetto/regex in memoria), come l'anteprima. Ordinamento per data, verso Asc|Desc.
- **D9 — Elimina fino a una data**: il FE converte il giorno scelto (ora locale del browser) nell'istante UTC di inizio del giorno successivo e lo invia come `receivedBeforeUtc` (estremo escluso). Esempio: "1 gen 2026" → `2026-01-01T23:00:00Z` → cestinate le mail ricevute prima delle 00:00 del 2 gennaio (ora italiana). Il valore viene salvato su `Run.TrashReceivedBeforeUtc`. Senza data il comportamento resta quello attuale.
- **D10 — Conteggio nella conferma**: `GET /messages` accetta `receivedBeforeUtc` facoltativo, così il dialogo di conferma mostra quante mail andranno nel cestino con la data scelta.
- **D11 — Anteprima del testo**: pulsante "Testo" per riga che apre un popup (`BaseDialog`) col testo leggibile della mail, da selezionare e copiare (pulsante "Copia"). Il FE lo legge come testo dallo stesso `GET /messages/{id}/content?format=txt`; nessun endpoint in più. Disabilitato se la mail non è archiviata.
- **Fuori scope (rimandato)**: ricerca nel testo delle mail archiviate, download in blocco.

## Scope

### Backend — `dr-mailroom`
- [x] `src/dr-mailroom-core/dr-mailroom-core.csproj` — pacchetto `MimeKit`
- [x] `src/dr-mailroom-core/Enums/ProcessType.cs` — `ContentArchive`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Entities/ArchivedMessageContent.cs` — nuova entità
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Configurations/ArchivedMessageContentConfiguration.cs` — indice unico `MessageId`, FK in cascata, lunghezze da `ColumnLengths`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/MailroomDbContext.cs` — `DbSet`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Entities/Run.cs` — `TrashReceivedBeforeUtc`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Migrations/*_ContentArchive.cs` (+ snapshot nel percorso storico `src/dr-mailroom-core/dr_mailroom_core/...`) — nuova tabella e colonna
- [x] `src/dr-mailroom-core/Gmail/IGmailSession.cs`, `GmailSession.cs`, `GmailConstants.cs` — `GetRawAsync` (`format=raw`, decodifica base64url) tramite `GmailCallExecutor`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/ArchiveProvider.cs` — aggiunta, esistenza, lettura per mail, percorsi da purgare
- [x] `src/dr-mailroom-core/Processes/ContentArchiveProcess.cs` — processo idempotente: file temporaneo e poi rinomina, come `AttachmentExtractionProcess`
- [x] `src/dr-mailroom-core/Processes/MessageTextExtractor.cs` — `.eml` → testo (MimeKit)
- [x] `src/dr-mailroom-core/MailroomCoreExtensions.cs` / `MailroomProviderExtensions.cs` — registrazione DI
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/MessageProvider.cs`:
  - purga con cancellazione dei file;
  - `Get/CountTrashCandidatesAsync` con la data facoltativa.
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Filters/FilterMessageFilter.cs` (nuovo) e `SenderMessageFilter.cs` (`ReceivedBeforeUtc`)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/RunProvider.cs`, `Runs/RunOrchestrator.cs` — data del giro di cestino
- [x] `src/dr-mailroom-api/Dto/` — `FilterMessageDto`, `FilterMessagePageDto` (`TotalCount`, `MissingArchiveCount`, `HasContentArchive`), `TrashSenderRequest` (+`ReceivedBeforeUtc`), `RunDto` (+`TrashReceivedBeforeUtc`)
- [x] `src/dr-mailroom-api/Validators/` — `FilterMessageFilterValidator` (nuovo), `MessageContentFormatValidator` (nuovo), `TrashSenderRequestValidator` e `SenderMessageFilterValidator` (data in UTC)
- [x] `src/dr-mailroom-api/Services/FilterService.cs` — `GetMessagesAsync`
- [x] `src/dr-mailroom-api/Services/MessageService.cs` — `GetContentAsync`: percorso letto dal DB e verificato sotto `RootPath`
- [x] `src/dr-mailroom-api/Services/RunService.cs` — data nel conteggio e nel giro
- [x] `src/dr-mailroom-api/Endpoints/FiltersMapping.cs` — `GET /filters/{id}/messages`
- [x] `src/dr-mailroom-api/Endpoints/MessagesMapping.cs`:
  - `GET /messages/{id}/content?format=eml|txt`;
  - descrizioni di `trash-sender` e di `GET /messages` aggiornate.
- [x] `src/dr-mailroom-api/Scheduling/PurgeTrashedMessagesJob.cs` — log dei file cancellati
- [x] `test/**` — test nuovi (vedi Fase 15)
- [x] `docs/architettura.md`, `docs/endpoint-filters.md`, `docs/endpoint-messages.md` — documentazione e footer

### Frontend — `dr-mailroom-fe`
- [x] `src/services/api/schema.d.ts` — rigenerato (`npm run gen:api`)
- [x] `src/composables/filters/filterPresentation.ts` (+ `FilterBuilder.vue` se le opzioni non derivano da `processLabels`) — etichetta "Salva contenuto"
- [x] `src/services/filtersService.ts` — `getFilterMessages`
- [x] `src/services/messagesService.ts`:
  - `downloadMessageContent` (blob + nome file da `Content-Disposition`);
  - `getMessageText` (testo per il popup);
  - `trashSender` e `getSenderMessages` con `receivedBeforeUtc`.
- [x] `src/router/routes/filters.ts` — rotta `/filters/:id/messages`, fuori dal menu
- [x] `src/views/FilterMessagesView.vue`, `src/composables/filters/useFilterMessages.ts`, `src/components/filters/FilterMessagesTable.vue`, `src/components/filters/MessageTextDialog.vue` — nuova pagina e popup del testo
- [x] `src/components/filters/FilterCard.vue`, `src/views/FiltersView.vue` — azione "Vedi mail"
- [x] `src/views/SenderMessagesView.vue`, `src/composables/stats/useTrashSender.ts`, `src/composables/stats/trashPresentation.ts` — data "fino al" nella conferma di "Elimina tutte"
- [x] `src/**/__tests__/*` — test nuovi
- [x] `README.md` / `docs/` del FE, se descrivono le viste

### Perimetro negativo
- Non toccherò:
  - il token e il flusso di lettura, lo scope OAuth e lo stato letto/non letto in Gmail;
  - gli allegati già estratti e la loro purga;
  - le viste SQL delle statistiche;
  - le altre viste del FE.
- Niente ricerca nel testo né download in blocco.
- Nessuna cancellazione definitiva in Gmail.
- Il lavoro non tracciato nei repo resta com'è (`.agents/`, `.codex/`, `AGENTS.md`, `briefs/`, i piani FE non tracciati).
- Nessun commit né push senza richiesta.

## Fasi

### Fase 0: Branch
- **Stato**: [x]
- **Precondizione**: piano `2026-10-13-cestino-mittente` chiuso (`COMPLETATO`) con l'ok dell'utente; nessuna modifica tracciata in sospeso nei due repo
- **File**: nessuno
- **Operazione**: EDIT (git)
- **Azione**: creare `feat/mail-filtro-contenuto` da `feat/cestino-mittente` in entrambi i repo. Serve partire da lì perché la funzione del cestino non è ancora su `main`.
- **Tool ammessi**: git
- **Verifica passo**: `git branch --show-current` = `feat/mail-filtro-contenuto` nei due repo
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 0: <cosa>` in plan.md, non procedere

### Fase 1: Modello dati e migrazione (core)
- **Stato**: [x]
- **Precondizione**: Fase 0 fatta; verificato come è salvato `ProcessType` (int o stringa) ed eventuali CHECK che lo vincolano
- **File**:
  - `ProcessType.cs`, `ArchivedMessageContent.cs`, `ArchivedMessageContentConfiguration.cs`, `MailroomDbContext.cs`, `Run.cs`;
  - migrazione `ContentArchive`.
- **Operazione**: CREATE + EDIT
- **Azione**:
  - aggiungere `ContentArchive` in coda all'enum;
  - creare l'entità con `Id`, `MessageId` (unico, FK in cascata), `RelativePath`, `SizeBytes`, `Sha256`, `ArchivedAtUtc`;
  - aggiungere `Run.TrashReceivedBeforeUtc` (`DateTime?`);
  - generare la migrazione e aggiornare un eventuale CHECK su `ProcessType`.
- **Tool ammessi**: `dotnet ef migrations add`, `dotnet ef migrations script` (sola lettura dello script)
- **Verifica passo**: `dotnet build` a 0 avvisi; lo script SQL contiene solo la tabella nuova, l'indice unico, la FK e la colonna su `Run`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Lettura raw da Gmail (core)
- **Stato**: [x]
- **Precondizione**: Fase 1 fatta
- **File**: `IGmailSession.cs`, `GmailSession.cs`, `GmailConstants.cs`
- **Operazione**: EDIT
- **Azione**: `Task<byte[]> GetRawAsync(string gmailMessageId, CancellationToken)`, che chiama `messages.get` con `format=raw` passando per `GmailCallExecutor` (costo quota `MessagesGetCost` esistente), e decodifica base64url
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; nessun metodo che modifica Gmail aggiunto alla sessione di lettura
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: Provider, processo e conversione in testo (core)
- **Stato**: [x]
- **Precondizione**: Fasi 1–2 fatte
- **File**: `dr-mailroom-core.csproj`, `ArchiveProvider.cs`, `ContentArchiveProcess.cs`, `MessageTextExtractor.cs`, registrazione DI
- **Operazione**: CREATE + EDIT
- **Azione**:
  - aggiungere `MimeKit`;
  - `ContentArchiveProcess`:
    - se esiste già un `ArchivedMessageContent` per la mail, esito `Skipped`;
    - altrimenti `GetRawAsync`, controllo della dimensione, file temporaneo, rinomina nel primo nome libero, registrazione a DB con SHA-256;
    - in caso di errore, file rimosso ed esito `Failed`;
  - `MessageTextExtractor.ToText(Stream)`: `TextBody` se c'è, altrimenti `HtmlToText`; intestazioni Da/Oggetto/Data in testa;
  - registrazione DI del processo.
- **Tool ammessi**: `dotnet add package`
- **Verifica passo**: build ok; il processo compare tra gli `IMessageProcess` risolti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: Purga dei file archiviati (core + api)
- **Stato**: [x]
- **Precondizione**: Fase 3 fatta
- **File**: `MessageProvider.cs`, `ArchiveProvider.cs`, `PurgeTrashedMessagesJob.cs`
- **Operazione**: EDIT
- **Azione**: la purga legge i `RelativePath` delle mail da purgare, esegue la DELETE delle mail come oggi, poi cancella i file verificando che il percorso risolto resti sotto `RootPath`. Un file mancante o che non si riesce a cancellare si logga e non blocca la purga. Il log riporta righe e file eliminati.
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; test della Fase 15 sulla purga verdi
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

### Fase 5: Elenco mail del filtro (api)
- **Stato**: [x]
- **Precondizione**: Fase 3 fatta
- **File**: `FilterMessageFilter.cs`, `FilterMessageFilterValidator.cs`, `FilterMessageDto.cs`, `FilterMessagePageDto.cs`, `FilterService.cs`, `FiltersMapping.cs`, registrazione del validatore
- **Operazione**: CREATE + EDIT
- **Azione**: `GET /api/v1/filters/{id}/messages?page&pageSize&sortDir`.
  - Righe: `Id`, `MailboxId`, `Subject`, `ReceivedAtUtc`, `HasAttachments`, `IsArchived`.
  - Pagina: `TotalCount`, `MissingArchiveCount`, `HasContentArchive`.
  - Logica: stesso match dell'anteprima, senza le cestinate, paginazione in memoria, flag letto con una sola query `MessageId IN (...)` sulla pagina.
  - Esiti: 404 se il filtro non esiste; 200 con `TotalCount` 0 se non ci sono mail.
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; l'endpoint compare in OpenAPI con 200/400/404
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 6: Download del contenuto (api)
- **Stato**: [x]
- **Precondizione**: Fase 3 fatta
- **File**: `MessageService.cs`, `MessagesMapping.cs`, `MessageContentFormatValidator.cs` (o un enum `MessageContentFormat` in core)
- **Operazione**: CREATE + EDIT
- **Azione**: `GET /api/v1/messages/{id}/content?format=eml|txt`.
  - 404 se la mail non esiste o non è archiviata; 400 se il formato non è valido.
  - Il percorso viene solo dal DB e deve risolversi sotto `RootPath`: se no, 404 e log di avviso.
  - `eml` → `application/octet-stream` in streaming (`TypedResults.PhysicalFile`).
  - `txt` → `text/plain; charset=utf-8` prodotto da `MessageTextExtractor`.
  - Sempre `Content-Disposition: attachment` con nome sanitizzato e `X-Content-Type-Options: nosniff`.
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; i test della Fase 15 sul download (percorso fuori radice → 404) sono verdi
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

### Fase 7: "Elimina tutte" fino a una data (core + api)
- **Stato**: [x]
- **Precondizione**: Fase 1 fatta
- **File**: `TrashSenderRequest.cs`, `TrashSenderRequestValidator.cs`, `SenderMessageFilter.cs`, `SenderMessageFilterValidator.cs`, `RunService.cs`, `RunProvider.cs`, `MessageProvider.cs`, `RunOrchestrator.cs`, `RunDto.cs`, `MessagesMapping.cs`
- **Operazione**: EDIT
- **Azione**:
  - `ReceivedBeforeUtc` facoltativo nel body di `trash-sender` e nella query di `GET /messages`. Il validatore lo vuole in UTC.
  - Il filtro è `ReceivedAtUtc < ReceivedBeforeUtc`, usato nel conteggio, nei candidati e nella griglia.
  - Il valore viene salvato su `Run.TrashReceivedBeforeUtc` e riletto dall'orchestratore, ed esposto in `RunDto`.
  - Se non ci sono mail fino alla data, la risposta resta il 409 "nulla da cestinare" già esistente.
  - Descrizioni OpenAPI aggiornate.
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; senza data il comportamento è identico a oggi (test esistenti verdi)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere

### Fase 8: Documentazione backend
- **Stato**: [x]
- **Precondizione**: Fasi 1–7 fatte
- **File**: `docs/architettura.md`, `docs/endpoint-filters.md`, `docs/endpoint-messages.md`
- **Operazione**: EDIT
- **Azione**: documentare:
  - il processo `ContentArchive`, con le decisioni D2, D4 e D5;
  - i due endpoint nuovi;
  - `receivedBeforeUtc` su `trash-sender` e su `GET /messages`.

  Aggiornare i footer secondo `doc-versioning`.
- **Tool ammessi**: nessuno
- **Verifica passo**: i file riletti descrivono gli endpoint come implementati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere

### Fase 9: Schema API e servizi FE
- **Stato**: [x]
- **Precondizione**: Fasi 5–7 fatte; documento OpenAPI disponibile senza toccare l'API sulla 5180 (host di test, come nel piano del cestino)
- **File**: `schema.d.ts`, `filtersService.ts`, `messagesService.ts`, `filterPresentation.ts` (+ `FilterBuilder.vue` se serve)
- **Operazione**: EDIT
- **Azione**:
  - rigenerare lo schema;
  - `getFilterMessages`;
  - `downloadMessageContent(id, format)`: `fetch` con la chiave, blob, nome file da `Content-Disposition`, link temporaneo e `revokeObjectURL`;
  - `getMessageText(id)`: stesso endpoint con `format=txt`, restituisce la stringa;
  - `receivedBeforeUtc` in `trashSender` e `getSenderMessages`;
  - etichetta "Salva contenuto" e testo dell'azione ("salva il contenuto della mail").
- **Tool ammessi**: `npm run gen:api`
- **Verifica passo**: `npm run type-check` a exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere

### Fase 10: Pagina "Mail del filtro" (FE)
- **Stato**: [x]
- **Precondizione**: Fase 9 fatta
- **File**: `routes/filters.ts`, `FilterMessagesView.vue`, `useFilterMessages.ts`, `FilterMessagesTable.vue`, `MessageTextDialog.vue`, `FilterCard.vue`, `FiltersView.vue`
- **Operazione**: CREATE + EDIT
- **Azione**:
  - rotta `/filters/:id/messages`, fuori dal menu, con il ritorno a Filtri;
  - tabella costruita con `SortableTable` e `PaginationNav`, con colonne:
    - data;
    - oggetto;
    - allegati;
    - stato di archiviazione, espresso a parole e non col solo colore;
    - azioni "Testo" (popup), ".eml" e ".txt", con `aria-label` che include l'oggetto della mail;
  - `MessageTextDialog`: `BaseDialog` con oggetto e data nel titolo, testo in un blocco scorrevole a spaziatura preservata (`white-space: pre-wrap`), pulsante "Copia" (Clipboard API, esito in una live region), stati di caricamento ed errore, focus gestito da `BaseDialog`;
  - le azioni sono disabilitate, con il motivo spiegato, se la mail non è archiviata;
  - banner "N mail da archiviare" con pulsante che apre la riapplica limitata a `ContentArchive` (riuso di `useFilterReapply`/`ReapplyDialog`). Se il filtro non ha il processo, il banner invita a modificare il filtro;
  - stati vuoto, errore e caricamento come le altre tabelle;
  - icone del tema, nessuna icona di default;
  - in `FilterCard`, azione "Vedi mail".
- **Tool ammessi**: nessuno
- **Verifica passo**: type-check e lint a exit 0; screenshot della pagina con API finta
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 10: <cosa>` in plan.md, non procedere

### Fase 11: Data "fino al" in "Elimina tutte" (FE)
- **Stato**: [x]
- **Precondizione**: Fase 9 fatta
- **File**: `SenderMessagesView.vue`, `useTrashSender.ts`, `trashPresentation.ts`
- **Operazione**: EDIT
- **Azione**:
  - nel dialogo di conferma, un campo data facoltativo "Fino al (compreso)", vuoto = tutte;
  - quando la data cambia, il conteggio si aggiorna con `GET /messages?receivedBeforeUtc=…&pageSize=1`;
  - il testo dice quante mail e "ricevute fino al <data>; le successive restano";
  - funzione pura `endOfLocalDayExclusiveUtc(date)` (inizio del giorno dopo in ora locale → ISO UTC), passata al POST.
- **Tool ammessi**: nessuno
- **Verifica passo**: type-check e lint a exit 0; screenshot del dialogo con data
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 11: <cosa>` in plan.md, non procedere

### Fase 12–14: riservate
Non usate: la numerazione resta libera per divergenze o fasi da aggiungere su richiesta.

### Fase 15: Test
- **Stato**: [x]
- **Precondizione**: Fasi 1–11 fatte
- **File**: `test/**` (backend), `src/**/__tests__/*` (FE)
- **Operazione**: CREATE + EDIT
- **Azione**:
  - **Backend**:
    - `ContentArchiveProcess`: salva, idempotente (`Skipped`), oltre il limite, errore → file rimosso;
    - `MessageTextExtractor`: text/plain, solo HTML, multipart;
    - purga con file: riga e file eliminati; file mancante non blocca;
    - `GET /filters/{id}/messages`: paginazione con regex, flag archiviato, `MissingArchiveCount`, 404;
    - `GET /messages/{id}/content`: eml, txt, 404 se non archiviata, percorso fuori radice → 404, 400 sul formato;
    - validatori nuovi;
    - cestino con data: conteggio, candidati e orchestratore rispettano l'estremo escluso; senza data invariato.
  - **FE**:
    - `useFilterMessages`;
    - `FilterMessagesTable`: azioni disabilitate e `aria-label`;
    - `MessageTextDialog`: caricamento, testo, errore, copia;
    - `endOfLocalDayExclusiveUtc`;
    - `useTrashSender` con la data;
    - `downloadMessageContent` (nome file da `Content-Disposition`).
- **Tool ammessi**: `dotnet test`, `npx vitest run`
- **Verifica passo**: tutti i test a exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 15: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [x] Backend: `dotnet build` a 0 avvisi, `dotnet test` verde, `dotnet format <csproj> --verify-no-changes` pulito su core, api e test
- [x] Script SQL della migrazione controllato; migrazione applicata solo con l'ok dell'utente
- [x] FE: `npm run lint:check`, `format:check`, `type-check`, `npx vitest run`, `build-only` a exit 0
- [x] Il percorso del contenuto non viene mai dall'input: test di traversal verde
- [x] Senza data, "Elimina tutte" si comporta come prima (test esistenti verdi)
- [x] Documentazione aggiornata (Fase 8)
- [x] Verifica a freddo con `/dr-verify-plan`
- [ ] **Prova reale, con l'utente**:
  - aggiungere "Salva contenuto" a un filtro dei codici sconto, "Archivia le mancanti", aprire il popup "Testo" e copiare il codice, scaricare .eml e .txt di una mail;
  - "Elimina tutte" con una data su un mittente di prova: in Gmail vanno nel cestino solo le mail fino a quel giorno.

## Note di esecuzione
- **Fase 1**: `ProcessType` salvato come testo `nvarchar(30)` senza CHECK, quindi nessun vincolo da aggiornare. La migrazione `20261013204821_ContentArchive` contiene solo la tabella, l'indice unico, la FK e la colonna `Run.TrashReceivedBeforeUtc`. Namespace file-scoped e niente BOM, come le altre. **Non applicata al DB**.
- **Fase 3**:
  - MimeKit 4.18.1 non ha più `HtmlToText`. Al suo posto, in `MessageTextExtractor`, c'è un convertitore minimo basato su `HtmlTokenizer`:
    - salta script, style, head e title;
    - va a capo su br e sui tag di blocco;
    - compatta spazi e righe vuote.
  - **Variante**: la sottocartella del `.eml` è quella del primo filtro corrispondente **con `ContentArchive`**, non `FilterMatchResult.AttachmentSubfolder`, che è legata all'estrazione allegati. Per un filtro con solo `ContentArchive` la sottocartella resta facoltativa (senza = radice). `FilterMatchResult.cs` e `FilterRequestValidator.cs` non sono stati toccati.
- **Fase 6**: formato come query `format` (stringa), validato da `MessageContentQueryValidator` e convertito in `MessageContentFormat` (enum nuovo in core). Esito interno `MessageContentFile` (file su disco o byte del testo).
- **Fase 7**:
  - le date in ingresso passano da `UtcDates.ToUtc` (nuovo helper in core): Local → UTC, Unspecified → UTC. È più robusto che rifiutare il binding;
  - `RunProvider.CreateRunAsync` ha un parametro facoltativo in coda, così gli altri chiamanti restano invariati;
  - `ValidationRules.ReceivedBefore` è nuova.
- **File fuori dallo Scope iniziale, toccati per necessità**:
  - `test/.../Ingestion/FakeGmail.cs` (`GetRawAsync` finto);
  - `Runs/RunTestHarness.cs` e `Api/MailroomApiFactory.cs` (data del cestino, `AttachmentsRoot` pubblico);
  - `Processes/AttachmentExtractionProcessTests.cs`. Il suo `Dispose` cancellava l'intera cartella comune `dr-mailroom-tests` e rompeva a caso i test paralleli (`ManyNewMessages…`, la riapplica). Ora cancella solo la propria.
- **Backend verificato (Fasi 1–8)**:
  - build con 0 avvisi;
  - 241 test superati (35 nuovi), stabili su 4 esecuzioni;
  - `dotnet format --verify-no-changes` a exit 0 su core, api e test.
- **Fasi 9–11 (FE)**:
  - download con `fetch` → blob → link temporaneo (`utils/downloadFile.ts`), perché serve l'header `x-api-key`. Nome del file da `Content-Disposition`, preferendo `filename*`;
  - **fuori Scope, per necessità**: `Program.cs` del backend espone `Content-Disposition` nel CORS (+ `CorsTests`). Senza, il browser non lo legge;
  - "Salva contenuto" nel costruttore: `filterForm.ts`, `useFilterBuilder.ts`, `FilterBuilder.vue` (campo `archiveContent`), oltre a `filterPresentation.ts`;
  - `ConfirmDialog.vue` ha uno slot per il campo data. `getFilter` è nuovo in `filtersService`, per il titolo della pagina;
  - popup "Testo": un contatore per apertura scarta le risposte arrivate dopo la chiusura o dopo un'altra apertura. Un confronto sull'oggetto non bastava, perché è un proxy reattivo;
  - data "fino al": `endOfLocalDayExclusiveUtc` invia l'inizio del giorno locale successivo in UTC. Il conteggio nel dialogo usa `GET /messages`, che include le mail inviate (`IsSent`), mentre il giro le esclude: l'avviso finale usa il numero del server.
- **Fase 15**:
  - backend: 242 test superati, 1 ignorato (SQL reale); build con 0 avvisi; `dotnet format` pulito su core, api e test;
  - FE: lint, format, type-check, build-only, 292 test unitari (35 file) e 4 smoke e2e, tutti a exit 0.
- **Screenshot**: fatti solo pagina e banner (chiaro e scuro, `scratchpad/shots/filtro/`). Quelli del popup, del dialogo con data e della vista mobile sono saltati: l'utente ha fermato il comando. Lo spec temporaneo è stato rimosso.
- README del FE: viste aggiornate (v1.1).
- **Script SQL della migrazione** (`dotnet ef migrations script TrashSender ContentArchive`), controllato: solo aggiunte. Contiene `Run.TrashReceivedBeforeUtc datetime2 NULL`, la tabella `ArchivedMessageContent` con la FK in cascata su `Message` e l'indice unico `IX_ArchivedMessageContent_MessageId`. Nessun `DROP` né `ALTER` su colonne esistenti. **Da applicare con l'ok dell'utente.**
- ⚠️ Divergenza (verifica a freddo, 2026-10-13):
  - `ValidationRules.cs`: `ReceivedBefore` inserito tra il `<summary>` di `AttachmentSubfolder` e il suo metodo, così i commenti XML sono spostati;
  - `FilterMessagesTable.vue` non usa `SortableTable` e spiega le azioni disabilitate solo con `title`; icone lucide da controllare rispetto alla regola delle icone;
  - `MessageTextDialog.vue`: la data è nella descrizione invece che nel titolo, e `@dismiss` è vuoto;
  - `FilterService.GetMessagesAsync`: il `MessageId IN (...)` gira su tutte le mail del filtro, non solo sulla pagina (serve a `MissingArchiveCount`), con rischio del limite di 2100 parametri;
  - test mancanti: validatori nuovi, purga riga+file end-to-end, rimozione di un file già scritto in caso di errore.
- **Correzioni FE dopo la verifica**:
  - `FilterMessagesTable` è ora su `SortableTable`; il motivo delle azioni disabilitate è un testo visibile collegato con `aria-describedby`;
  - popup: titolo "oggetto · data"; chiudere l'avviso di errore chiude il dialog;
  - icone lucide confermate come set del progetto (33 file, compresi i componenti condivisi);
  - 295 test, controlli a exit 0.
  - **Effetto collaterale**: `SortableTable` mette tutte le celle in `td`, quindi l'oggetto non è più `th scope="row"`. Recuperarlo vuol dire modificare il componente condiviso: rimandato.
- **Correzioni BE dopo la verifica**:
  - `ArchiveProvider.GetArchivedMessageIdsBySenderAsync` (join per mittente, nessuna lista di id) sostituisce `GetArchivedMessageIdsAsync`; il service interseca in memoria;
  - summary di `ValidationRules` sistemati;
  - test nuovi: `Validators/ValidatorTests.cs`, `Infrastructure/TrashedPurgeFlowTests.cs` (flusso riga+file, file mancante), test del nuovo metodo, `ContentArchiveProcess` con errore DB dopo la scrittura → file rimosso;
  - 265 test superati e 1 ignorato; build con 0 avvisi; format pulito.
  - **Limite**: la `ExecuteDeleteAsync` della purga non gira su InMemory, quindi il flusso è testato con gli stessi componenti e la DELETE vera su SQL Server resta non coperta.
- **Seconda verifica a freddo (2026-10-13)**: tutti i requisiti corrispondono; BE build 0 avvisi, 265 test superati + 1 ignorato, format pulito; FE lint, format, type-check, 295 test, build a exit 0. Le divergenze annotate sopra sono risolte.
- **Migrazione e deploy (2026-10-13, ok dell'utente)**:
  - API fermata, `dotnet ef database update` applicato (`ContentArchive` presente nell'elenco), API riavviata;
  - poi `docker compose up -d --build` nei due repo. La build parte dalla cartella di lavoro, **senza commit**;
  - controlli: health 200, i due endpoint nuovi presenti in OpenAPI, la vista `FilterMessagesView` servita dal container FE.
