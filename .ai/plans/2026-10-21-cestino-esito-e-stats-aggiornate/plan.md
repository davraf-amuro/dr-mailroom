# Piano: esito del cestino visibile e statistiche sempre aggiornate
Data: 2026-10-21
Stato: COMPLETATO
Approvazione: utente in chat, 2026-10-21 — "per stats ok, per elimina segna solo 'processo in esecuzione' e basta, senza aggiornare la pagina"
Branch: `plan/aggiustamenti-generali` (proposto: stesso branch del lavoro di oggi, solo repo `dr-mailroom-fe`)

## Obiettivo
1. Dopo "Elimina tutte" / "Elimina senza 📎" l'avviso dice solo che il processo è in esecuzione. **Nessun polling, nessun aggiornamento della pagina** (decisione utente).
2. Tornando su `/stats` si rileggono **tutti** i dati (totale in testa, grafico dei più attivi, tabella), non solo la tabella.

## Analisi
- DB e API sono corretti: giro 40057 `Completed`, 870/870 mail di `jagent@route.monster.com` con `TrashedAtUtc`; `GET /stats/senders/overview` non restituisce più il mittente.
- Causa 1: `useTrashSender.start` mostra "verranno spostate" e si ferma; il giro è asincrono (Quartz) e la vista non ne segue l'esito, né rilegge la griglia.
- Causa 2: `useStatsView` all'apertura, con casella già scelta, chiama solo `loadOverview`; `loadTop` (grafico) e `loadYears` (totale della frase in testa) restano quelli della visita precedente (store Pinia vivo tra le viste).

## Scope
### File da modificare (dr-mailroom-fe)
- [x] `src/stores/stats.ts` — `reload()`: anni, panoramica e classifica insieme
- [x] `src/composables/stats/useStatsView.ts` — all'apertura `reload()` invece del solo `loadOverview`
- [x] `src/composables/stats/trashPresentation.ts` — `trashStartedText` apre con "Processo in esecuzione"
- [x] `src/composables/stats/useTrashSender.ts` — avviso di avvio con tono `info` (non è ancora un esito)
- [x] test: `stores/__tests__/stats.spec.ts`, `composables/stats/__tests__/useTrashSender.spec.ts`, `trashPresentation.spec.ts`

### Perimetro negativo
- Nessuna modifica all'API né al DB. Nessuna push, nessun merge.

## Fasi
### Fase 1: statistiche rilette per intero — [x]
### Fase 2: avviso "processo in esecuzione" dopo Elimina — [x]
### Fase 3: lint, type-check, test, build; commit; rebuild container FE — [x]

## Criteri di completamento
- [x] Riaprendo `/stats` grafico e totale non mostrano più le mail cestinate
- [x] Dopo Elimina l'avviso dice "Processo in esecuzione"; la pagina non si aggiorna da sola
- [x] Lint, type-check, test e build verdi; container FE aggiornato

## Esito verifica (2026-10-21)
- FE: lint e format puliti, type-check ok, 303 test superati (nuovo: rilettura completa dello store stats), build ok.
- Commit `09690f9` su `plan/aggiustamenti-generali` (dr-mailroom-fe); container `dr-mailroom-fe` ricostruito, `/stats` risponde 200.
- Non verificato a mano nel browser.
