# Piano: Analisi delle mail — griglia delle mail di un mittente dalle statistiche
Brief: [20261010-02.md](../../../briefs/20261010-02.md)
Data: 2026-10-11
Stato: COMPLETATO

> Origine: brief 20261010-02 + tavolo `/dr-warroom` del 2026-10-11.
> **Allegati esclusi per decisione dell'utente** ("lascia perdere gli allegati"): niente apertura diretta, niente popup di scelta, niente endpoint di download. La colonna "Contiene allegati" resta, perché è un campo già indicizzato (`Message.HasAttachments`).

## Obiettivo
Click su una barra del grafico "mittenti più attivi" (o su "Mail" nella riga della tabella mittenti) → vista con la griglia paginata delle mail di quel mittente nella casella: Oggetto, Data ricevimento, Labels, Contiene allegati.

## Decisioni (confermare in approvazione)
- **D1 — Vista dedicata, non dialog**: route `/stats/messages?mailboxId=&sender=`, link condivisibile e tasto Indietro funzionante. La stessa vista ospiterà la selezione per l'eliminazione del brief 20261010-01.
- **D2 — Labels**: le label di sistema Gmail (INBOX, UNREAD, IMPORTANT, STARRED, SENT, CATEGORY_*) si traducono in italiano lato FE con una mappa centralizzata. Le label utente (`Label_<n>`) restano come chip con l'ID grezzo: la traduzione in nome richiede una chiamata Gmail `labels.list` + cache ed è **fuori scope** (piano futuro).
- **D3 — Contiene allegati**: icona Lucide curata (`Paperclip`) + testo per gli screen reader, non "1/0".
- **D4 — Click sulla barra**: oggi fa `focusSender` (cerca il mittente in tabella); dopo il piano apre la vista mail. La ricerca in tabella resta disponibile dal campo di ricerca.
- **D5 — Filtro oggetto**: la griglia ha un campo "cerca nell'oggetto" (contiene) — utile anche al brief 01, esempio 3.

## Convivenza con l'altra sessione agente
Sessione peer `dr-mailroom-7e` attiva sullo stesso repo (probabile brief 20261010-01). Per non disturbarla:
- lavoro in **git worktree** separati (mai `git checkout` nella cartella condivisa): `../dr-mailroom-wt-analisi` (branch `feat/analisi-mail-mittente` da `main`) e `../dr-mailroom-fe-wt-analisi` (stesso nome branch);
- solo file **nuovi**, tranne i punti di aggancio elencati nello Scope (registrazioni DI, mapping, route, StatsView);
- nessuna migrazione EF: l'indice `(MailboxId, SenderAddress, ReceivedAtUtc) INCLUDE (Subject)` esiste già;
- merge su `main` solo a fine piano, dopo aver verificato lo stato dell'altra sessione.

> ⚠️ Verificato all'avvio: l'altra sessione esegue il piano `2026-10-11-posta-inviata` (aggiunge `Message.IsSent`, migrazione). BE: nessun file in comune. FE: in comune `SenderOverviewTable.vue` e `useStatsView.ts` → in Fase 9 modifiche minime, conflitti da risolvere al merge.

## Scope
### File da modificare — backend (`dr-mailroom`, nel worktree)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Filters/SenderMessageFilter.cs` — CREATE: MailboxId, SenderAddress, Search, SortDir, Page, PageSize (+ costanti default/max come `SenderOverviewFilter`)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/MessageProvider.cs` — EDIT: `GetSenderMessagesPageAsync<T>(filter, projection)` → (items, totalCount); ordine `ReceivedAtUtc` + `Id` come tie-break
- [x] `src/dr-mailroom-api/Dto/SenderMessageDto.cs` — CREATE: `SenderMessageDto(Id, Subject, ReceivedAtUtc, Labels: string? (CSV), HasAttachments)` + Projection — la divisione in chip la fa il FE (`splitLabels`)
- [x] `src/dr-mailroom-api/Dto/SenderMessagePageDto.cs` — CREATE: `SenderMessagePageDto(Items, Page, PageSize, TotalCount)` (un record per file)
- [x] `src/dr-mailroom-api/Validators/SenderMessageFilterValidator.cs` — CREATE: mailboxId obbligatorio, senderAddress obbligatorio ≤ `ColumnLengths.EmailAddress`, search ≤ lunghezza Subject, page ≥ 1, pageSize 1–100
- [x] `src/dr-mailroom-api/Services/MessageService.cs` — EDIT: `GetSenderMessagesAsync` (404 se la casella non esiste, nessuna mail → 200 totalCount 0)
- [x] `src/dr-mailroom-api/Endpoints/MessagesMapping.cs` — EDIT: `GET /api/v1/messages` (`GetSenderMessageList`)
- [x] `src/dr-mailroom-api/Services/ServiceRegistrationExtensions.cs` — EDIT: registra il validatore
- [x] `test/dr-mailroom-tests/Infrastructure/MessageProviderPageTests.cs` — CREATE: paginazione, ordine, filtro oggetto, isolamento per casella e mittente
- [x] `test/dr-mailroom-tests/Api/EndpointTests.cs` — EDIT: 400 su input mancanti, 404 casella inesistente, 200 vuoto
- [x] `docs/endpoint-messages.md` — EDIT: documenta il nuovo endpoint

