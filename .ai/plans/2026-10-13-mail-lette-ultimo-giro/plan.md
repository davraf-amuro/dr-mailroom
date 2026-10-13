# Piano: mail lette nell'ultimo giro concluso
Data: 2026-10-13
Stato: COMPLETATO

## Obiettivo
Nella card della casella, accanto a "Ultimo giro concluso", mostrare anche quante mail sono state lette in quel giro: "13 ott 2026, 20:45 · 1.234 mail lette".

Oggi l'API non espone il dato sulla casella. `MailboxDto` ha solo `LastRunAtUtc`, mentre `MessagesRead` è salvato sul giro (`Run`).

## Analisi
- `LastRunAtUtc` lo scrive `MailboxProvider.RecordRunOutcomeAsync`, chiamato da `RunOrchestrator` solo alla chiusura dei giri `Completed` e `Failed`.
- `Skipped` e `Cancelled` non aggiornano la data.
- Quindi "ultimo giro concluso" = giro più recente (Id massimo) della casella con stato `Completed` o `Failed`.
- Il conteggio si ottiene con una sottoquery nella proiezione EF, come già avviene per `CurrentRun`. Niente migrazione, niente colonne nuove.

## Scope
### Backend (`dr-mailroom`)
- [x] `src/dr-mailroom-api/Dto/MailboxDto.cs` — nuovo campo `int? LastRunMessagesRead`, subito dopo `LastRunAtUtc`. Contiene il `MessagesRead` dell'ultimo giro `Completed`/`Failed` (Id massimo), `null` se la casella non ha giri conclusi. Aggiornare il summary XML.
- [x] `test/dr-mailroom-tests/Api/MailboxDtoTests.cs` — due casi:
  - giri misti: viene preso l'ultimo concluso, ignorando `Running`, `Skipped` e `Cancelled` più recenti;
  - nessun giro concluso: `null`.
  - Il test di traduzione SQL resta verde.

### Frontend (`dr-mailroom-fe`)
- [x] `src/services/api/schema.d.ts` — `lastRunMessagesRead` in `MailboxDto`, rigenerato con `npm run gen:api` dall'API aggiornata. Se l'API locale non è riavviabile, modifica manuale con la stessa forma del generatore.
- [x] `src/composables/mailboxes/mailboxPresentation.ts` — nuova funzione `lastRunText(mailbox)`: data e conteggio formattati con `formatDateTime` e `formatCount`, che già esistono.
  - "Mai" se non c'è data.
  - Solo la data se il conteggio è `null`.
- [x] `src/components/mailboxes/MailboxCard.vue` e `MailboxDetailDialog.vue` — "Ultimo giro concluso" usa `lastRunText`.
- [x] Test: `mailboxPresentation.spec.ts` con i casi di `lastRunText`.
- [x] Fixture con `lastRunMessagesRead`, perché il campo nuovo è obbligatorio nel tipo: `useMailboxPolling.spec.ts`, `mailboxes.spec.ts`, `runs.spec.ts`, `e2e/smoke.spec.ts`.

### Perimetro negativo
- Nessuna migrazione DB, nessun cambio a `Run` o all'orchestratore, nessun altro endpoint.
- `MailboxTable.vue` invariata: la colonna resta la data, ordinabile.
- Il lavoro non correlato già presente nei repo resta com'è: `briefs/`, `.agents/`, `.codex/`, `AGENTS.md`, i piani in `.ai/`.
- Nessun commit e nessun push senza richiesta.

## Fasi
### Fase 1: Backend
- **Stato**: [x]
- **Verifica**: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`

### Fase 2: Schema FE
- **Stato**: [x]
- **Verifica**: `npm run type-check` (fallisce sulle fixture finché non si fa la Fase 3)

### Fase 3: Presentazione FE
- **Stato**: [x]
- **Verifica**: `npm run lint:check`, `npm run format:check`, `npm run type-check`, `npx vitest run`, `npm run build-only`

## Criteri di verifica
- [x] La proiezione restituisce il conteggio dell'ultimo giro `Completed`/`Failed`, `null` senza giri conclusi (test)
- [x] La card e il dettaglio mostrano "data · N mail lette"
- [x] Build e test verdi in entrambi i repo, lint e format puliti

## Note di esecuzione
- `schema.d.ts` modificato a mano: l'API sulla 5180 era la build precedente, non riavviata. Va riallineato con `npm run gen:api` dopo il riavvio dell'API.
- `lastRunText` usa spazi non separabili in "N mail lette", così l'andata a capo non spezza il conteggio. Con `?? null` un'API senza il campo mostra la sola data, invece di "NaN".
- Verifiche: backend con 175 test superati (2 nuovi) e `dotnet format` pulito su API e test; FE con lint, format, type-check, build e 235 test (3 nuovi) tutti a exit 0. Screenshot della card con API finta: "13 ott 2026, 20:45 · 1.234 mail lette".
