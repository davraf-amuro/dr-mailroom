---
agent: 'agent'
description: 'Genera o aggiorna il documento di architettura docs/architettura.md secondo la struttura standard'
tools: ['search/codebase', 'edit/editFiles']
---

# Template: Documento di architettura

Genera o aggiorna `docs/architettura.md`, il documento che spiega come è fatto il sistema e perché.

**Struttura, ordine delle sezioni, tabelle, diagrammi, condizioni e ID delle decisioni** stanno in `.github/instructions/architecture-doc.instructions.md`: leggila per intero e applicala. Questo prompt non la ricopia; dice solo dove prendere le informazioni e come verificarle.

Istruzione assente nel progetto → fermati e dichiaralo: senza la fonte unica il documento non si genera a memoria.

## Prima di scrivere

1. Leggi `.github/instructions/architecture-doc.instructions.md` e `.github/instructions/doc-versioning.instructions.md`.
2. Leggi i piani in `.ai/plans/`: obiettivo, decisioni (aperte con le risposte, prese), fasi e limiti emersi. Sono la fonte delle decisioni D e, se citano un warroom, delle decisioni T.
3. Leggi il manifest `.ai/dr-guidelines-packages.json` e le istruzioni installate in `.github/instructions/`: sono la fonte delle decisioni C. Manifest assente → dichiaralo e ricava le convenzioni solo dalle istruzioni presenti.
4. Individua gli entry point (es. `Program.cs`, `main`, file di avvio del servizio) e i componenti: progetti, servizi, dipendenze esterne.
5. Individua la persistenza: `DbContext` e migrazioni, script SQL, file di stato o l'equivalente dello stack. Nessuna persistenza → vale la frase fissa dell'istruzione.
6. Se `docs/architettura.md` esiste già, leggilo: gli ID delle decisioni esistenti restano invariati.

## Durante la scrittura

- Ogni affermazione si ricava dal codice, dai piani o dai file di configurazione. Quello che non si ricava si scrive `Da verificare`.
- Decisioni dei piani senza ID: ricostruiscile come dice l'istruzione, marcandole `Da verificare`.
- Documento scritto prima del codice e implementazione ora conclusa → applica la sezione "Riallineamento a fine implementazione" dell'istruzione.

## Checklist post-generazione

- [ ] Le 8 sezioni dell'istruzione sono presenti, nell'ordine indicato
- [ ] Ogni sezione condizionale ha il contenuto oppure la sua frase fissa
- [ ] Tutti i diagrammi sono blocchi Mermaid, del tipo richiesto dall'istruzione
- [ ] Ogni decisione dei piani in `.ai/plans/` ha una riga nelle tabelle delle decisioni
- [ ] Nessun ID esistente rinumerato; le decisioni superate riportano `Sostituita da <ID>`
- [ ] Nessuna informazione inventata: i dubbi sono marcati `Da verificare`
- [ ] Footer secondo `doc-versioning.instructions.md`, revisione incrementata

*Prompt v1.0 - Architecture Doc - 2026-10-09 — claude-opus-5-5 — procedura di generazione; la struttura sta in `architecture-doc.instructions.md` (issue #10)*