### File da modificare — frontend (`dr-mailroom-fe`, nel worktree)
- [x] `src/services/api/schema.d.ts` — rigenerato con `npm run gen:api` (API in esecuzione dal worktree)
- [x] `src/services/messagesService.ts` — EDIT: `listSenderMessages(query)`
- [x] `src/utils/gmailLabels.ts` — CREATE: mappa label di sistema → etichetta italiana + `labelText(id)`
- [x] `src/utils/__tests__/gmailLabels.spec.ts` — CREATE
- [x] `src/composables/stats/useSenderMessages.ts` — CREATE: stato da query string (mailboxId, sender, page, sort, search), caricamento, stati loading/empty/error
- [x] `src/components/stats/SenderMessagesTable.vue` — CREATE: colonne Oggetto, Data ricevimento (ora locale), Labels (chip `BaseBadge`), Allegati (icona + sr-only); ordinamento per data
- [x] `src/components/stats/__tests__/SenderMessagesTable.spec.ts` — CREATE
- [x] `src/views/SenderMessagesView.vue` — CREATE: intestazione con mittente e conteggio, ricerca oggetto, tabella, `PaginationNav`, link "Torna alle statistiche"
- [x] `src/router/routes/stats.ts` — EDIT: route `stats-messages` → `/stats/messages` (fuori dal menu, che è una lista esplicita in `AppSidebar.vue`)
- [x] `src/composables/stats/useStatsView.ts` — EDIT: `openMessages(senderAddress)` → push della route con la casella corrente
- [x] `src/components/stats/SenderOverviewTable.vue` — EDIT: azione "Mail" nella riga (emit `messages`)
- [x] `src/views/StatsView.vue` — EDIT: barra del grafico → `openMessages`; testo d'aiuto aggiornato; aggancio dell'emit `messages`

### Perimetro negativo
- Non toccherò: allegati (`ExtractedAttachment`, `AttachmentDto`, endpoint `/attachments`), eliminazione mail (brief 20261010-01), traduzione delle label utente via Gmail, migrazioni EF / schema DB, `SenderDetailDialog`, piani di altre sessioni in `.ai/plans/`, i file in `briefs/`, la cartella di lavoro condivisa su `main` (nessun checkout lì)

## Fasi

### Fase 1: Worktree
- **Stato**: [x] — due worktree creati, cartelle principali rimaste su main
- **Precondizione**: `main` pulito in entrambi i repo, a parte i brief non tracciati; i branch `feat/analisi-mail-mittente` non esistono
- **File**: nessuno (git)
- **Operazione**: CREATE
- **Azione**: `git worktree add ../dr-mailroom-wt-analisi -b feat/analisi-mail-mittente main` in `dr-mailroom`; la stessa cosa in `dr-mailroom-fe` → `../dr-mailroom-fe-wt-analisi`
- **Tool ammessi**: git
- **Verifica passo**: `git worktree list` mostra i due worktree; la cartella principale resta su `main`
- **Su divergenza**: STOP

