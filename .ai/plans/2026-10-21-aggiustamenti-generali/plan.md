# Piano: aggiustamenti generali (stats, cestino senza allegati, Apri in Gmail, cronologia giri)
Brief: [briefs/20261021-01.md](../../../briefs/20261021-01.md)
Data: 2026-10-21
Stato: COMPLETATO
Branch: `plan/aggiustamenti-generali` (in `dr-mailroom` e `dr-mailroom-fe`)
Approvazione: esecuzione autonoma autorizzata dall'utente in chat ("solo per questa volta crea il piano ed eseguilo in autonomia"), commit + deploy Docker a fine lavoro.

## Obiettivo
1. `/stats`: per ogni mittente il numero di mail con allegati accanto al totale (`345 (📎49)`); cestinate escluse dai conteggi.
2. `/stats/messages`: oltre a "Elimina tutte", "Elimina senza 📎" (solo mail senza allegati), con conteggio in conferma.
3. Ogni mail della griglia del mittente ha il link "Apri in Gmail". Download e salvataggio del body **rimandati** (decisione utente dopo il warroom).
4. `/runs`: cronologia non caricata all'apertura; l'utente sceglie i filtri e preme "Carica".

## Analisi
- Warroom 2026-10-21: convergenza; l'utente ha tolto il download del body (basta il link Gmail) e accetta il filtro semplice "senza allegati".
- Esclusione cestinate: **già presente** ovunque (`TrashedAtUtc == null` in `StatsProvider.MessagesOf`, `GetYearStatAsync`, viste `vw_Sender*`, `SenderMessageFilter`). DB reale: 38.934 mail, 2.538 cestinate, nessuna con label TRASH/SPAM (le label sono fotografate all'ingestione). Le mail cancellate **direttamente da Gmail** dopo l'indicizzazione restano invece contate: l'ingestione segue solo `messageAdded`. Fuori scope di questo piano → segnalato all'utente.
- `HasAttachments` = payload `multipart/mixed` (`GmailSession.GetMetadataAsync`): approssimazione nota (warroom ARCH), non si cambia qui.
- Il giro di cestino è asincrono (`Run` letto dal job): il flag "solo senza allegati" va salvato sul `Run` → colonna bool additiva `TrashWithoutAttachmentsOnly` (default false) e migrazione EF. Unico cambio di schema.
- Link Gmail: `https://mail.google.com/mail/u/?authuser=<casella>#all/<GmailMessageId>`; serve `GmailMessageId` in `SenderMessageDto` e l'indirizzo della casella nel FE.

## Scope
### dr-mailroom (API)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/StatsProvider.cs`, `SenderOverview.cs`, `src/dr-mailroom-api/Dto/SenderOverviewDto.cs` — `AttachmentCount`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Entities/Run.cs`, `RunProvider.cs`, `MessageProvider.cs`, `Runs/RunOrchestrator.cs`, migrazione nuova + snapshot — flag sul giro
- [x] `src/dr-mailroom-api/Dto/TrashSenderRequest.cs`, `RunDto.cs`, `Services/RunService.cs`, `Endpoints/MessagesMapping.cs` — parametro e descrizioni
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Filters/SenderMessageFilter.cs` — `WithoutAttachmentsOnly` per il conteggio
- [x] `src/dr-mailroom-api/Dto/SenderMessageDto.cs` — `GmailMessageId`
- [x] test esistenti adeguati + `TrashSender_WithoutAttachmentsOnly_KeepsMailWithAttachmentsAsync`
- [x] `Configurations/MessageConfiguration.cs` — `HasAttachments` nell'INCLUDE di `IX_Message_MailboxId_SenderAddress_ReceivedAtUtc` (stessa migrazione `20261021203059_TrashWithoutAttachments`)
- [x] `docs/endpoint-stats.md`, `docs/endpoint-messages.md`, `docs/frontend-specifiche.md`

### dr-mailroom-fe
- [x] `src/services/api/schema.d.ts` (rigenerato o allineato), `messagesService.ts`
- [x] `src/components/stats/SenderOverviewTable.vue` — `345 (📎49)` con testo accessibile
- [x] `src/views/SenderMessagesView.vue`, `src/composables/stats/useTrashSender.ts`, `trashPresentation.ts` — seconda azione
- [x] `src/components/stats/SenderMessagesTable.vue`, `useSenderMessages.ts` — link "Apri in Gmail"
- [x] `src/composables/runs/useRunsView.ts`, `src/stores/runs.ts`, `src/views/RunsView.vue`, `RunHistoryFilters.vue` — "Carica"
- [x] test esistenti adeguati

### Perimetro negativo
- Non toccherò: ingestione Gmail, calcolo di `HasAttachments`, tabella del body, `.env`, file già modificati e non tracciati estranei al piano (`.env.example`, `docs/avvio-e-configurazione.md`, `*.new`, piano key-ring, `.gitignore.new` nel FE).
- Nessuna push, nessun merge su `main`.

## Fasi
### Fase 1: Branch
- **Stato**: [x]
- **Azione**: `git switch -c plan/aggiustamenti-generali` in entrambi i repo (modifiche locali estranee restano non committate)

### Fase 2: API — conteggio allegati
- **Stato**: [x]
- **Verifica passo**: build + test

### Fase 3: API — cestino solo senza allegati (+ migrazione)
- **Stato**: [x]
- **Verifica passo**: build + test; migrazione con sola `AddColumn` bool default false

### Fase 4: API — `GmailMessageId` nella griglia
- **Stato**: [x]

### Fase 5: FE — stats, cestino, Apri in Gmail
- **Stato**: [x]
- **Verifica passo**: `npm run lint`, `type-check`, test unitari

### Fase 6: FE — cronologia su richiesta
- **Stato**: [x]

### Fase 7: Commit e deploy Docker
- **Stato**: [x]
- **Azione**: commit nei due repo (solo i file del piano); `docker compose up -d --build` in entrambi; migrazione applicata all'avvio; verifica `/health` e pagine

## Criteri di completamento
- [x] Panoramica mittenti restituisce `attachmentCount`; FE mostra `N (📎M)`
- [x] "Elimina senza 📎" cestina solo mail con `HasAttachments = false`, conferma con conteggio
- [x] Ogni mail della griglia ha "Apri in Gmail" con `authuser` della casella
- [x] `/runs` all'apertura non chiama `GET /runs` per la cronologia; "Carica" la legge
- [x] Build, test e lint verdi; container aggiornati e sani

## Esito verifica (2026-10-21)
- API: build 0 errori, 269 test superati (1 ignorato, preesistente), `dotnet format --verify-no-changes` pulito sui 3 progetti.
- FE: lint pulito, `format:check` ok, type-check ok, 302 test superati, build ok.
- DB: migrazione `20261021203059_TrashWithoutAttachments` applicata (`dotnet ef database update`).
- Produzione: container `dr-mailroom-api` e `dr-mailroom-fe` ricostruiti; `/health` 200; panoramica casella 1 con `attachmentCount` (es. 1129 mail, 204 con allegati); `WithoutAttachmentsOnly=true` → 925 = 1129 − 204; `gmailMessageId` presente nella griglia.
- Non verificati a mano nel browser: aspetto dei pulsanti e apertura effettiva del link Gmail (`authuser`).

## Punti aperti
- Mail cancellate **direttamente in Gmail** dopo l'indicizzazione restano contate: l'ingestione legge solo `messageAdded`. Servirebbe leggere anche `labelAdded TRASH`/`messageDeleted` dalla history.
- `HasAttachments` = solo `multipart/mixed` (allegati inline non contati).
- Lettura del body in dr-mailroom: rimandata.
