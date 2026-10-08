# Decisioni

Decisioni prese nella sessione 2026-10-08 → 2026-10-09 sul frontend `dr-mailroom-fe`. Quelle di stack, prese prima di questa sessione, sono nella tabella "Decisioni di stack" di `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md`; quelle del backend nel piano `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` e in `docs/architettura.md`.

## Repo FE pubblico

- **Contesto:** domanda aperta D-1 del piano.
- **Decisione:** `davraf-amuro/dr-mailroom-fe` pubblico.
- **Motivazione:** scelta dell'utente.
- **Conseguenze:** `public/config.json` ignorato da git; la Fase 18 controlla che non sia tracciato.

## Font Saira "per ora"

- **Contesto:** domanda aperta D-3.
- **Decisione:** Saira, da rivalutare al Checkpoint C1.
- **Motivazione:** scelta dell'utente.
- **Conseguenze:** un eventuale cambio tocca solo `tokens.css`/`main.css` e il pacchetto Fontsource.

## Piano `mailroom-gmail-reader` da rivedere a FE pronto

- **Contesto:** il piano backend è `INTERROTTO`.
- **Decisione:** resta `INTERROTTO`, con una nota di revisione futura.
- **Motivazione:** scelta dell'utente.
- **Conseguenze:** non blocca il piano FE.

## Etichette dalla specifica, metafora come sopratitolo

- **Contesto:** la specifica dice "Esegui ora", design.md diceva "Lancia giro".
- **Decisione:** "Esegui ora" con sopratitolo "Sortita".
- **Motivazione:** coerenza con la specifica e con la regola di design.md "etichetta chiara accanto alla metafora".
- **Conseguenze:** design.md v1.1 aggiornato.

## Revisione v1.1 del piano

- **Contesto:** il piano era precedente alla specifica v1.1 (pausa casella, annullamento giri).
- **Decisione:** aggiunti Pausa/Riprendi, Annulla/Annulla tutti, stop del polling su `Cancelled`, gestione 401, endpoint mancanti, nota CORS, controllo su `config.json`, commit locale per fase, Checkpoint C1.
- **Motivazione:** revisione approvata dall'utente ("procedi").
- **Conseguenze:** 26 endpoint coperti dai criteri finali.

## TypeScript 6.0.x

- **Contesto:** l'utente ha chiesto la versione più recente possibile; TS 7.0.2 è uscito.
- **Decisione:** restare su 6.0.3.
- **Motivazione:** `typescript-eslint` 8.71 ha peer `typescript >=4.8.4 <6.1.0`.
- **Conseguenze:** da rivalutare quando `typescript-eslint/typescript-eslint#10940` sarà chiusa.

## Override per `openapi-typescript`

- **Contesto:** `openapi-typescript` 7.13 dichiara peer `typescript ^5.x`.
- **Decisione:** `overrides: { "openapi-typescript": { "typescript": "$typescript" } }`, non `--legacy-peer-deps`.
- **Motivazione:** eccezione limitata a un pacchetto; `gen:api` verificato funzionante.
- **Conseguenze:** da rimuovere quando il pacchetto accetterà TS 6.

## npm globale aggiornato

- **Contesto:** npm 10.7.0 falliva `npm install` con `Cannot read properties of null (reading 'edgesOut')`.
- **Decisione:** `npm i -g npm@11` (11.21.0).
- **Motivazione:** richiesta dell'utente.
- **Conseguenze:** nessun bisogno di `npx npm@11`.

## Avvio del backend da parte dell'agente

- **Contesto:** la Fase 6 richiede `/openapi/v1.json`.
- **Decisione:** l'agente avvia il backend in background (opzione B).
- **Motivazione:** scelta dell'utente, informato che il cron legge le caselle reali.
- **Conseguenze:** il processo resta attivo finché non viene fermato.

## Chiave API in memoria, configurazione a runtime

- **Contesto:** `dr-minimalapi` non è installato nel FE; si applica il ripiego della Regola 7 di `frontend-organization.instructions.md`.
- **Decisione:** chiave in una variabile di modulo di `client.ts`, letta da `config.json` prima del mount.
- **Motivazione:** specifica ("non in localStorage", configurazione a runtime) e Regola 7.
- **Conseguenze:** la stessa build gira con chiavi diverse; nessuna login né route guard.

## Segnali globali separati dall'interpretazione delle risposte

- **Contesto:** 401/403/503 devono arrivare al banner, indipendentemente dalla vista.
- **Decisione:** `apiEvents.ts` (`onApiAlarm`) alimentato da un middleware del client; `problem.ts` resta una funzione pura.
- **Motivazione:** responsabilità separate, test semplici.
- **Conseguenze:** file non previsto dal piano, annotato nella Fase 6.

## Token aggiuntivi di contrasto

- **Contesto:** `--c-panel-line` misura 1.40:1 (light) e 1.32:1 (dark).
- **Decisione:** `--c-control-border` (≥ 3:1) per i controlli e token `--c-on-*` per il testo su fondi pieni; panel line solo decorativa.
- **Motivazione:** WCAG 1.4.3 e 1.4.11.
- **Conseguenze:** tabella dei contrasti misurati in design.md.

## Fase dedicata all'icona

- **Contesto:** l'utente non vuole un'icona di ripiego su un'interfaccia curata; ha proposto di farla generare a ChatGPT.
- **Decisione:** Fase 9.1 nel piano (v1.2); prompt in `icon-prompt.md`; l'immagine eventuale viene ridisegnata in SVG.
- **Motivazione:** leggibilità a 16 px; l'agente non ha accesso a ChatGPT.
- **Conseguenze:** icona mostrata al Checkpoint C1.

## Conferma che non si chiude da sola

- **Contesto:** le conferme (elimina, annulla) avviano chiamate asincrone.
- **Decisione:** `ConfirmDialog` emette `confirm` e resta aperta; la chiude il chiamante.
- **Motivazione:** gestire `busy` e gli errori 409 senza perdere la finestra.
- **Conseguenze:** ogni vista gestisce la chiusura dopo l'esito.