### Fase 2: Filtro + provider BE
- **Stato**: [x] — build ok
- **Precondizione**: Fase 1
- **File**: `SenderMessageFilter.cs`, `MessageProvider.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: filtro sul modello di `SenderOverviewFilter`; metodo paginato con mittente normalizzato minuscolo (D7), `Subject.Contains(search)`, `Skip/Take`, `CountAsync`, proiezione passata dal chiamante
- **Tool ammessi**: `dotnet build`
- **Verifica passo**: build ok
- **Su divergenza**: STOP

### Fase 3: DTO, validatore, service, endpoint
- **Stato**: [x] — build ok; OpenAPI espone GetSenderMessageList. Nota: Labels resta stringa CSV nel DTO (Projection solo accesso ai membri, regola EF); la divisione la fa il FE
- **Precondizione**: Fase 2
- **File**: `SenderMessageDto.cs`, `SenderMessageFilterValidator.cs`, `MessageService.cs`, `MessagesMapping.cs`, `ServiceRegistrationExtensions.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: Labels resta CSV nel DTO (la Projection EF ammette solo accesso ai membri), divisione lato FE; handler con validazione esplicita come `GetOverviewHandlerAsync`; `MessageService` riceve `MailboxProvider` per il 404
- **Tool ammessi**: `dotnet build`
- **Verifica passo**: build ok; `/openapi/v1.json` contiene `GetSenderMessageList`
- **Su divergenza**: STOP

