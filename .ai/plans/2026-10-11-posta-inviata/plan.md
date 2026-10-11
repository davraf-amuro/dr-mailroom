# Piano: Segnalare la posta inviata
Data: 2026-10-11
Stato: COMPLETATO

## Obiettivo
Riconoscere la posta inviata (label Gmail `SENT` o mittente = indirizzo della casella) e **segnalarla**
nel FE — statistiche e anteprima filtri — senza escluderla da conteggi, classifiche o filtri.

## Decisioni (warroom 2026-10-10 + utente)
- **Segnalare, non escludere**: nessun parametro `includeSent`, nessun interruttore; totali e classifiche invariati.
- **Filtri invariati**: un filtro agisce anche sulla posta inviata (se chiede gli allegati delle mail inviate, li salva).
- **Criterio**: `IsSent = Labels contiene SENT OR SenderAddress = Mailbox.EmailAddress` (copre alias/send-as e mail a se stessi).
- **Pregresso dal DB**, nessuna rilettura da Gmail. `Labels` è `nvarchar(1000)`: gli id delle label sono brevi, la perdita di `SENT` per troncamento è improbabile; la Fase 4 lo misura.
- **Segno mai affidato al solo colore**: badge con testo + icona Lucide `Send`; nel grafico tratteggio (decal) + testo nel tooltip e nella tabella alternativa.
- Servizio API in pausa su Docker durante migration e backfill (dall'utente).

## Scope
### File da modificare — BE (`dr-mailroom`)
- [x] `src/dr-mailroom-core/Gmail/GmailConstants.cs` — costante `SentLabel = "SENT"`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Entities/Message.cs` — proprietà `IsSent`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Configurations/MessageConfiguration.cs` — `bit NOT NULL DEFAULT 0`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Migrations/<ts>_MessageIsSent.cs` (+ Designer, snapshot) — colonna, `UPDATE` di backfill, `IsSent` in INCLUDE dell'indice `IX_Message_MailboxId_SenderAddress_ReceivedAtUtc`
- [x] `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs` — `ToEntity` calcola `IsSent` (labelIds complete, prima del troncamento, + indirizzo casella)
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/SenderOverview.cs` + `StatsProvider.cs` — `SentCount` per mittente
- [x] `src/dr-mailroom-api/Dto/SenderOverviewDto.cs` — `SentCount`
- [x] `src/dr-mailroom-api/Dto/MessageSummaryDto.cs` — `IsSent` (anteprima filtri)
- [x] `test/dr-mailroom-tests/...` — ingestione (SENT, indirizzo casella, nessuno dei due), `SentCount` della panoramica, `ToQueryString` ancora traducibile (+ `MessageSummaryDtoTests.cs`)
- [x] `docs/architettura.md`, `docs/endpoint-stats.md`, `docs/endpoint-filters.md`, `docs/frontend-specifiche.md` — via `/dr-professor`

### File da modificare — FE (`dr-mailroom-fe`)
- [x] `src/services/api/schema.d.ts` — `npm run gen:api`
- [x] `src/composables/stats/statsPresentation.ts` — testi/etichette centralizzati (`Posta inviata`, `N inviate`), helper `sentLabel(row)`
- [x] `src/components/stats/SenderOverviewTable.vue` — badge sotto l'indirizzo
- [x] `src/composables/stats/topSendersOption.ts` + `src/components/stats/TopSendersChart.vue` — barra con decal/colore da token, tooltip "posta inviata", badge nella tabella alternativa
- [x] `src/composables/stats/useStatsView.ts` — frase eroe: se il più attivo è posta inviata → "Il mittente più attivo è la tua posta inviata, X, …"
- [x] `src/components/stats/SenderDetailDialog.vue` — badge nel titolo/descrizione se posta inviata (dato passato dalla riga)
- [x] `src/views/StatsView.vue` — passa l'etichetta al dialog (scope aggiunto 2026-10-11 con consenso utente)
- [x] `src/components/filters/FilterPreviewPanel.vue` — badge per messaggio `isSent`
- [x] `src/**/__tests__/*` — test di presentazione, frase eroe, tabella, anteprima

### Perimetro negativo
- Non toccherò: logica dei filtri e della riapplica, `/stats/years`, `/stats/senders`, `/stats/senders/monthly`, viste `vw_SenderStats`/`vw_SenderMonthlyStats`, lettura Gmail (`GmailSession`), token di design (si usano quelli esistenti), altre viste FE
- Nessun parametro di esclusione, nessun interruttore
- Nessun `git push`

## Fasi

### Fase 1: Costante e entità
- **Stato**: [x] — build 0 errori, 0 avvisi; IsSent con HasDefaultValue(false), IsSent in INCLUDE dell'indice overview
- **Precondizione**: working tree BE pulito (a parte `briefs/`)
- **File**: `GmailConstants.cs`, `Message.cs`, `MessageConfiguration.cs`
- **Operazione**: EDIT
- **Azione**: `SentLabel = "SENT"`; `public bool IsSent { get; set; }` con `[Column("IsSent")]`; default SQL `0`
- **Tool ammessi**: nessuno
- **Verifica passo**: `dotnet build` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Ingestione
- **Stato**: [x] — FetchMessagesAsync/ToEntity ricevono la Mailbox; test Backfill_MarksSentMail_ByLabelOrMailboxAddressAsync (FakeGmailSession con SenderById/LabelsById); 12/12
- **Precondizione**: Fase 1 `[x]`
- **File**: `MailboxIngestionService.cs`
- **Operazione**: EDIT
- **Azione**: `FetchMessagesAsync`/`ToEntity` ricevono l'indirizzo della casella; `IsSent = metadata.LabelIds.Contains(SentLabel) || SenderAddress == indirizzo casella (OrdinalIgnoreCase)`
- **Tool ammessi**: nessuno
- **Verifica passo**: test di ingestione nuovi verdi (3 casi)
- **Su divergenza**: STOP

### Fase 3: Migration con backfill
- **Stato**: [x] — `20261010220452_MessageIsSent`: DropIndex → AddColumn bit DEFAULT 0 → UPDATE (Labels LIKE ,SENT, OR indirizzo casella) → CreateIndex INCLUDE (Subject, IsSent); Down simmetrico; file-scoped; snapshot aggiornato nella sua posizione storica `dr_mailroom_core/Infrastructure/Mailroom/Migrations/`
- **Precondizione**: Fasi 1–2 `[x]`; container API in pausa (conferma utente)
- **File**: nuova migration `MessageIsSent`
- **Operazione**: CREATE
- **Azione**: `dotnet ef migrations add MessageIsSent`; in `Up` dopo `AddColumn`: `UPDATE m SET IsSent = 1 FROM Message m JOIN Mailbox b ON b.Id = m.MailboxId WHERE (',' + m.Labels + ',') LIKE '%,SENT,%' OR m.SenderAddress = LOWER(b.EmailAddress)`; ricrea l'indice overview con INCLUDE (`Subject`, `IsSent`); namespace file-scoped come le altre migration
- **Tool ammessi**: `dotnet ef`
- **Verifica passo**: migration rilette, `Down` simmetrico; build 0
- **Su divergenza**: STOP

### Fase 4: Applicazione e misura
- **Stato**: [x] — 2026-10-11, `docker stop dr-mailroom-api` (consenso utente), `dotnet ef database update` ok. Misura: 38.452 mail, IsSent 1.128 = label SENT 1.128 = stesso indirizzo 1.128 = entrambi 1.128 (criteri coincidenti, nessun alias send-as); Labels troncate 0, lunghezza massima 60; unico mittente inviato d.raffagli@gmail.com
- **Precondizione**: Fase 3 `[x]`, backup/rollback: la colonna è nuova, `Down` la rimuove
- **Operazione**: `dotnet ef database update`
- **Azione**: applica; misura `COUNT` per criterio (solo SENT, solo indirizzo, entrambi) e numero di righe con `LEN(Labels) = 1000`
- **Tool ammessi**: `dotnet ef`, `sqlcmd` (sola lettura dopo l'update)
- **Verifica passo**: conteggi riportati qui; righe troncate = 0 o dichiarate
- **Su divergenza**: STOP

### Fase 5: Statistiche e DTO
- **Stato**: [x] — `SentCount = g.Count(m => m.IsSent)` in SenderAggregate → SenderOverview → SenderOverviewDto; `MessageSummaryDto.IsSent`; test Overview_CountsSentMail_WithoutExcludingItAsync + `sentCount` nel test endpoint; suite 159 superati / 1 ignorato; format exit 0
- **Precondizione**: Fase 4 `[x]`
- **File**: `SenderOverview.cs`, `StatsProvider.cs`, `SenderOverviewDto.cs`, `MessageSummaryDto.cs`
- **Operazione**: EDIT
- **Azione**: `SentCount = g.Count(m => m.IsSent)` nell'aggregato e nel record; `IsSent` nella proiezione `MessageSummaryDto`
- **Tool ammessi**: nessuno
- **Verifica passo**: test panoramica con `SentCount`; `Projection_TranslatesToSqlServer` / `ToQueryString` verdi; suite completa verde; `dotnet format --verify-no-changes` exit 0
- **Su divergenza**: STOP

### Fase 6: FE — contratto e presentazione
- **Stato**: [x] — gen:api (API locale avviata col profilo http e fermata subito, nessun giro); `SENT_MAIL_LABEL`, `hasSentMail`, `isAllSent`, `sentLabel` (Posta inviata | N inviate | null); 3 test nuovi, vue-tsc 0
- **Precondizione**: Fase 5 `[x]`, API locale avviata per `gen:api`
- **File**: `schema.d.ts`, `statsPresentation.ts`
- **Operazione**: EDIT
- **Azione**: `gen:api`; testi centralizzati e helper `sentLabel(row)` → `null` | `Posta inviata` | `N inviate` (mittente misto)
- **Verifica passo**: `type-check` exit 0, test helper verdi
- **Su divergenza**: STOP

### Fase 7: FE — componenti statistiche
- **Stato**: [x] — badge neutral + Send in tabella, tabella alternativa del grafico e dialog (via `detailSentLabel` + StatsView, scope aggiunto); grafico: `aria.decal.show`, barre normali `decal: none`, barre con inviate colore `--c-neutral` + tratteggio + testo "· Posta inviata" accanto al valore (margine destro 170); frase eroe "Il mittente più attivo è la tua posta inviata, …"; test +4; type-check 0, 71 test
- **Precondizione**: Fase 6 `[x]`
- **File**: `SenderOverviewTable.vue`, `topSendersOption.ts`, `TopSendersChart.vue`, `useStatsView.ts`, `SenderDetailDialog.vue`
- **Operazione**: EDIT
- **Azione**: `BaseBadge` tono `neutral` + icona `Send`; barra con `decal` e colore `--c-neutral` letto dai token; tooltip e tabella alternativa con il testo; frase eroe adattata; badge nel dettaglio
- **Verifica passo**: test aggiornati verdi
- **Su divergenza**: STOP

### Fase 8: FE — anteprima filtri
- **Stato**: [x] — badge `Posta inviata` + Send accanto alla data per `isSent`; nuovo `FilterPreviewPanel.spec.ts`; gate FE: type-check, lint:check, format:check, build exit 0, vitest 217/217
- **Precondizione**: Fase 7 `[x]`
- **File**: `FilterPreviewPanel.vue`
- **Operazione**: EDIT
- **Azione**: badge `Posta inviata` per i messaggi con `isSent`
- **Verifica passo**: test verdi
- **Su divergenza**: STOP

### Fase 9: Gate e verifica visiva
- **Stato**: [x] — 2026-10-11. Tag rollback `dr-mailroom-api:pre-sent`, `dr-mailroom-fe:pre-sent`; `docker compose up -d --build` su entrambi. API dal vivo: overview con `sentCount` (d.raffagli@gmail.com 1.128/1.128), preview 200. ⚠️ Divergenze risolte durante la verifica visiva: (1) `itemStyle.decal: 'none'` a livello di serie fa crashare ECharts 6 (`Cannot create property 'dirty' on string 'none'`, grafico vuoto) → decal solo per elemento, senza `aria.decal.show`; (2) su mobile il margine di 170 px schiacciava le barre → parametro `sentText` (false su schermo stretto: tratteggio + colore, testo nella tabella alternativa). Gate FE rieseguiti exit 0, vitest 217/217; screenshot light/dark desktop/mobile di Statistiche e anteprima filtri, nessun errore in console
- **Azione**: FE `type-check`, `lint:check`, `format:check`, `test:unit --run`, `build`; BE test + format; container API e FE ricreati con tag di rollback (`pre-sent`); screenshot light/dark desktop/mobile di Statistiche e anteprima filtri
- **Su divergenza**: STOP

### Fase 10: Documentazione
- **Stato**: [x] — `/dr-professor`: architettura.md v1.9 (D20, T5, tabella Message), endpoint-stats.md v1.2 (`sentCount`, indice), endpoint-filters.md v1.2 (`isSent` nell'anteprima), frontend-specifiche.md v1.4 (sezione "Posta inviata", anteprima). Fuori elenco ma coerente: onboarding.md v1.1 (D1–D20)
- **Azione**: `/dr-professor` su `architettura.md` (tabella `Message`, decisione), `endpoint-stats.md` (`sentCount`), `endpoint-filters.md` (`isSent`), `frontend-specifiche.md` (segno)

## Criteri di verifica finale
- [x] `Message.IsSent` presente, popolato sul pregresso; conteggi per criterio annotati in Fase 4
- [x] Nuove mail indicizzate con `IsSent` corretto (test: SENT, indirizzo casella, nessuno)
- [x] Panoramica espone `sentCount`, anteprima filtri `isSent`; totali e ordinamenti invariati
- [x] FE: badge testuale + icona in tabella, dettaglio e anteprima; grafico con decal + testo; frase eroe adattata
- [x] Filtri e riapplica non modificati
- [x] Gate BE e FE verdi; container ricreati con rollback
- [x] `/dr-verify-plan` superato — 2026-10-11: prima passata con divergenza minore (mancava il controllo `ToQueryString` esplicito per `IsSent`), corretta con `MessageSummaryDtoTests` + asserzione `[IsSent]` nel test della panoramica; riverifica: tutti i file CORRISPONDONO, criteri 1–6 SODDISFATTI, suite 160 superati / 1 ignorato, format 0. Nota: `docs/onboarding.md` aggiornato fuori elenco (solo D1–D20)

## Note
- Piani `IN CORSO` aperti: `2026-10-08-dr-mailroom-fe` (fasi 14+) e `2026-10-10-filtro-oggetto-emoji` (manca la verifica a freddo). Questo piano passa a `IN CORSO` solo su approvazione esplicita dell'utente.
