# Piano: completamento FE — filtri, esecuzioni, statistiche per mittente
Brief: [20261009-01.md](../../../briefs/20261009-01.md)
Data: 2026-10-09
Stato: COMPLETATO

## Obiettivo
Chiudere il frontend: CRUD filtri, cronologia esecuzioni e statistiche per casella con, per ogni mittente, numero di mail, data e oggetto della più recente e della più vecchia.

## Contesto
- Warroom 2026-10-09 (ARCH, BE, UI, UX, DBADMIN) sul brief: filtri ed esecuzioni sono già coperti da API esistenti e dalle Fasi 11–12 del piano `2026-10-08-dr-mailroom-fe`; le statistiche richieste **non** sono coperte né dall'API (`/stats/senders` restituisce solo `MailCount`) né dalla Fase 13 pianificata.
- Decisioni dell'utente (2026-10-09):
  - **Nuovo endpoint** per le statistiche, `/stats/senders` resta invariato
  - **"Crea filtro da questo mittente"** come azione di riga nelle statistiche
  - **Checkpoint C1 subito**
  - Ordine dei piani lasciato all'agente (vedi "Ordine")
  - Heatmap: l'utente non la conosceva → proposta: **rimandata** (fuori da questo piano), confermare in approvazione
- Decisioni dell'utente (2026-10-10, dopo `/dr-tech`):
  - Aggregato **per indirizzo email** (mai per nome), totale assoluto con filtro **anno** facoltativo: parametro `year` su `overview` + `GET /stats/years` (nessuna vista nuova, nessuna migration)
  - Pannello di dettaglio del mittente con il **conteggio per anno** (stesso `GET /stats/years` con `senderAddress` esatto)
  - Le viste `vw_SenderStats` (2.440 righe) e `vw_SenderMonthlyStats` (11.737 righe, 1,09 MB) **non** si caricano dal FE
  - CORS per `http://localhost:5173` **solo in sviluppo**: nuova opzione `Mailroom:Frontend:ExtraCorsOrigins`, vuota di default, valorizzata solo nel `.env` locale
- `Message.Subject` ha lunghezza massima (`ColumnLengths.Subject`): un indice con `INCLUDE (Subject)` è possibile.
- Nel backend non esiste ancora un modello di paginazione: questo piano lo introduce per il solo nuovo endpoint.

- Date 1970 (2026-10-10, diagnosi del subagente tech): 10 mail su 38.439 salvate con `ReceivedAtUtc` = 1970-01-01, causate da `GmailSession.cs:119` (`InternalDate ?? 0`); in Gmail web almeno una risulta del 20/12/2021 11:48. Decisioni dell'utente: **rileggere da Gmail tutte e 10** (diagnosi sul valore grezzo), **correggere il codice** (perimetro esteso all'ingestione, Fase 5b), **bonifica = rilettura e correzione**. Fase 4 in parallelo (subagente).

