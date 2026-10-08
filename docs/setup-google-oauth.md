# Come preparare Google Cloud per dr-mailroom

dr-mailroom legge Gmail con le API ufficiali di Google e l'autorizzazione OAuth 2.0, in **sola lettura**. Prima di collegare una casella serve un progetto Google Cloud con un client OAuth.

La procedura si fa una volta sola e dura circa 15 minuti. Basta un account Google personale.

---

## Riepilogo dei valori

| Voce | Valore |
|---|---|
| Tipo di client OAuth | Applicazione web |
| Scope | `https://www.googleapis.com/auth/gmail.readonly` (unico) |
| Redirect URI | `http://localhost:5180/api/v1/mailboxes/connect/callback` |
| Origini JavaScript | nessuna |
| Stato di pubblicazione | **In produzione** |
| Logo dell'app | nessuno |

---

## 1. Creare il progetto e abilitare la Gmail API

1. Apri [console.cloud.google.com](https://console.cloud.google.com) e crea un nuovo progetto, per esempio `dr-mailroom`.
2. Vai in **API e servizi → Libreria**, cerca **Gmail API** e premi **Abilita**.

## 2. Compilare il Branding

Senza questi campi Google non permette di pubblicare l'app: il pulsante "Pubblica app" mostra un avviso e resta bloccato.

In **Google Auth Platform → Branding**:

| Campo | Cosa inserire |
|---|---|
| Nome app | `dr-mailroom` |
| Email per l'assistenza utenti | il tuo indirizzo Gmail |
| Logo dell'app | **lascia vuoto**: con un logo Google richiede la verifica dell'app |
| Home page dell'applicazione | un URL che controlli (sito personale, profilo GitHub, pagina GitHub Pages) |
| Link alle norme sulla privacy | un URL che controlli, anche una pagina di poche righe |
| Domini autorizzati | il dominio dei due URL precedenti |
| Email di contatto dello sviluppatore | il tuo indirizzo Gmail |

Per un'app personale Google non verifica la proprietà del dominio: la verifica scatta solo se chiedi di far verificare l'app.

## 3. Dichiarare lo scope di sola lettura

In **Accesso ai dati → Aggiungi o rimuovi ambiti**, seleziona `.../auth/gmail.readonly` e salva.

Google lo classifica come ambito "con restrizioni". Per un uso personale non serve altro. Con questo scope dr-mailroom **non può** modificare niente: né lo stato letto/non letto, né le label, né cancellare mail.

## 4. Pubblicare l'app "In produzione"

In **Pubblico** premi **Pubblica app**. Lo stato diventa **In produzione**.

> ⚠️ **Non restare in "Test".** In modalità Test Google fa scadere il refresh token dopo **7 giorni**. Dopo una settimana la casella passerebbe allo stato `TokenRevoked` e andrebbe ricollegata.

## 5. Creare il client OAuth

In **Client → Crea client**:

1. Tipo di applicazione: **Applicazione web**.
2. Nome: `dr-mailroom-api` (lo vedi solo in console).
3. **Origini JavaScript autorizzate**: lascia vuoto. Il flusso OAuth passa dal backend, non dal browser.
4. **URI di reindirizzamento autorizzati**: aggiungi `http://localhost:5180/api/v1/mailboxes/connect/callback`.
5. Non spuntare "This client will be used by an AI-powered agent".
6. Premi **Crea**, poi **Scarica JSON**.

Il redirect URI deve coincidere **carattere per carattere** con `Mailroom:Gmail:RedirectUri`. Se cambi porta o indirizzo dell'API, aggiorna entrambi.

## 6. Custodire le credenziali

Il JSON contiene `client_id` e `client_secret`.

- Non incollarlo in chat, in issue o in file tracciati da git.
- Se lo tieni nella cartella del progetto, usa `Gmail/`: è già esclusa da `.gitignore`, insieme a ogni `client_secret*.json`.

Dove vanno i due valori:

| Ambiente | File | Chiavi |
|---|---|---|
| Sviluppo locale | `src/dr-mailroom-api/appsettings.local.json` (ignorato da git, caricato solo in Development) | `Mailroom:Gmail:ClientId`, `Mailroom:Gmail:ClientSecret` |
| Container | `.env` (ignorato da git; modello in `.env.example`) | `Mailroom__Gmail__ClientId`, `Mailroom__Gmail__ClientSecret` |

## 7. Collegare la prima casella

Con l'API avviata:

1. Chiama `GET /api/v1/mailboxes/connect` con l'header `x-api-key`. La risposta contiene `authorizationUrl`.
2. Apri quell'URL nel browser e accedi con la casella da collegare.
3. Google mostra **"Google non ha verificato questa app"**. È normale per un'app personale: premi *Avanzate → Vai a dr-mailroom (non sicuro)*.
4. Autorizza la sola lettura.
5. Il browser torna su `…/mailboxes?oauth=ok`, sul frontend. Senza frontend la pagina non si apre, ma la casella risulta comunque collegata.

Per controllare il collegamento usa `GET /api/v1/mailboxes`. Il primo giro (cron o `POST /api/v1/runs`) legge tutto lo storico.

---

## Quando qualcosa va storto

| Sintomo | Causa probabile | Rimedio |
|---|---|---|
| Google risponde `redirect_uri_mismatch` | il redirect URI in console non coincide con `Mailroom:Gmail:RedirectUri` | allinea i due valori; le modifiche in console possono richiedere qualche minuto |
| Ritorno con `reason=invalid_state` | sono passati più di 10 minuti, oppure l'API è stata riavviata con un altro key ring | ripeti il collegamento |
| Ritorno con `reason=exchange_failed` | Google non ha restituito il refresh token | revoca l'accesso a dr-mailroom da [myaccount.google.com/permissions](https://myaccount.google.com/permissions) e ricollega |
| Casella in stato `TokenRevoked` | accesso revocato dall'account, password cambiata o app rimasta in "Test" | verifica la pubblicazione (passo 4) e ricollega la casella |
| `reason=mailbox_deleted` | l'indirizzo appartiene a una casella eliminata | per scelta di progetto non si ricollega (D18) |

---

## Fonti

- Piano: `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (T1, T12, C2, C10, D18)
- Codice: `src/dr-mailroom-core/Gmail/GmailAuthorizer.cs`, `src/dr-mailroom-api/Services/MailboxService.cs`
- Architettura: [architettura.md](architettura.md)

*Revisione v1.0 — 2026-10-05 19:36 — claude-opus-5-5*