### Fase 4: Test BE
- **Stato**: [x] — 170 test verdi, 1 ignorato (preesistente)
- **Precondizione**: Fase 3
- **File**: `MessageProviderPageTests.cs`, `EndpointTests.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: casi della sezione Scope
- **Tool ammessi**: `dotnet test`
- **Verifica passo**: tutti i test verdi
- **Su divergenza**: STOP

### Fase 5: Doc endpoint
- **Stato**: [x] — doc v1.1
- **Precondizione**: Fase 4 verde
- **File**: `docs/endpoint-messages.md`
- **Operazione**: EDIT
- **Azione**: parametri, risposte 200/400/404, esempio; footer versione
- **Tool ammessi**: nessuno
- **Verifica passo**: rilettura
- **Su divergenza**: STOP

### Fase 6: Contratto FE
- **Stato**: [x] — API del worktree su :5190 (5180 usata dall'altra sessione), config fittizia via env, DB irraggiungibile e cron 2099 → nessuna scrittura; schema +107 righe; type-check pulito
- **Precondizione**: API del worktree avviata (usata :5190, la :5180 era occupata)
- **File**: `schema.d.ts`, `messagesService.ts`
- **Operazione**: EDIT
- **Azione**: `npm run gen:api`; `listSenderMessages` con `callApi('list', …)`
- **Tool ammessi**: `npm run gen:api`, `npm run type-check`
- **Verifica passo**: type-check pulito
- **Su divergenza**: STOP

### Fase 7: Label Gmail
- **Stato**: [x] — 9 test verdi; corretto un bug trovato dal test (`toString` ereditato da Object)
- **Precondizione**: nessuna
- **File**: `gmailLabels.ts`, `gmailLabels.spec.ts`
- **Operazione**: CREATE
- **Azione**: mappa D2; ID sconosciuto → restituito invariato
- **Tool ammessi**: `npm run test:unit`
- **Verifica passo**: test verdi
- **Su divergenza**: STOP

### Fase 8: Tabella + composable + vista
- **Stato**: [x] — 224 test verdi, type-check/lint/prettier puliti. Aggiunti allo scope: `src/composables/stats/senderMessagesPresentation.ts` (+ spec) — parsing puro della query string e link alla griglia, per non toccare `statsPresentation.ts` (in scope all'altra sessione)
- **Precondizione**: Fasi 6-7
- **File**: `SenderMessagesTable.vue` (+ spec), `useSenderMessages.ts`, `SenderMessagesView.vue`, `routes/stats.ts`
- **Operazione**: CREATE / EDIT
- **Azione**: componenti come da Scope; stato nella query string; caption della tabella; date in ora locale; `Paperclip` + `sr-only` "Contiene allegati"; meta della route con title/overline
- **Tool ammessi**: `npm run test:unit`, `npm run type-check`
- **Verifica passo**: test verdi, type-check pulito
- **Su divergenza**: STOP

### Fase 9: Aggancio dalle statistiche
- **Stato**: [x] — barra e «Mail» → `openMessages` (sostituisce `focusSender`); `SenderOverviewTable.spec.ts` aggiornato per la nuova azione; 224 test verdi, type-check/lint/prettier puliti
- **Precondizione**: Fase 8
- **File**: `useStatsView.ts`, `SenderOverviewTable.vue`, `StatsView.vue`
- **Operazione**: EDIT
- **Azione**: barra → `openMessages`; azione "Mail" nella riga; testo d'aiuto "Seleziona una barra per vedere le mail del mittente"
- **Tool ammessi**: `npm run test:unit`, `npm run lint:check`, `npm run type-check`
- **Verifica passo**: tutto verde; i test esistenti di `SenderOverviewTable.spec.ts` sono aggiornati se rompono
- **Su divergenza**: STOP

### Fase 10: Prova reale
- **Stato**: [x] — 2026-10-11, sui container ripubblicati da `main` (Fase 12; nessun giro in corso, «0 caselle attive»), Playwright su `mailroom.spectre.local`: «Mail» nella riga e barra del grafico → griglia del mittente giusto in casella 1; pagina 2, ordinamento Asc (`aria-sort` ascending), ricerca oggetto (10 risultati), link ricaricato con stato ripristinato, Indietro → `/stats` (pagina/ordine/ricerca usano `replace`), label tradotte («Da leggere», «Posta in arrivo», «Inviata»), light/dark e 390 px. Console: un `TimeoutError` della View Transition comparso 1 volta su 2 solo con resize + screenshot subito dopo la navigazione; 8 navigazioni senza resize pulite → artefatto del test, `useViewTransition.ts` non toccato
- **Precondizione**: Fasi 1-9
- **File**: nessuno
- **Operazione**: —
- **Azione**: API + FE dai worktree; click su una barra → griglia; cambio pagina, ordinamento, ricerca oggetto, Indietro del browser; link condivisibile ricaricato
- **Tool ammessi**: skill `run` / browser
- **Verifica passo**: tutti i passaggi funzionano senza errori in console
- **Su divergenza**: STOP

### Fase 11: Allineamento a main e commit
- **Stato**: [x] — rebase su `main`: BE senza conflitti (173 test verdi, 1 ignorato; `dotnet format` pulito sui 3 csproj); FE conflitti solo negli import di `SenderOverviewTable.vue` e `useStatsView.ts` (uniti `Mails`+`Send`, `isAllSent`/`sentLabel`+`senderMessagesLocation`), Prettier sulla riga unita; `gen:api` dall'API ripubblicata → schema invariato; 232 test, type-check, lint:check, format:check verdi. Fast-forward: BE `27f165d`, FE `61516e9`. Nessun push
- **Precondizione**: Fasi 1-9; sessione `dr-mailroom-7e` non più attiva (verificato 2026-10-11)
- **File**: nessuno (git)
- **Operazione**: EDIT
- **Azione**: commit sul branch nei due worktree; rebase su `main` (BE `87b7249`, FE `10c39db`, commit «posta inviata»); risoluzione conflitti; test/lint ripetuti; merge fast-forward su `main`; nessun push
- **Tool ammessi**: git, `dotnet test`, `dotnet format`, `npm run test:unit|lint:check|type-check`
- **Verifica passo**: test verdi dopo il rebase, `main` contiene il commit
- **Su divergenza**: STOP

### Fase 12: Ripubblicazione container
- **Stato**: [x] — `docker compose up -d --build` di API e FE da `main`; container `Up`, `/openapi/v1.json` 200 con `GetSenderMessageList`
- **Precondizione**: Fase 11 (richiesta utente: «se devi ferma i container e ripubblica»)
- **File**: nessuno
- **Operazione**: —
- **Azione**: rebuild e riavvio di `dr-mailroom-api` e `dr-mailroom-fe` da `main` con i rispettivi `docker-compose.yml`; la prova reale (Fase 10) si fa sui container, così nessuna API di sviluppo chiude i giri `Running`
- **Tool ammessi**: docker compose
- **Verifica passo**: container `Up`, `/status` ok
- **Su divergenza**: STOP

## Criteri di verifica finale
> Piano 2026-10-11-analisi-mail-mittente verificato. Tutti i criteri soddisfatti.

- [x] `GET /api/v1/messages?mailboxId&senderAddress` paginato, validato, 404 su casella inesistente
- [x] `dotnet test` verde; `dotnet format <csproj> --verify-no-changes` pulito
- [x] FE: `test:unit`, `lint:check`, `type-check` verdi
- [x] Click su una barra e "Mail" nella riga aprono la griglia del mittente giusto, nella casella corrente
- [x] Colonne: Oggetto, Data ricevimento, Labels (sistema tradotte, utente grezze), Allegati (icona + testo accessibile)
- [x] Nessuna modifica ad allegati, schema DB o file fuori Scope; la cartella principale del repo è rimasta su `main`
- [x] Verifica a freddo con `/dr-verify-plan` — 2026-10-11, subagente indipendente: tutti i file CORRISPONDONO, criteri SODDISFATTI; unica divergenza testuale (`Labels` CSV vs `string[]` nello Scope) allineata nel piano
