# Specifica del frontend dr-mailroom-fe

Questo documento descrive il frontend di dr-mailroom: **cosa deve fare**, non come è implementato. Il frontend è un progetto separato e non esiste ancora (D10).

Serve a chi svilupperà la SPA: per ogni vista elenca dati, azioni, endpoint e casi d'errore.

---

## Stack e confini

| Voce | Scelta |
|---|---|
| Tipo | SPA, nessun rendering lato server (niente Blazor né Razor) |
| Tecnologia | Vue 3 + Vite + TypeScript, router e Pinia |
| Repository | `dr-mailroom-fe`, separato dal backend; pacchetto di linee guida `dr-fe` |
| Rilascio | container nginx dedicato, separato da `dr-mailroom-api` |
| Indirizzo locale | `http://localhost:5173` (default di `Mailroom:Frontend:BaseUrl`) |
| Backend | `http://localhost:5180`, solo REST JSON sotto `/api/v1` |
| Client HTTP | generato da `GET /openapi/v1.json`; la documentazione interattiva è su `/scalar/v1` |

Il frontend **non** notifica l'utente delle allerte: quel compito spetta a un software esterno (vedi "Endpoint non usati dal frontend").

---

## Convenzioni dell'API da rispettare

### Autenticazione con chiave API

Ogni chiamata, tranne `status` e `retry-database`, richiede l'header `x-api-key`. Senza chiave l'API risponde **401**.

> ⚠️ **Rischio da conoscere.** In una SPA la chiave finisce nel codice o nella memoria del browser: chiunque usi il frontend può leggerla con gli strumenti di sviluppo. Va bene per un uso personale, su `localhost` o in una LAN fidata. Non esporre frontend e API su Internet con questo schema: servirebbe un'autenticazione per utenti (vedi la matrice "Autenticazione" di `minimal-api-architecture.instructions.md`).

Indicazioni pratiche:
- la chiave si configura a runtime (per esempio con un file `config.json` servito da nginx), non a build time;
- non va salvata in `localStorage`.

### Come interpretare le risposte

| Risposta | Significato | Cosa fa il frontend |
|---|---|---|
| **200** su una lista | dati presenti | mostra la tabella |
| **404** su una lista (`GET /`, `/summary`, statistiche) | **nessun dato** per i filtri scelti, non è un errore | mostra lo stato vuoto ("Nessun filtro", "Nessun giro"…) |
| **404** su una risorsa (`/{id}`) | la risorsa non esiste | messaggio + ritorno all'elenco |
| **400** `ValidationProblem` | `errors` con un elenco di messaggi per campo (chiavi in camelCase) | mostra ogni messaggio **accanto al proprio campo** |
| **409** `ProblemDetails` | conflitto: casella occupata, eliminata o non attiva | mostra `detail` in un avviso, senza perdere i dati del form |
| **202** | operazione avviata in background | mostra i `runIds` e avvia il polling |
| **503** | database non raggiungibile | banner globale (vedi "Banner di stato del servizio") |

### Formati

- Gli **enum viaggiano come stringhe**: `"Active"`, `"Contains"`, `"AttachmentExtraction"`, `"Running"`…
- Le date sono in UTC (`…Utc`). Il frontend le mostra nell'ora locale.
- `YearMonth` nelle statistiche è un intero `yyyyMM` (es. `202610`): il frontend lo mostra come "ottobre 2026".
- Il CORS ammette solo l'origine `Mailroom:Frontend:BaseUrl`. Se cambia l'indirizzo del frontend, va aggiornata quella configurazione.

---

## Banner di stato del servizio

È sempre visibile in testata e non richiede la chiave API.

| Azione | Endpoint |
|---|---|
| Interroga lo stato all'avvio e ogni 60 s | `GET /api/v1/status` → `{ databaseReady, lastError, lastCheckedUtc }` (sempre 200) |
| Pulsante "Riprova connessione" quando `databaseReady = false` | `POST /api/v1/status/retry-database` → 200 pronto, 503 ancora giù |

Con `databaseReady = false` il banner è rosso e riporta `lastError`, in una regione `aria-live="assertive"`.

---

## Vista 1 — Caselle

**Percorso:** `/mailboxes`

### Cosa mostra

Una tabella da `GET /api/v1/mailboxes` (DTO `MailboxDto`):

| Colonna | Campo | Note |
|---|---|---|
| Indirizzo | `emailAddress` | |
| Stato | `status` | badge: `Active` verde, `TokenRevoked` arancione, `Disabled` grigio, `Deleted` grigio barrato |
| Collegata | `isConnected` | |
| Backfill | `backfillCompleted`, `backfillInProgress` | "Completato" / "In corso (riprende dal punto salvato)" / "Da avviare" |
| Ultimo giro | `lastRunAtUtc` | |
| Errore | `lastError` | testo troncato, completo al passaggio del mouse e nel dettaglio |

