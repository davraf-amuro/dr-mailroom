---
applyTo: "docs/architettura.md"
---

# Documento di architettura — struttura obbligatoria di `docs/architettura.md`

## Scopo

Questa istruzione è la **fonte unica** del documento di architettura di un progetto: sezioni, ordine, tabelle, diagrammi, condizioni e schema degli ID delle decisioni. Vale per chiunque lo scriva o lo aggiorni, qualunque agente usi. I prompt e le skill che lo generano rimandano qui e non ricopiano la struttura.

- **Un documento per repository**: `docs/architettura.md` nella radice del repository. In una soluzione multi-repo ogni repository ha il proprio.
- **Mai inventare.** Un'informazione che non si ricava dal codice, dai piani o dai file di configurazione si scrive `Da verificare`. Vale per ogni cella di tabella, ogni nodo di diagramma, ogni regola.

---

## Diagrammi: solo Mermaid

Ogni diagramma del documento è un blocco di codice recintato con linguaggio `mermaid`. Niente immagini, niente file esterni, niente altri formati.

Perché: Mermaid è testo, quindi entrambi gli agenti lo leggono e lo modificano come il resto del documento, e GitHub e VS Code lo visualizzano senza strumenti esterni.

---

## Struttura obbligatoria

Le 8 sezioni compaiono in quest'ordine. Una sezione condizionale non si omette: quando la condizione manca, al posto del contenuto c'è la sua frase fissa.

| # | Sezione | Contenuto obbligatorio | Condizionale |
|---|---|---|---|
| 1 | Introduzione | 2–3 frasi + cosa resta fuori + fonte delle decisioni | no |
| 2 | Come sono fatti i componenti | `flowchart` + tabella `Componente \| Ruolo` | no |
| 3 | Decisioni | tabelle `# \| Tema \| Decisione` per origine D / T / C | solo la tabella T |
| 4 | Come funziona `<flusso>` | da 1 a 5 sezioni, diagramma + "Regole che valgono" | no |
| 5 | Modello dati | tabella + `erDiagram` + note su enum e cancellazione | sì |
| 6 | Cosa il sistema non fa | elenco | no |
| 7 | Limiti noti | tabella `Limite \| Effetto \| Come si rimedia` | sì (frase fissa se vuota) |
| 8 | Footer | formato di `doc-versioning.instructions.md` | no |

### 1. Introduzione

- Cosa fa il sistema in 2–3 frasi: per chi, quale problema risolve.
- **Una riga** su cosa resta fuori, con rimando alla sezione "Cosa il sistema non fa", dove l'elenco è completo.
- La fonte delle decisioni: i piani in `.ai/plans/` (con il loro percorso) ed eventuali sessioni warroom.

### 2. Come sono fatti i componenti

- Un diagramma `flowchart` Mermaid con i componenti e le dipendenze fra loro: progetti, servizi, database, sistemi esterni.
- La tabella:

| Componente | Ruolo |
|---|---|
| `<nome come compare nel codice>` | cosa fa, in una frase |

Ogni nodo del diagramma ha una riga nella tabella, e viceversa.

### 3. Decisioni

Una tabella per origine, con il prefisso dell'ID che dice da dove viene la decisione:

| Prefisso | Origine | Condizione |
|---|---|---|
| `D` | decisioni dell'utente (piani, risposte alle decisioni aperte) | sempre |
| `T` | decisioni tecniche nate da un warroom | solo se c'è stato un warroom |
| `C` | convenzioni dei pacchetti `dr-*` installati (manifest `.ai/dr-guidelines-packages.json` e istruzioni in `.github/instructions/`) | sempre |

Formato di ogni tabella:

| # | Tema | Decisione |
|---|---|---|
| D1 | `<argomento in poche parole>` | `<cosa si è deciso e perché, in una o due frasi>` |

Regole degli ID:

- Forma `D1`, `T1`, `C1`: prefisso più numero progressivo. Gli ID valgono **solo dentro questo documento**.
- **Stabili**: un ID non si rinumera e non si riusa, anche quando una decisione viene tolta o superata.
- Decisione superata: la riga resta, e nella colonna Decisione si aggiunge `Sostituita da <ID>` con l'ID della nuova.
- Nessun warroom: la tabella T non compare e al suo posto c'è la frase fissa **"Nessuna decisione tecnica da warroom."**

**Ogni decisione dei piani in `.ai/plans/` che riguardano il sistema compare nel documento.** Per verificarlo, si scorrono le sezioni delle decisioni di ogni piano (per esempio "Decisioni aperte" con le risposte, "Decisioni prese") e si controlla che ciascuna abbia una riga.

