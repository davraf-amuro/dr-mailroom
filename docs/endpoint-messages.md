# Endpoint group: Messages

## Introduzione

Il gruppo `Messages` espone le mail indicizzate. Ha tre endpoint:

- la **griglia delle mail di un mittente** in una casella, a pagine. La apre il frontend quando si seleziona un mittente nelle statistiche;
- gli **allegati salvati** di una mail;
- **"Elimina tutte"**: sposta nel cestino di Gmail le mail ricevute da un mittente (D21).

Un allegato si salva quando la mail corrisponde a un filtro con il processo `AttachmentExtraction`. Il file sta sotto `Mailroom:Attachments:RootPath`. L'API restituisce il percorso relativo e l'hash SHA-256, non il contenuto del file.

Tutti gli endpoint richiedono la chiave API nell'header `x-api-key`.

## Architettura

| Componente | File | Responsabilità |
|---|---|---|
| Mapping | `src/dr-mailroom-api/Endpoints/MessagesMapping.cs` | Route e metadati OpenAPI |
| Validatore | `src/dr-mailroom-api/Validators/SenderMessageFilterValidator.cs` | Controlla i parametri della griglia prima della query |
| Validatore | `src/dr-mailroom-api/Validators/TrashSenderRequestValidator.cs` | Casella e mittente obbligatori per "Elimina tutte" |
| Servizio | `src/dr-mailroom-api/Services/RunService.cs` | "Elimina tutte": verifica casella, autorizzazione del cestino e mail da cestinare, poi crea e affida il giro `TrashSender` |
| Orchestratore e client | `RunOrchestrator`, `GmailTrasher` (in `src/dr-mailroom-core/`) | Cestina a blocchi da 1.000 con il token separato e marca `TrashedAtUtc` |
| Servizio | `src/dr-mailroom-api/Services/MessageService.cs` | Verifica la casella, normalizza il mittente, legge la pagina; verifica la mail e legge gli allegati |
| Provider | `MessageProvider`, `AttachmentProvider`, `MailboxProvider` (in `src/dr-mailroom-core/Infrastructure/Mailroom/`) | Mail indicizzate, allegati estratti, esistenza della casella |
| Filtro | `SenderMessageFilter` (in `.../Mailroom/Filters/`) | Casella, mittente, ricerca nell'oggetto, verso, pagina |
| Entity | `Message`, `ExtractedAttachment` | Mail e allegato salvato |
| DTO | `SenderMessageDto` | `id`, `subject`, `receivedAtUtc`, `labels`, `hasAttachments` |
| DTO | `SenderMessagePageDto` | `items`, `page`, `pageSize`, `totalCount` |
| DTO | `AttachmentDto` | `id`, `partId`, `originalFileName`, `relativePath`, `sizeBytes`, `sha256`, `savedAtUtc` |
| DTO | `TrashSenderRequest` / `TrashSenderStartedDto` | body `mailboxId`, `senderAddress` / risposta `runId`, `messagesToTrash` |

Accesso DB: sì. Servizi esterni: Gmail, solo per "Elimina tutte" (scope `gmail.modify`, token separato).

## Descrizione endpoint

| Metodo | URL | Descrizione | Parametri | Risposta |
|---|---|---|---|---|
| GET | `/api/v1/messages` | Mail di un mittente in una casella | Query: `mailboxId` (obbligatorio), `senderAddress` (obbligatorio, max 320), `search` (facoltativo, max 1000), `sortDir` (`Asc`\|`Desc`, default `Desc`), `page` (da 1, default 1), `pageSize` (1–100, default 25) | 200 `SenderMessagePageDto` · 400 parametri non validi · 404 casella inesistente |
| GET | `/api/v1/messages/{id}/attachments` | Allegati salvati di una mail | Route: `id` (long) | 200 `List<AttachmentDto>` · 404 se la mail non esiste o non ha allegati salvati |
| POST | `/api/v1/messages/trash-sender` | "Elimina tutte": avvia il giro che sposta nel cestino le mail ricevute dal mittente | Body: `mailboxId`, `senderAddress` (max 320) | 202 `TrashSenderStartedDto` · 400 · 404 casella inesistente · 409 casella non attiva, cestino non autorizzato, niente da cestinare o casella occupata |

### Note sulla griglia delle mail