Filtri sopra la tabella: indirizzo (testo) e stato (select). Sono i parametri di query `emailAddress` e `status`.

Il dettaglio di una casella usa `GET /api/v1/mailboxes/{id}`.

### Collegare una casella Gmail

1. Il pulsante **"Collega casella Gmail"** chiama `GET /api/v1/mailboxes/connect`, che risponde `{ authorizationUrl }`.
2. Il frontend porta il browser sulla pagina di Google: `window.location.assign(authorizationUrl)`.
3. L'utente acconsente alla **sola lettura**. Per un'app personale non verificata Google mostra "App non verificata": si prosegue da *Avanzate → Vai a dr-mailroom*.
4. Google richiama il backend su `GET /api/v1/mailboxes/connect/callback`. Lo chiama il browser, non il frontend.
5. Il backend reindirizza a `{BaseUrl}/mailboxes?oauth=ok` oppure `?oauth=error&reason=…`.

Al ritorno, la vista legge `oauth` e `reason`, mostra un messaggio in una regione `aria-live="polite"` e pulisce la query string.

| `reason` | Messaggio per l'utente |
|---|---|
| (assente, `oauth=ok`) | "Casella collegata. Il primo giro legge tutto lo storico: può richiedere molto tempo." |
| `access_denied` o altro valore di Google | "Consenso non concesso su Google." |
| `invalid_state` | "Collegamento scaduto o non valido: riprova (hai 10 minuti per completarlo)." |
| `mailbox_deleted` | "Questa casella è stata eliminata e non può essere ricollegata." |
| `exchange_failed` | "Google non ha completato il collegamento: riprova. Se si ripete, revoca l'accesso dell'app dall'account Google e ricollega." |

### Token revocato

Con `status = TokenRevoked` la riga mostra il pulsante **"Ricollega"**: rifà lo stesso flusso. Il backend aggiorna il token della casella esistente e la riporta ad `Active`, senza rifare il backfill.

### Eliminare una casella

Il pulsante **"Elimina"** chiede conferma con una finestra modale, poi chiama `DELETE /api/v1/mailboxes/{id}`.

- Testo della conferma: *"La casella non verrà più letta e i suoi filtri non saranno più modificabili. Statistiche e storico restano consultabili. L'operazione non si annulla dal frontend."*
- **204**: la riga passa a `Deleted`.
- **409**: un giro è in corso. Mostra "Attendi la fine del giro in corso".

È un'**eliminazione logica** (D18): la riga resta visibile con lo stato `Deleted` e nessuna azione è disponibile.

---

## Vista 2 — Filtri

**Percorso:** `/filters`

### Elenco

Una tabella da `GET /api/v1/filters` (DTO `FilterDto`). Filtri di ricerca: `name`, `senderAddress`, `mailboxId`, `isEnabled`.

| Colonna | Campo |
|---|---|
| Nome | `name` |
| Mittente | `senderAddress` |
| Casella | `mailboxId` (null → "Tutte") |
| Condizione oggetto | `subjectMatchType` + `subjectValue` (es. "Contiene «fattura»") |
| Processi | `processes` (badge "Allegati", "Allerta") |
| Sottocartella | `attachmentSubfolder` |
| Attivo | `isEnabled` |

Il dettaglio usa `GET /api/v1/filters/{id}`.

### Form di creazione e modifica

Creazione con `POST /api/v1/filters` (201 + filtro creato), modifica con `PUT /api/v1/filters/{id}` (204). Ogni campo ha una `<label>` esplicita e un'area di errore collegata con `aria-describedby`.

| Campo | Controllo | Regole lato server (D15) — il frontend le anticipa |
|---|---|---|
| `name` | testo | obbligatorio, massimo 100 caratteri |
| `senderAddress` | email | obbligatorio, un solo indirizzo, massimo 320; confronto senza distinzione di maiuscole |
| `mailboxId` | select ("Tutte le caselle" = null) | facoltativo; elenco da `GET /api/v1/mailboxes/summary`, escluse le `Deleted` |
| `subjectMatchType` | select: Nessuna condizione / Inizia con (`StartsWith`) / Contiene (`Contains`) / Espressione regolare (`Regex`) | con "Nessuna condizione" il valore va omesso |
| `subjectValue` | testo, visibile solo con una condizione | obbligatorio con un tipo, massimo 500; con `Regex` deve compilare |
| `processes` | checkbox "Estrai allegati" (`AttachmentExtraction`), "Crea allerta" (`Alert`) | almeno uno, nessun duplicato |
| `attachmentSubfolder` | testo, visibile con "Estrai allegati" | obbligatorio con l'estrazione; relativo, senza `..` né caratteri non validi, massimo 200 |
| `isEnabled` | interruttore | default attivo |