- Piano con decisioni numerate o identificate: la riga ne cita l'origine (es. `piano 2026-10-09-<slug>, decisione 3`).
- Piano senza ID o senza una sezione delle decisioni: le decisioni si ricostruiscono dal testo del piano e la riga termina con `Da verificare`, perché la ricostruzione è un'interpretazione.

### 4. Come funziona `<flusso>`

Una sezione per ogni **flusso principale**: un percorso che attraversa almeno 2 componenti della sezione 2 (per esempio una richiesta HTTP che passa dall'endpoint al database, un job che legge una coda e scrive un file). Un'operazione che resta dentro un solo componente non è un flusso principale.

- Da **1 a 5** sezioni. Più di 5 flussi candidati → si descrivono i 5 più importanti per chi entra nel progetto, gli altri si citano in una riga.
- Titolo: `Come funziona <flusso>`, con il nome del flusso in linguaggio naturale (es. "Come funziona l'importazione delle email").
- Un diagramma Mermaid: `sequenceDiagram` quando conta l'ordine dei messaggi fra componenti, `flowchart` quando contano le diramazioni e le decisioni.
- Sotto il diagramma, l'elenco **"Regole che valgono"**: una regola per voce, ognuna con il perché (es. "Lo stato si salva solo dopo l'invio: un errore a metà non lascia record orfani").

### 5. Modello dati

Condizione: il progetto persiste dati (database, file di stato, code durevoli). Se non ne persiste, la sezione contiene solo la frase fissa **"Il progetto non persiste dati."**

- La tabella:

| Tabella | Colonne chiave | Vincoli e indici |
|---|---|---|
| `<nome>` | chiave primaria, chiavi esterne, colonne che guidano la logica | unicità, indici, check |

- Un diagramma `erDiagram` Mermaid con le entità e le relazioni.
- Le note su enum e stati (valori ammessi e significato) e sulle regole di cancellazione (fisica, logica, a cascata).

### 6. Cosa il sistema non fa

Elenco di ciò che il sistema esplicitamente non fa, ciascuno con una riga di motivazione o il rimando a chi lo fa al suo posto. Serve a chi legge per non cercare funzioni che non esistono.

### 7. Limiti noti

| Limite | Effetto | Come si rimedia |
|---|---|---|
| `<cosa non funziona o non scala>` | `<cosa vede chi usa il sistema>` | `<rimedio, workaround o "Da verificare">` |

Nessun limite noto → al posto della tabella la frase fissa **"Nessun limite noto alla revisione vN."**, con il numero di revisione del footer.

### 8. Footer

Si applica `doc-versioning.instructions.md` (il documento sta in `docs/`): ultima riga del file, revisione incrementata a ogni modifica.

---

## Riallineamento a fine implementazione

Si applica in due casi: il documento è stato scritto **prima del codice**, per esempio da una fase iniziale di un piano; oppure un piano modifica codice che il documento già descrive. In entrambi, a implementazione finita il documento può descrivere il progetto o il sistema di prima, non quello realizzato: va confrontato col codice reale e corretto.

Quando farlo lo stabilisce il piano che modifica il documento, che prevede la fase di riallineamento secondo `plan-tracking.instructions.md`. Questa sezione dice **cosa** si confronta.

| Ambito | Cosa si confronta col codice reale |
|---|---|
| Flussi (sezioni "Come funziona") | chi crea cosa, dove nasce un errore e con quale codice o eccezione, quando si salva lo stato |
| Modello dati | tabelle, colonne, vincoli, stati ed enum effettivamente presenti |
| Limiti noti | i limiti emersi durante l'implementazione (nel piano, nei commenti, nei test) si spostano in "Limiti noti" |

- Ogni differenza si corregge nel documento, diagrammi compresi: il codice è la fonte.
- Un'affermazione che non trova riscontro nel codice si corregge o si marca `Da verificare`.
- La revisione nel footer si incrementa secondo `doc-versioning.instructions.md`.

---

## Regole di perimetro

- Le 8 sezioni nell'ordine indicato, nessuna omessa.
- Nessuna sezione vuota: o il contenuto, o la frase fissa della sua condizione.
- Nessun diagramma in un formato diverso da Mermaid.
- Nessun dato sensibile (segui `sensitive-data.instructions.md`): nomi di server, stringhe di connessione e credenziali non entrano nel documento.

---

*Istruzione v1.1 - Architecture Doc - 2026-10-09 — claude-opus-5-5 — riallineamento esteso al codice già descritto, come plan-tracking (issue #11)*
