# Decisioni — brief 20261009-01 (2026-10-09 / 2026-10-10)

## Nuovo endpoint per le statistiche per mittente

Contesto: `/stats/senders` restituisce solo `MailCount`; servono data+oggetto della prima e dell'ultima mail.

Decisione: nuovo `GET /api/v1/stats/senders/overview` (paginato, ordinabile, con ricerca); `/stats/senders` e `/stats/senders/monthly` invariati.

Motivazione: non rompere il contratto esistente; paginazione obbligatoria con 2.440 mittenti.

Conseguenze: Fasi 6–9 del piano coordinatore.

## Aggregazione per indirizzo email e filtro anno

Contesto: le viste esistenti restituiscono fino a 11.737 righe (1,09 MB).

Decisione: aggregare per `SenderAddress`, mai per nome; `year` facoltativo su `overview`; `GET /stats/years` per il selettore e per il dettaglio del mittente (con `senderAddress` esatto).

Motivazione: nessuna vista nuova, nessuna migration, rollback = immagine precedente.

Conseguenze: il FE non carica più le viste mensili; heatmap rimandata.

## Heatmap rimandata e azione "Crea filtro"

Contesto: warroom UX/UI.

Decisione: la Fase 13 non include la heatmap mittente × mese; ogni riga delle statistiche ha "Crea filtro da questo mittente" → `/filters?sender=`.

Motivazione: il bisogno è decidere quali filtri creare; la heatmap non serve al brief.

Conseguenze: Fase 11 con precompilazione `?sender=` (fatta).

## CORS solo in sviluppo per il dev server

Contesto: l'API in container ammetteva solo `http://mailroom.spectre.local`.

Decisione: opzione `Mailroom:Frontend:ExtraCorsOrigins`, vuota di default, valorizzata solo nel `.env` locale; `BaseUrl` resta l'unica destinazione del redirect OAuth.

Motivazione: parità degli ambienti senza origini di sviluppo nei file committati.

Conseguenze: il dev server 5173 funziona contro il container.

## Date di ricezione senza ripiego su 1970

Contesto: 10 mail di spam con `internalDate = 0` restituito da Gmail, salvate come 1970-01-01.

Decisione: ordine delle fonti internalDate > 0 → header `Date` → ora di lettura, con warning; riparazione automatica delle righe con data = 1970-01-01. La data ricavabile dall'id Gmail **non** si usa; nessuna bonifica ulteriore delle 10 righe.

Motivazione: scelta dell'utente: sono spam e le elimina da Gmail.

Conseguenze: 9 mail datate 2026-10-10, 1 dall'header `Date`.

## Regex dei filtri validata solo dal server

Contesto: la validazione lato client con `RegExp` di JavaScript rifiutava sintassi valide in .NET.

Decisione: il FE non compila la regex; mostra il 400 del server accanto al campo.

Motivazione: il server (`ValidationRules.cs`) usa lo stesso motore che applica il filtro.

Conseguenze: rimossa `isCompilableRegex` dal FE.

## Ordine di esecuzione

Contesto: piano backfill con una migration aperta; Fase 13 dipendente dal nuovo contratto.

Decisione: C1 → piano FE → chiusura backfill → CORS → Filtri ∥ date → Esecuzioni → BE statistiche → misura → doc → FE Statistiche.

Motivazione: una migration aperta per volta; contratto API stabile prima della Fase 13.

Conseguenze: prossima la Fase 5 (Esecuzioni).