Testo d'aiuto sotto `subjectMatchType`: *"Un filtro ha al più una condizione sull'oggetto. Per più condizioni crea più filtri con lo stesso mittente."*

Errori:
- **400**: ogni messaggio di `errors` va accanto al campo con la stessa chiave;
- **409**: "La casella di questo filtro è eliminata: il filtro non è modificabile".

La cancellazione usa `DELETE /api/v1/filters/{id}`, con conferma. La 409 ha lo stesso significato.

### Anteprima (dry-run)

Il pulsante **"Prova sulle mail già lette"** è nel form. Chiama `POST /api/v1/filters/preview` con `senderAddress`, `mailboxId`, `subjectMatchType`, `subjectValue`.

- La risposta `FilterPreviewDto` contiene `totalMatches` e le 50 mail più recenti (`MessageSummaryDto`: mittente, oggetto, data, allegati).
- Non scrive nulla e non chiama Gmail: lavora solo sulle mail già indicizzate.
- Ogni riga con `hasAttachments = true` ha un link **"Allegati salvati"**, che apre `GET /api/v1/messages/{id}/attachments` (404 = nessun allegato salvato).
- Il risultato va in una regione `aria-live="polite"`: "12 mail corrispondono".

### Riapplicare un filtro allo storico

L'azione **"Riapplica"** è sulla riga del filtro. Serve a lavorare le mail **già indicizzate** (D13), per esempio "scarica gli allegati di tutte le fatture passate, senza creare allerte".

1. Una modale propone le checkbox dei soli processi del filtro, tutte selezionate.
2. **"Avvia"** chiama `POST /api/v1/filters/{id}/reapply` con `{ "processes": [...] }`. Il body è facoltativo: senza, valgono tutti i processi.
3. **202** → `RunsStartedDto { runIds, skippedMailboxIds }`: un giro per casella (per un filtro globale, uno per ogni casella attiva). Il frontend apre la vista Esecuzioni sui `runIds`. Se `skippedMailboxIds` non è vuoto, segnala le caselle saltate perché occupate.
4. **409**: tutte le caselle sono occupate, oppure la casella è eliminata. **400**: è stato scelto un processo che il filtro non ha.

La riapplica è idempotente: rilanciarla non duplica né allegati né allerte.

---

## Vista 3 — Esecuzioni

**Percorso:** `/runs`

### Avviare un giro manuale

Il pulsante **"Esegui ora"** ha accanto una select della casella; "Tutte" corrisponde a `mailboxId` null.

- Chiama `POST /api/v1/runs` con `{ "mailboxId": 3 }` oppure `{}`. Il giro parte **subito** (D11).
- Durante la chiamata il pulsante ha `aria-busy="true"` ed è disabilitato.
- **202** → `RunsStartedDto`: i giri compaiono in cima alla cronologia e parte il polling.
- **409** → "La casella ha già un giro in corso" (oppure "Tutte le caselle…"). Non è un errore da segnalare in rosso: è un avviso.
- **400** → casella inesistente.

> ℹ️ **Il primo giro su una casella è lungo** e legge tutto lo storico. In quel giro **i filtri non si applicano**: le mail vengono solo indicizzate per le statistiche (D12). Per lavorare lo storico si usa "Riapplica" dalla vista Filtri. Mostra questo avviso quando un giro parte su una casella con `backfillCompleted = false`.

### Polling

Per ogni giro con `status = Running`:
- chiama `GET /api/v1/runs/{id}` ogni 3–5 secondi;
- si ferma quando lo stato diventa `Completed`, `Failed` o `Skipped`;
- si sospende quando la scheda non è visibile (`document.visibilityState`).

L'avanzamento (`messagesRead`) va in una regione `aria-live="polite"`, aggiornata al massimo ogni 10 s per non saturare gli screen reader.

### Cronologia

Una tabella da `GET /api/v1/runs`, dal più recente. Filtri: `mailboxId`, `trigger`, `status`, `startedAtUtcFrom`, `startedAtUtcTo`.

| Colonna | Campo |
|---|---|
| Avvio / fine | `startedAtUtc`, `endedAtUtc` |
| Casella | `mailboxId` (nome da `GET /api/v1/mailboxes/summary`) |
| Origine | `trigger`: Schedulato / Manuale / Riapplica (con `filterId` e `requestedProcesses`) |
| Stato | `status` |
| Mail lette / lavorate | `messagesRead`, `messagesProcessed` |
| Errori | `errorCount`, `errorSummary` |

`Skipped` con "Casella occupata da un altro giro" è normale: il cron ha trovato la casella già al lavoro.

Per i widget compatti (per esempio "ultimi giri" in home) c'è `GET /api/v1/runs/summary`.

---

## Vista 4 — Statistiche

**Percorso:** `/stats`

