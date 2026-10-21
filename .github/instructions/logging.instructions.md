---
applyTo: "**"
---

# Structured Logging Rules (AI Agent)

Scopo: regole di logging valide per qualsiasi linguaggio e stack. La configurazione concreta (libreria, sink, template) sta nell'istruzione di dominio. Per .NET: controlla `.ai/dr-guidelines-packages.json`; se elenca `dr-dotnet-backend` segui `serilog-logging.instructions.md`, altrimenti applica solo le regole di questo file con il logger strutturato del linguaggio e dichiara che manca la configurazione di dominio.

## Regole core (sempre)

1. Logging strutturato: il messaggio è un template con placeholder nominati, i valori passano come argomenti
2. Mai interpolazione o concatenazione di stringhe dentro il messaggio di log
3. Mai dati sensibili nei log (credenziali, token, dati personali): segui `sensitive-data.instructions.md`
4. Contesto ricorrente (thread, task, richiesta, soggetto) aggiunto dal meccanismo di contesto della libreria (enricher, scope, MDC, context var), non ripetuto a mano in ogni messaggio
5. Configurazione dei log (livelli, destinazioni, formato) esterna al codice e sovrascrivibile per ambiente, così un programma in container cambia comportamento senza ricompilare

## Soggetto del log

Il **soggetto** è l'entità su cui lavora un flusso, scelta dall'utente: il valore di un campo come un ID, un Codice o un altro identificativo. Serve a isolare con un solo filtro tutti i log che riguardano quell'entità.

Quando l'utente individua un soggetto (nel task, nel brief, nel piano):

1. Ogni evento di log del flusso lo riporta, compresi quelli scritti dai componenti chiamati
2. Proprietà strutturata con **nome fisso** `Subject`, valore `<Campo>=<valore>` (es. `OrderId=42`, `Codice=ART-0042`)
3. Si imposta **una volta**, all'ingresso del flusso (richiesta, messaggio, elemento elaborato), con il meccanismo di contesto della libreria; si chiude all'uscita del flusso
4. Mai concatenato nel testo del messaggio: sta nella proprietà, non nel template
5. Senza soggetto, la proprietà vale `-`: la colonna è sempre presente e il formato non cambia
6. Nei formati testuali (console, file) ha uno slot fisso: `S:{Subject}`
7. Un flusso, un soggetto. Contesti annidati: vince il più interno
8. Il nome del campo è uno solo in tutto il codice (costante), così il filtro non cambia da un punto all'altro

Soggetto non chiaro (quale campo, quale flusso) → chiedi all'utente, non sceglierlo per supposizione.

### Isolare i log di un soggetto

| Destinazione | Filtro |
|--------------|--------|
| File o console testuale | testo `S:OrderId=42 ` (lo spazio finale esclude `OrderId=420`) |
| Sink strutturato (Seq, Elasticsearch, Loki, GELF) | `Subject = 'OrderId=42'` |

## ✅ Checklist Post-Generazione

- [ ] Messaggi con placeholder, nessuna interpolazione o concatenazione
- [ ] Nessun dato sensibile nei log
- [ ] Configurazione dei log esterna al codice e sovrascrivibile per ambiente
- [ ] Soggetto indicato dall'utente: proprietà `Subject` = `<Campo>=<valore>` impostata all'ingresso del flusso, `-` quando assente, slot `S:{Subject}` nei formati testuali
- [ ] Istruzione di dominio seguita per la configurazione concreta (.NET con `dr-dotnet-backend`: `serilog-logging.instructions.md`), oppure ripiego dichiarato

*Istruzione v2.1 - Logging - 2026-10-20 — claude-opus-5-5 — rimando a `serilog-logging` condizionale al manifest, con ripiego dichiarato*
