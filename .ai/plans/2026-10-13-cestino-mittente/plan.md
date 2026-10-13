# Piano: "Elimina tutte" le mail di un mittente (cestino Gmail)
Data: 2026-10-13
Stato: COMPLETATO — 2026-10-13, chiuso con l'ok dell'utente all'approvazione del piano 2026-10-13-mail-filtro-contenuto (fasi, migrazione, commit e deploy fatti; esito della prova reale non registrato qui)

## Obiettivo
Dalla vista delle mail di un mittente (`/stats/messages?mailboxId=…&sender=…`), il pulsante "Elimina tutte":
1. chiede conferma;
2. al sì avvia un giro in background che sposta nel cestino di Gmail tutte le mail ricevute da quel mittente nella casella;
3. le mail cestinate spariscono da elenchi e statistiche;
4. un job elimina dal DB le righe cestinate da più di 180 giorni (valore configurabile).

## Decisioni (prese con l'utente il 2026-10-13)
- **D-T1 bis — token separato**:
  - La lettura resta con il solo token `gmail.readonly` (T1 invariata per ingestione, filtri e processi).
  - Per il cestino si chiede un **secondo consenso** con scope `gmail.modify`, una volta per casella, al primo "Elimina tutte". Il token si salva cifrato in una colonna dedicata.
  - Solo il codice del cestino lo usa: il codice di lettura non ha accesso a quel token, quindi non può modificare lo stato letto/non letto.
- **Righe nel DB**:
  - Le mail cestinate restano nel DB con `TrashedAtUtc` valorizzato e sono escluse da statistiche, elenchi, anteprima filtri e riapplica.
  - Un job giornaliero le cancella fisicamente dopo `Mailroom:Retention:TrashedMessagesDays` giorni (default 180).
- **Chiamata Gmail**:
  - `users.messages.batchModify` con `addLabelIds: ["TRASH"]`, a blocchi da 1.000 id (limite documentato). TRASH è "manually applicable" secondo la guida delle label.
  - Se Google rifiuta la chiamata in blocco con un 400, il blocco ripiega su `users.messages.trash`, una mail alla volta.
- **Ambito**: solo le mail **ricevute** dal mittente (`IsSent = false`), già indicizzate e non ancora cestinate. Le mail arrivate dopo l'avvio del giro non sono incluse: si rilancia.
- **Reversibilità**: il cestino di Gmail si svuota da solo dopo 30 giorni; fino ad allora le mail si recuperano da Gmail. dr-mailroom non offre "ripristina" (fuori scope).

## Scope

### Fase 1 — Modello dati e migrazione (core)
- [x] `Mailbox.EncryptedTrashRefreshToken` (`string?`), cifrato come il token di lettura
- [x] `Message.TrashedAtUtc` (`DateTime?`) con indice `(MailboxId, TrashedAtUtc)`
- [x] `Run.SenderAddress` (`string?`): il mittente del giro di cestino
- [x] `RunTrigger.TrashSender`
- [x] Migrazione EF: colonne nuove; `ALTER VIEW` di `SenderStat` e `SenderMonthlyStat` con `WHERE TrashedAtUtc IS NULL`
- [x] **Da verificare prima della migrazione**: chiavi esterne verso `Message` (`ProcessExecution`, `Alert`, allegati) e comportamento in cancellazione, per la purga

### Fase 2 — OAuth del cestino (core + api)
- [x] `GmailConstants.ModifyScope` (`https://www.googleapis.com/auth/gmail.modify`)
- [x] `IGmailAuthorizer`: URL di consenso e scambio del code parametrizzati sullo scope
- [x] `OAuthStateProtector`: lo state porta lo scopo (`connect` | `trash:{mailboxId}`), resta firmato e a scadenza
- [x] Callback: con scopo `trash`:
  - verifica che l'indirizzo del profilo coincida con la casella;
  - salva `EncryptedTrashRefreshToken`;
  - torna alla vista del mittente.
  - Il flusso `connect` resta invariato.
- [x] Endpoint `GET api/v1/mailboxes/{id}/trash-authorization`: URL di consenso per il cestino

### Fase 3 — Client Gmail del cestino (core)
- [x] Nuovo `IGmailTrasher` / `GmailTrasher`, separato da `GmailReader`:
  - crea la sessione **solo** dal token del cestino;
  - `TrashAsync(ids)` con `batchModify` a blocchi da 1.000, ripiego su `messages.trash` e backoff su 429/5xx, come il lettore;
  - `invalid_grant` → token del cestino azzerato, errore "riautorizza il cestino" (la casella **non** passa a `TokenRevoked`: la lettura funziona ancora).