Le statistiche contano **tutte** le mail lette, non solo quelle filtrate (D9). Restano disponibili anche per le caselle eliminate.

### Totale per mittente

`GET /api/v1/stats/senders` (filtri: `mailboxId`, `senderAddress`) → `SenderStatDto { mailboxId, senderAddress, mailCount }`, già ordinati dal mittente più attivo.

### Per mittente e mese

`GET /api/v1/stats/senders/monthly` (filtri: `mailboxId`, `senderAddress`, `yearMonthFrom`, `yearMonthTo` in formato `yyyyMM`) → `SenderMonthlyStatDto { mailboxId, senderAddress, yearMonth, mailCount }`.

Requisiti comuni:
- tabelle **ordinabili** su ogni colonna. L'intestazione è un `<button>` con `aria-sort` aggiornato;
- clic su un mittente nella tabella dei totali → apre quella mensile filtrata su quel mittente;
- la select della casella si popola da `GET /api/v1/mailboxes/summary`, incluse le `Deleted`;
- con **404** si mostra "Nessuna mail per i filtri scelti".

---

## Accessibilità

| Requisito | Dove |
|---|---|
| `<label>` esplicita per ogni campo; errori collegati con `aria-describedby` | form dei filtri, filtri di ricerca |
| `aria-live="polite"` per esiti di azioni e avanzamento; `assertive` solo per il DB giù | collegamento casella, anteprima, polling, banner |
| `aria-busy` sui pulsanti che avviano chiamate | "Esegui ora", "Riapplica", "Prova sulle mail già lette" |
| `aria-sort` sulle intestazioni ordinabili | statistiche, cronologia |
| Stato e badge mai affidati al solo colore (testo o icona con testo alternativo) | stati di caselle e giri |
| Modali con focus intrappolato, chiusura con Esc, focus restituito al pulsante d'origine | conferme di eliminazione, riapplica |

---

## Mappa degli endpoint

Sono tutti i path esposti dalla Fase 15 (`/openapi/v1.json`).

| Endpoint | Vista |
|---|---|
| `GET /api/v1/mailboxes` | Caselle (elenco) |
| `GET /api/v1/mailboxes/summary` | Filtri (select casella), Esecuzioni (select e nomi), Statistiche (select) |
| `GET /api/v1/mailboxes/{id}` | Caselle (dettaglio) |
| `DELETE /api/v1/mailboxes/{id}` | Caselle (Elimina) |
| `GET /api/v1/mailboxes/connect` | Caselle (Collega / Ricollega) |
| `GET /api/v1/filters` | Filtri (elenco) |
| `GET /api/v1/filters/summary` | Esecuzioni (nome del filtro nei giri di riapplica) |
| `GET /api/v1/filters/{id}` | Filtri (dettaglio / modifica) |
| `POST /api/v1/filters` | Filtri (crea) |
| `PUT /api/v1/filters/{id}` | Filtri (modifica) |
| `DELETE /api/v1/filters/{id}` | Filtri (cancella) |
| `POST /api/v1/filters/preview` | Filtri (anteprima) |
| `POST /api/v1/filters/{id}/reapply` | Filtri (Riapplica) |
| `POST /api/v1/runs` | Esecuzioni (Esegui ora) |
| `GET /api/v1/runs` | Esecuzioni (cronologia) |
| `GET /api/v1/runs/summary` | Esecuzioni (widget ultimi giri) |
| `GET /api/v1/runs/{id}` | Esecuzioni (polling) |
| `GET /api/v1/stats/senders` | Statistiche (totale) |
| `GET /api/v1/stats/senders/monthly` | Statistiche (mensile) |
| `GET /api/v1/messages/{id}/attachments` | Filtri (anteprima → allegati salvati) |
| `GET /api/v1/status` | Banner di stato |
| `POST /api/v1/status/retry-database` | Banner di stato |

### Endpoint non usati dal frontend

| Endpoint | Chi lo usa |
|---|---|
| `GET /api/v1/alerts`, `GET /api/v1/alerts/summary` | il **software esterno** che avvisa l'utente: legge `?consumed=false` |
| `POST /api/v1/alerts/{id}/consume` | il **software esterno**, dopo aver notificato l'allerta |
| `GET /api/v1/mailboxes/connect/callback` | il **browser**, reindirizzato da Google (anonimo, protetto dallo `state` firmato) |
| `GET /health` | l'**infrastruttura** (healthcheck di container e reverse proxy) |

---

## Fonti

- Piano: `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (D10–D18, C1–C12)
- Codice: `src/dr-mailroom-api/Endpoints/`, `src/dr-mailroom-api/Dto/`, `src/dr-mailroom-api/Validators/`, `src/dr-mailroom-api/Services/MailboxService.cs`
- Architettura: [architettura.md](architettura.md)

*Revisione v1.0 — 2026-10-05 19:18 — claude-opus-5-5*