## Ordine
1. Checkpoint C1 (richiesto subito dall'utente) sulla vista Caselle così com'è nel working tree.
2. Chiusura del piano `2026-10-09-scheda-avanzamento-backfill` (Fasi 9–10): ha una migration non committata, va chiusa **prima** di aprirne un'altra.
3. FE Fasi 11 e 12 (non dipendono dal backend).
4. Backend statistiche (nuovo endpoint + eventuale indice), poi `gen:api`.
5. FE Fase 13 riscritta sul nuovo contratto.

## Scope
### Piani da aggiornare (`.ai/`)
- [x] `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` — Fase 11 (precompilazione del costruttore da query `?sender=`), Fase 13 riscritta (tabella principale + classifica a barre, heatmap rimandata), Scope con i nuovi file

### File da modificare — backend (`dr-mailroom`)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Filters/SenderOverviewFilter.cs` (nuovo) — `MailboxId` obbligatorio, `Search`, `SortBy` (`count|first|last|sender`), `SortDir`, `Page`, `PageSize`
- [x] `src/dr-mailroom-api/Validators/SenderOverviewFilterValidator.cs` (nuovo) — `MailboxId > 0`, `PageSize` 1–100, `Page ≥ 1`, `SortBy`/`SortDir` in elenco chiuso, `Search` ≤ lunghezza indirizzo
- [x] `src/dr-mailroom-api/Dto/SenderOverviewDto.cs`, `PagedResult.cs` (nuovi) — `SenderAddress`, `SenderName`, `MailCount`, `FirstReceivedAtUtc`, `FirstSubject`, `LastReceivedAtUtc`, `LastSubject`; `Items` + `TotalCount` + `Page` + `PageSize`
- [x] `src/dr-mailroom-api/Services/StatsService.cs` — `GetSenderOverviewAsync`: una sola query set-based (raggruppamento + prima/ultima mail con tie-break su `Id`), paginata e ordinata in SQL; date con `DateTimeKind.Utc`
- [x] `src/dr-mailroom-api/Endpoints/StatsMapping.cs` — `GET /api/v1/stats/senders/overview` (con `year` facoltativo) e `GET /api/v1/stats/years` (`mailboxId` obbligatorio, `senderAddress` facoltativo **esatto**), 404 se la casella non esiste, `RequireAuthorization`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Filters/YearStatFilter.cs`, `src/dr-mailroom-api/Validators/YearStatFilterValidator.cs`, `src/dr-mailroom-api/Dto/YearStatDto.cs` (nuovi) — `Year`, `MailCount`
- [x] `src/dr-mailroom-core/Configuration/FrontendOptions.cs`, `src/dr-mailroom-api/Program.cs` — `ExtraCorsOrigins` (string[], default vuoto) aggiunte alla policy CORS
- [x] `.env.example` — voce commentata `Mailroom__Frontend__ExtraCorsOrigins__0=http://localhost:5173` (solo sviluppo); `.env` locale valorizzato (non committato)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Configurations/MessageConfiguration.cs` + `Migrations/*` — indice `(MailboxId, SenderAddress, ReceivedAtUtc) INCLUDE (Subject)` **solo se** la misura della Fase 7 lo giustifica
- [x] `test/dr-mailroom-tests/**` — validator, servizio (prima/ultima, pari merito, paginazione, ordinamenti, ricerca), traduzione SQL Server (`ToQueryString`), endpoint (400/404/200)
- [x] `docs/endpoint*.md`, `docs/architettura.md` — nuovo endpoint

### File da modificare — backend, Fase 5b (date 1970)
- [x] `src/dr-mailroom-core/Gmail/GmailSession.cs` — header `Date` tra i `MetadataHeaders`; data ricevuta risolta senza ripiego su 0
- [x] `src/dr-mailroom-core/Gmail/ReceivedDateResolver.cs`, `ReceivedDateSource.cs` (nuovi) — funzione pura: `internalDate > 0` → header `Date` valido → ora di ingestione
- [x] `src/dr-mailroom-core/Gmail/GmailMessageMetadata.cs` — `ReceivedDateSource`, `RawInternalDate` (diagnostica)
- [x] `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs` — riparazione a inizio giro delle mail con data < 2000-01-01 (max 50 per giro), log con valore grezzo
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/MessageProvider.cs` (o equivalente) — lettura delle mail senza data valida e aggiornamento di `ReceivedAtUtc`
- [x] `test/dr-mailroom-tests/**` — resolver (null, 0, negativo, header valido/malformato/assente), riparazione nel servizio di ingestione, `FakeGmail`

### File da modificare — frontend (`dr-mailroom-fe`)
- [x] File elencati nelle Fasi 11, 12 e 13 del piano `2026-10-08-dr-mailroom-fe` (aggiornate in Fase 2 di questo piano)
- [x] `src/services/api/schema.d.ts` — rigenerato con `npm run gen:api`
- Nota di consuntivo: `PagedResult` è diventato `SenderOverviewPageDto`; `SenderOverviewPageDto` e `YearStatDto` stanno in `Dto/SenderOverviewDto.cs`; `SortBy`/`SortDir` sono gli enum `SenderOverviewSortBy` (`MailCount|LastReceivedAt|FirstReceivedAt|SenderAddress`) e `SortDirection` (`Asc|Desc`) in `dr-mailroom-core/Enums`; la query è in `StatsProvider` (due query: conteggio + pagina), non nel servizio; date restituite come lette dal DB (UTC senza designatore, gestito dal FE con `parseUtc`); anche `ValidationRules.cs` (`RequiredMailboxId`, `OptionalYear`) e `SenderOverview.cs` (modelli core)

### Perimetro negativo
- Non toccherò: `/stats/senders` e `/stats/senders/monthly` (contratto invariato), viste `vw_SenderStats`/`vw_SenderMonthlyStats`, scheduler, processi; ingestione **solo** per la Fase 5b
- Nessuna heatmap mittente × mese (rimandata)
- Il brief `briefs/20261009-01.md` non si modifica
- Nessun commit né push senza richiesta esplicita

## Fasi

### Fase 1: Checkpoint C1 — revisione visiva della vista Caselle
- **Stato**: [x] — 2026-10-10 OK dell'utente; screenshot dal container `mailroom.spectre.local` (il dev server 5173 è bloccato dal CORS dell'API in container)
- **Precondizione**: piano approvato
- **File**: nessuno (aggiorna solo lo stato di C1 nel piano FE)
- **Operazione**: —
- **Azione**: avvia API (5180) e FE (`npm run dev`, 5173); mostra vista Caselle in light e dark, banner, stato vuoto, icona (16/32/180 px), font Saira; raccogli il giudizio dell'utente
- **Tool ammessi**: dotnet, npm, playwright (screenshot)
- **Verifica passo**: OK esplicito dell'utente; correzioni richieste → nuova fase nel piano FE prima della Fase 11
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: aggiornamento del piano FE
- **Stato**: [x] — piano FE v1.3: C1 `[x]`, Fase 11 con `?sender=`, Fase 13 riscritta su `/stats/senders/overview`, heatmap rimandata
- **Precondizione**: Fase 1 completata
- **File**: `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md`
- **Operazione**: EDIT
- **Azione**: Fase 11 + precompilazione del costruttore da `/filters?sender=<indirizzo>`; Fase 13 riscritta: selettore casella, tabella paginata lato server (ricerca, ordina per conteggio/prima/ultima/mittente, oggetti come testo troncato con `title`, `<time datetime>`, `aria-live` al cambio casella, reset di pagina e ordinamento), classifica a barre dei primi 20 come sintesi collegata alla tabella, azione di riga "Crea filtro da questo mittente"; heatmap rimossa
- **Tool ammessi**: nessuno
- **Verifica passo**: rilettura del piano FE: Fase 13 non cita più la heatmap né `/stats/senders/monthly`; Fase 13 dipende dalla Fase 6 di questo piano
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: chiusura del piano scheda-avanzamento-backfill
- **Stato**: [x] — piano backfill già `COMPLETATO` (Fasi 9–10 e criteri `[x]`, screenshot durante il giro 27.000 → 27.500). Il 2026-10-10 gate FE/BE rieseguiti tutti verdi e container FE ricreato (`bde4b5e51933`, rollback `dr-mailroom-fe:pre-backfill-close`): backfill concluso, 38.439 indicizzate
- **Precondizione**: Fase 2 completata
- **File**: quelli delle Fasi 9–10 di `2026-10-09-scheda-avanzamento-backfill`
- **Operazione**: secondo quel piano
- **Azione**: esegui le Fasi 9 e 10 di quel piano e i suoi criteri finali; `Stato: COMPLETATO`
- **Tool ammessi**: quelli di quel piano
- **Verifica passo**: piano backfill `COMPLETATO`, tutti i criteri `[x]`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 3b: CORS di sviluppo
- **Stato**: [x] — `FrontendOptions.ExtraCorsOrigins` + `WithOrigins([BaseUrl, ..extra])`; `CorsTests` 3/3; `.env.example` con voce commentata, `.env` locale (ignorato da git) con `localhost:5173`; container API ricreato (rollback `dr-mailroom-api:pre-cors`); preflight: 5173 e `mailroom.spectre.local` ammessi, `evil.local` rifiutato
- **Precondizione**: Fase 3 completata
- **File**: `FrontendOptions.cs`, `Program.cs`, `.env.example`, `.env` (locale), test
- **Operazione**: EDIT
- **Azione**: `ExtraCorsOrigins` (string[], default vuoto) aggiunte a `WithOrigins` insieme a `BaseUrl`; `BaseUrl` resta l'unica origine del redirect OAuth; valore `http://localhost:5173` solo nel `.env` locale; rebuild del container API (immagine precedente etichettata `pre-cors` per il rollback)
- **Tool ammessi**: dotnet, docker
- **Verifica passo**: preflight `OPTIONS` con `Origin: http://localhost:5173` → `Access-Control-Allow-Origin` presente; con `Origin: http://evil.local` → assente; `mailroom.spectre.local` continua a funzionare; senza voce nel `.env` il comportamento è quello di oggi (test)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3b: <cosa>` in plan.md, non procedere

### Fase 4: FE Fase 11 — Filtri
- **Stato**: [x] — subagente; gate FE riverificati (type-check, lint, format, build exit 0; vitest 140/140); `?sender=` testato; fuori lista: `problem.ts` (201 = successo, prima la creazione falliva). Contratto `/runs?runIds=…&skippedMailboxIds=…` passato alla Fase 12 del piano FE. Regex (decisione delegata all'agente, 2026-10-10): validazione **solo lato server** (motore .NET, `ValidationRules.cs`), rimossa `isCompilableRegex` dal FE, 400 mostrato accanto al campo; gate FE exit 0, vitest 139/139
- **Precondizione**: Fase 3b completata
- **File**: secondo la Fase 11 del piano FE
- **Operazione**: CREATE
- **Azione**: esegui la Fase 11 del piano FE (incluso `?sender=`)
- **Tool ammessi**: npm
- **Verifica passo**: verifica della Fase 11 del piano FE + test della precompilazione da query
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

### Fase 5: FE Fase 12 — Esecuzioni
- **Stato**: [x] — 2026-10-10. Fase 12 del piano FE completata (dettagli e note di attuazione lì); container FE ricreato, rollback `dr-mailroom-fe:pre-runs`
- **Precondizione**: Fase 4 completata
- **File**: secondo la Fase 12 del piano FE
- **Operazione**: CREATE
- **Azione**: esegui la Fase 12 del piano FE
- **Tool ammessi**: npm
- **Verifica passo**: verifica della Fase 12 del piano FE
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 5b: date 1970 — correzione e rilettura da Gmail
- **Stato**: [x] — 2026-10-10. `ReceivedDateResolver` (internalDate > 0 → header `Date` → ora di lettura) + `ReceivedDateSource`, header `Date` richiesto, warning strutturato; riparazione a inizio giro delle mail con data **= 1970-01-01** (non `< 2000`: eviterebbe di rileggere a ogni giro mail autentiche anteriori al 2000). Diagnosi: Gmail restituisce davvero `internalDate = 0` per le 10 mail (spam con header `Date` falsati, es. `__smptDate`); la data vera è nell'id Gmail (`id >> 20`), ma per **decisione dell'utente** non si usa: le 10 restano datate come lette (9 all'ora di lettura 2026-10-10, 1 dall'header), l'utente le elimina da Gmail; nessuna bonifica del pregresso. Bug trovato e corretto: query di riparazione senza `AsTracking()` (contesto NoTracking) → le date non si salvavano e venivano rilette a ogni giro; `InMemoryMailroomDb` ora NoTracking come in produzione (ha fatto emergere anche un test di `RunOrchestratorTests` che non salvava la pausa, corretto). Test 131/131, format 0, container API ricreato (rollback `dr-mailroom-api:pre-date-fix`), giro 25 `Completed`, `stats/senders/monthly` → 0 righe con `YearMonth < 200001`
- **Precondizione**: Fase 3b completata (in parallelo con la Fase 4)
- **File**: vedi "File da modificare — backend, Fase 5b"
- **Operazione**: CREATE / EDIT
- **Azione**: (1) `ReceivedDateResolver` puro + `Date` nei `MetadataHeaders`, log warning strutturato con `GmailMessageId` quando la fonte non è `internalDate`; (2) a inizio di ogni giro la casella rilegge da Gmail le mail con `ReceivedAtUtc < 2000-01-01` (max 50), aggiorna la data (YearMonth si ricalcola: colonna persistita) e scrive per ognuna un log con `GmailMessageId`, `internalDate` grezzo, header `Date` e fonte scelta (= diagnosi richiesta dall'utente); (3) test; (4) rebuild del container API (rollback `dr-mailroom-api:pre-date-fix`), avvio di un giro, lettura dei log
- **Tool ammessi**: dotnet, docker, curl
- **Verifica passo**: build 0 avvisi, test verdi, `dotnet format` exit 0; nei log 10 righe di riparazione con il valore grezzo; `stats/senders/monthly?mailboxId=1` → 0 righe con `YearMonth < 200001`; esito della diagnosi (internalDate presente/assente) annotato qui
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5b: <cosa>` in plan.md, non procedere

### Fase 6: BE — endpoint `GET /api/v1/stats/senders/overview` e `GET /api/v1/stats/years`
- **Stato**: [x] — 2026-10-10. `GET /stats/senders/overview` (paginato, `sortBy` MailCount|LastReceivedAt|FirstReceivedAt|SenderAddress, `sortDir`, ricerca solo sull'indirizzo, `year`; 200 con `totalCount 0` se vuoto, 404 casella inesistente) e `GET /stats/years` (`senderAddress` esatto e normalizzato in minuscolo; 200 con lista vuota). Note di attuazione: (1) filtro anno come intervallo su `ReceivedAtUtc` invece di `YearMonth BETWEEN` — stessa semantica, testabile su InMemory (dove la colonna calcolata vale 0) e coperto dall'indice della Fase 7; (2) anno ammesso da **1970** (non 2000) all'anno corrente + 1, così ogni anno restituito da `stats/years` resta interrogabile; (3) ricerca solo sull'indirizzo (decisione utente: mai per nome); (4) `BuildSenderOverviewPageQuery` pubblico per il test `ToQueryString`. Test +20 (151 superati, 1 ignorato), build 0 avvisi, format 0; container API ricreato (rollback `dr-mailroom-api:pre-overview`); verifica dal vivo: 2.440 mittenti, anni 2014–2026, 400 su `sortBy` non valido
- **Precondizione**: Fase 5b completata (date valide prima del filtro per anno)
- **File**: `SenderOverviewFilter.cs`, `SenderOverviewFilterValidator.cs`, `SenderOverviewDto.cs`, `PagedResult.cs`, `YearStatFilter.cs`, `YearStatFilterValidator.cs`, `YearStatDto.cs`, `StatsService.cs`, `StatsMapping.cs`, test
- **Operazione**: CREATE / EDIT
- **Azione**: filtro + validator esplicito (`year` facoltativo, 2000–anno corrente+1, tradotto in `YearMonth BETWEEN year*100+1 AND year*100+12`); query unica set-based con prima/ultima mail per mittente (tie-break `Id`), ricerca su indirizzo e nome, ordinamento e paginazione in SQL; 404 se la casella non esiste, lista vuota → 200 con `TotalCount = 0`; `stats/years`: `GROUP BY YearMonth / 100` su `Message`, `senderAddress` confrontato per uguaglianza (minuscolo), ordinato per anno decrescente; rebuild del container API (immagine precedente etichettata `pre-overview`); logging strutturato
- **Tool ammessi**: dotnet
- **Verifica passo**: `dotnet build` 0 avvisi; test verdi, compreso `ToQueryString` (nessuna valutazione lato client)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

### Fase 7: misura e indice
- **Stato**: [x] — 2026-10-10. Misura per differenza (media di 5 chiamate HTTP meno la chiamata di base 404 ≈ 225 ms di trasporto): panoramica ≈ 270 ms netti (> 300 ms con la ricerca) → indice `IX_Message_MailboxId_SenderAddress_ReceivedAtUtc` INCLUDE (`Subject`), migration `20261010184202_SenderOverviewIndex` applicata con `dotnet ef database update` (non serve rebuild del container). Dopo: panoramica ≈ 50–75 ms netti, anno ≈ 45 ms, `years` ≈ 25 ms. **Limite noto**: la ricerca (`LIKE '%…%'`, non sargable) resta ≈ 400 ms netti; accettata (il FE la invia con debounce). Ordine: la Fase 9 è eseguita prima della 8 su richiesta dell'utente (rilascio rapido delle statistiche); la 13 del piano FE dipende dal contratto, già disponibile
- **Precondizione**: Fase 6 completata
- **File**: `MessageConfiguration.cs`, `Migrations/*` (solo se serve)
- **Operazione**: EDIT / CREATE condizionale
- **Azione**: misura tempo e piano di esecuzione della query sulla casella più grande (~48.000 mail); se > 300 ms o con scansione completa ripetuta per mittente → `dotnet ef migrations add SenderReceivedIndex` con `(MailboxId, SenderAddress, ReceivedAtUtc) INCLUDE (Subject)`, poi rimisura
- **Tool ammessi**: dotnet, dotnet-ef, sqlcmd
- **Verifica passo**: tempi prima/dopo annotati qui; migration (se creata) contiene solo `CreateIndex`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere

### Fase 8: documentazione BE
- **Stato**: [x] — 2026-10-10 (dopo la Fase 9, su richiesta dell'utente). `/dr-professor`: `docs/endpoint-stats.md` v1.1 (parametri, valori predefiniti, risposte, 200 vuoto/400/404, calcolo, indice, misure e limite della ricerca), `docs/frontend-specifiche.md` v1.3 (Vista 4 riscritta sulla vista implementata, Vista 3 e mappa degli endpoint allineate), `docs/architettura.md` v1.7 (riga T5). Rilettura contro `StatsMapping`, `StatsService`, validatori e provider: corrispondono
- **Precondizione**: Fase 7 completata
- **File**: `docs/endpoint*.md`, `docs/architettura.md`
- **Operazione**: EDIT
- **Azione**: `/dr-professor` per descrivere il nuovo endpoint (parametri, ordinamenti, paginazione, 404)
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: rilettura: parametri e codici di risposta corrispondono al codice
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere

### Fase 9: FE Fase 13 — Statistiche
- **Stato**: [x] — 2026-10-10. Fase 13 del piano FE completata (dettagli lì); container FE ricreato, rollback `dr-mailroom-fe:pre-stats`
- **Precondizione**: Fasi 5 e 8 completate
- **File**: secondo la Fase 13 riscritta del piano FE + `schema.d.ts`
- **Operazione**: CREATE
- **Azione**: `npm run gen:api`; esegui la Fase 13 riscritta del piano FE
- **Tool ammessi**: npm, playwright (screenshot)
- **Verifica passo**: verifica della Fase 13 del piano FE; "Crea filtro" apre `/filters?sender=…` con il mittente precompilato
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [x] Checkpoint C1 approvato dall'utente
- [x] Piano `scheda-avanzamento-backfill` `COMPLETATO` prima della nuova migration
- [x] `GET /api/v1/stats/senders/overview?mailboxId=…` restituisce per mittente conteggio, prima e ultima mail (data UTC + oggetto), paginato e ordinabile; 400 su parametri non validi, 404 su casella inesistente
- [x] `overview?year=2025` limita conteggi, prima e ultima mail all'anno; `stats/years?mailboxId=1` elenca gli anni; con `senderAddress` esatto non include indirizzi che lo contengono
- [x] CORS: `localhost:5173` ammesso solo se presente in `ExtraCorsOrigins` (`.env` locale); nessuna origine di sviluppo in file committati
- [x] Nessuna mail con data < 2000-01-01; nuove mail senza `internalDate` datate dall'header `Date` con warning nei log
- [x] `/stats/senders` e `/stats/senders/monthly` invariati
- [x] `dotnet build` 0 avvisi, `dotnet test` exit 0, `dotnet format --verify-no-changes` exit 0
- [x] FE: vista Filtri (CRUD completo), vista Esecuzioni (cronologia), vista Statistiche (tabella + barre) funzionanti; `type-check`, `lint:check`, `format:check`, `test:unit`, `build` exit 0
- [x] Oggetti delle mail resi solo come testo, mai HTML
- [x] Azione "Crea filtro da questo mittente" precompila il costruttore
- [x] Logging strutturato con placeholder nei nuovi log