- [x] Costi quota in `GmailConstants` (`BatchModifyCost` 50, `MessagesTrashCost` 5)

### Fase 4 — Giro di cestino (core + api)
- [x] `POST api/v1/messages/trash-sender` con body `{ mailboxId, senderAddress }` e validatore FluentValidation:
  - 404: casella inesistente;
  - 409: casella non `Active` o con un giro in corso;
  - 428 (precondizione): manca il token del cestino, con `authorizationUrl` nel corpo;
  - 202: `{ runId, messagesToTrash }`.
- [x] `RunOrchestrator`: ramo `TrashSender`, sotto lo stesso lock della casella:
  - legge gli id Gmail da cestinare;
  - a blocchi chiama il trasher e marca `TrashedAtUtc`;
  - `MessagesRead` = mail trovate, `MessagesProcessed` = mail cestinate;
  - rispetta l'annullamento: si ferma tra un blocco e l'altro, e ciò che è già cestinato resta marcato.
- [x] Il giro di cestino **non** aggiorna `LastRunAtUtc` né `LastError` della casella: non è un giro di lettura. Il suo esito sta nel `Run`. `MailboxDto.LastRunMessagesRead` esclude i giri `TrashSender`.

### Fase 5 — Esclusione delle cestinate (core)
- [x] `MessageProvider` (elenco del mittente, anteprima filtri, riapplica, altre query) e `StatsProvider`: filtro `TrashedAtUtc == null`
- [x] `MailboxDto.MessagesIndexed` **conta ancora tutte** le righe: è l'avanzamento della lettura dello storico, confrontato col totale Gmail
- [x] Ingestione: una mail già presente e marcata cestinata non viene reinserita né "riattivata"

### Fase 6 — Purga (api)
- [x] `RetentionOptions` (`TrashedMessagesDays`, default 180, validato > 0; `PurgeCron`, default ogni giorno alle 03:30) in `appsettings.json`
- [x] Job Quartz `PurgeTrashedMessagesJob`: `ExecuteDeleteAsync` sulle righe con `TrashedAtUtc` più vecchio della soglia, dipendenze comprese secondo la verifica della Fase 1; log del conteggio

### Fase 7 — Frontend (`dr-mailroom-fe`)
- [x] `schema.d.ts` rigenerato (`npm run gen:api`)
- [x] Service, composable e store per `trashSender` e `trash-authorization`
- [x] `SenderMessagesView.vue`, pulsante "Elimina tutte" (variante `danger`) → `ConfirmDialog`. Il testo della conferma dice:
  - quante mail, da chi e da quale casella;
  - che vanno nel cestino di Gmail, recuperabili da Gmail per 30 giorni;
  - che spariscono da dr-mailroom.
- [x] Risposta 428 → secondo dialogo che spiega il permesso aggiuntivo ("dr-mailroom potrà spostare mail nel cestino; la lettura resta in sola lettura") → redirect a Google → ritorno alla stessa vista con esito
- [x] Giro avviato → avanzamento con polling sul `Run`; a fine giro l'elenco si aggiorna; esito ed errori a parole
- [x] Vista "Esecuzioni": etichetta del trigger `TrashSender` ("Cestino mittente") con il mittente
- [x] Testi che oggi promettono "non modifica né cancella nulla" (hero delle caselle, schermata vuota, `design.md`): aggiornati in "legge in sola lettura; cestina solo quando lo chiedi tu"

### Fase 8 — Documentazione
- [x] `docs/architettura.md`: T1 aggiornata con D-T1 bis (secondo token, scope `gmail.modify` solo per il cestino), job di purga, trigger `TrashSender`
- [x] README / guida d'uso: come autorizzare il cestino; la verifica dell'app Google con scope `gmail.modify` (scope riservato: in modalità "test" funziona per gli utenti di test)

### Test
- Backend:
  - validatore;
  - service (404/409/428/202);
  - orchestratore con trasher finto: blocchi, annullamento, `invalid_grant`, ripiego;
  - viste e provider che escludono le cestinate;
  - purga;
  - callback OAuth con scopo `trash` e indirizzo diverso → rifiuto;
  - `LastRunMessagesRead` ignora `TrashSender`.
- Frontend:
  - dialogo di conferma;
  - percorso 428;
  - polling del giro;
  - etichetta del trigger.

### Perimetro negativo
- Nessuna cancellazione definitiva in Gmail (`batchDelete`, scope `mail.google.com`), nessun "ripristina".
- Il token e il flusso di lettura restano invariati, così come lo stato letto/non letto.
- Nessuna eliminazione per filtro o per selezione multipla: solo "tutte del mittente".
- Il lavoro non correlato nei repo resta com'è.
- Nessun commit e nessun push senza richiesta.