- **Mittente**: confrontato per uguaglianza, senza distinguere maiuscole e minuscole. Il servizio lo porta in minuscolo, come viene salvato. Non è richiesto un indirizzo email formalmente valido: la griglia deve aprirsi anche per i mittenti salvati così come arrivano da Gmail.
- **Ricerca**: cerca il testo dentro l'oggetto. Le mail senza oggetto non compaiono quando la ricerca è attiva.
- **Ordine**: per data di ricezione. A parità di data decide l'`id`, così le pagine restano stabili.
- **`labels`**: gli id delle label Gmail separati da virgola, così come salvati (es. `INBOX,CATEGORY_PROMOTIONS,Label_12`). La traduzione in testo leggibile la fa il frontend. Le label create dall'utente restano come id.
- **Nessuna mail**: 200 con `totalCount` 0 e `items` vuoto, non 404.
- **Indice**: la query usa l'indice esistente `(MailboxId, SenderAddress, ReceivedAtUtc)`.
- **Mail cestinate**: quelle spostate nel cestino con "Elimina tutte" (`TrashedAtUtc` valorizzato) non compaiono.

### Note su "Elimina tutte"

- **Quali mail**: quelle ricevute dal mittente nella casella e non ancora cestinate. Le mail inviate a quell'indirizzo (`IsSent`) restano. La ricerca per oggetto della griglia non conta: vale per tutte.
- **Dove finiscono**: nel cestino di Gmail, recuperabili da Gmail per 30 giorni. dr-mailroom non elimina mai definitivamente in Gmail e non offre un "ripristina".
- **Autorizzazione**: senza il token del cestino il servizio risponde 409. Il frontend legge `MailboxDto.trashAuthorized` e, se serve, porta l'utente al consenso con `GET /api/v1/mailboxes/{id}/trash-authorization`.
- **Avanzamento**: si segue con `GET /api/v1/runs/{runId}`. `messagesRead` = mail trovate, `messagesProcessed` = mail già nel cestino, aggiornati a ogni blocco.

## Flusso endpoint

### GET /api/v1/messages

```mermaid
flowchart LR
    A[GET /messages?mailboxId&senderAddress] --> V{Validatore}
    V -- errori --> X1[400 ValidationProblem]
    V -- ok --> B[MessageService.GetSenderMessagesAsync]
    B --> C[MailboxProvider: la casella esiste?]
    C -- no --> X2[404 ProblemDetails]
    C -- sì --> D[Mittente in minuscolo, ricerca ripulita]
    D --> E[MessageProvider: COUNT + pagina ordinata]
    E --> F[(Message)]
    F --> G[SenderMessageDto.Projection]
    G --> H[200 SenderMessagePageDto]
```

### GET /api/v1/messages/{id}/attachments

```mermaid
flowchart LR
    A[GET /messages/id/attachments] --> B[MessageService.GetAttachmentsAsync]
    B --> C[MessageProvider: la mail esiste?]
    C -- no --> X[404 ProblemDetails]
    C -- sì --> D[AttachmentProvider]
    D --> E[(ExtractedAttachment)]
    E --> F[AttachmentDto.Projection]
    F --> G{Lista vuota?}
    G -- no --> H[200 OK]
    G -- sì --> I[404 ProblemDetails]
```

### POST /api/v1/messages/trash-sender

```mermaid
flowchart LR
    A[POST /messages/trash-sender] --> V{Validatore}
    V -- errori --> X1[400 ValidationProblem]
    V -- ok --> B[RunService.StartTrashSenderAsync]
    B --> C{Casella esiste e attiva?}
    C -- no --> X2[404 / 409]
    C -- sì --> D{Token del cestino?}
    D -- no --> X3[409 cestino non autorizzato]
    D -- sì --> E{Mail da cestinare? Casella libera?}
    E -- no --> X4[409]
    E -- sì --> F[Run TrashSender + Quartz]
    F --> G[202 runId, messagesToTrash]
    F -.-> H[RunOrchestrator: batchModify TRASH a blocchi, TrashedAtUtc]
```

## Esempi

```http
GET /api/v1/messages?mailboxId=1&senderAddress=newsletter@negozio.it&search=promozione&page=1&pageSize=25
x-api-key: {{apiKey}}
```

```json
{
  "items": [
    {
      "id": 4812,
      "subject": "Promozione d'autunno",
      "receivedAtUtc": "2026-10-02T08:14:00Z",
      "labels": "INBOX,CATEGORY_PROMOTIONS",
      "hasAttachments": false
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1
}
```

Per gli altri casi d'uso fare riferimento a `src/dr-mailroom-api/dr-mailroom-api.http`.

---
*Revisione v1.2 — 2026-10-13 21:35 — claude-opus-5-5*