## Verifica finale
- Backend: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` su tutti i progetti toccati; migrazione applicata su un DB locale.
- FE: `npm run lint:check`, `format:check`, `type-check`, `npx vitest run`, `build-only`; screenshot della vista con conferma e avanzamento.
- **Prova reale, con l'utente**: autorizzazione del cestino su una casella e "Elimina tutte" su un mittente di prova con poche mail. Si controlla in Gmail che le mail siano nel cestino e che lo stato letto/non letto delle altre non sia cambiato.

## Note di esecuzione
- **Fase 1**: chiavi esterne verso `Message` (`Alert`, `ExtractedAttachment`, `ProcessExecution`) tutte `Cascade`, quindi la purga con `ExecuteDeleteAsync` è sicura; i file su disco degli allegati estratti restano. Lo snapshot del modello sta nel percorso storico `src/dr-mailroom-core/dr_mailroom_core/...` ed è aggiornato lì. Gli `ALTER VIEW` vanno applicati con `dotnet ef database update` (comandi separati), come i `CREATE VIEW` iniziali.
- **Variazione rispetto al piano (Fase 4)**: niente risposta 428. `MailboxDto.TrashAuthorized` dice al FE se serve il consenso; il FE chiede l'URL a `GET mailboxes/{id}/trash-authorization`; il `POST` senza autorizzazione risponde 409, come difesa. Stesso schema `ServiceResult` degli altri endpoint.
- Quota e ritentativi estratti da `GmailSession` in `GmailCallExecutor`, riusato dal cestino; il comportamento della lettura è invariato.
- **Difetto trovato dai test e corretto**: un annullamento arrivato mentre Gmail cestina un blocco faceva saltare la marcatura nel DB. Ora marcatura e avanzamento si scrivono con `CancellationToken.None` dopo un blocco riuscito.
- `RunDto.SenderAddress` aggiunto, per l'etichetta nelle esecuzioni.
- Backend verificato: build con 0 avvisi; 206 test superati (31 nuovi); `dotnet format` pulito su core, api e test; script SQL della migrazione generato e controllato. La migrazione **non** è applicata al DB: va lanciata dall'utente.
- **Fase 7**:
  - `schema.d.ts` generato dal documento OpenAPI della nuova API, servito dall'host di test (DB InMemory, nessun job): l'API sulla 5180 non è stata toccata. Il test temporaneo usato per salvarlo è stato rimosso.
  - Il FE controlla `trashAuthorized` prima di avviare; il ritorno da Google usa `useOAuthReturn` con i testi del cestino.
  - **Difetto trovato dai test e corretto**: la lettura immediata del polling e quella dell'avvio chiudevano il giro due volte (doppio avviso, doppia rilettura). Ora la chiusura avviene una volta sola.
  - Testi "non modifica né cancella nulla" aggiornati in `MailboxesHero.vue` e `MailboxesEmptyHangar.vue`. `design.md` (piano storico del FE in `.ai/`) non toccato.
- **Fase 8**: `architettura.md` (D21, T1, job di purga), `setup-google-oauth.md` (scope del cestino), `avvio-e-configurazione.md` (`Mailroom:Retention:*`), `endpoint-messages.md` ed `endpoint-mailboxes.md`; footer aggiornati.
- FE verificato: lint, format, type-check, build, 255 test unitari (17 nuovi) e 4 smoke e2e, tutti a exit 0. Screenshot del flusso con API finta in `scratchpad/shots/trash/`.
- **Da fare con l'utente**: applicare la migrazione (`dotnet ef database update`), aggiungere lo scope `gmail.modify` alla schermata di consenso in Google Cloud, riavviare l'API, fare la prova reale su un mittente con poche mail.
- **Variazione richiesta dall'utente (2026-10-13)**: niente barra di avanzamento né polling del giro nel FE. Dopo la conferma un avviso dice "N mail di X verranno spostate nel cestino di Gmail"; il giro prosegue sul servizio. Tolti `trashOutcomeNotice`, `trashProgressText` e il polling da `useTrashSender`. Il backend aggiorna comunque l'avanzamento sul `Run`, visibile in Esecuzioni. FE ricontrollato: lint, format, type-check, build e 250 test a exit 0.
- Migrazione applicata al DB dall'agente con l'ok dell'utente. Commit sul branch `feat/cestino-mittente` nei due repo, deploy Docker su richiesta dell'utente.
