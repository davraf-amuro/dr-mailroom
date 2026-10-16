---
applyTo: "**"
---

# Concurrency — Operazioni indipendenti, esecuzione parallela

Regola trasversale (tutti i linguaggi). Le operazioni indipendenti non si accodano per abitudine: si valuta prima se possono procedere insieme, e se restano in serie lo si dichiara.

Livello della regola: **preferire** il parallelismo, **dichiarare** sempre la serie. Non è un obbligo di parallelizzare: quote dei servizi e ordine dei risultati possono imporre la serie, ma la scelta deve essere visibile nell'output.

---

## 1. Scopo e perimetro

Si applica quando il codice da scrivere esegue più operazioni di I/O o di calcolo su elementi distinti: un ciclo che chiama un servizio per ogni elemento, una sequenza di chiamate a sorgenti diverse, l'elaborazione di un insieme di messaggi o file.

Non si applica a una singola operazione, né a operazioni il cui esito alimenta la successiva.

---

## 2. Prima di scrivere il ciclo — controllo di indipendenza

Prima di scrivere un ciclo o una sequenza di chiamate, verifica tre cose:

1. **Dipendenza**: l'operazione N usa il risultato dell'operazione N-1?
2. **Ordine**: l'ordine di esecuzione o di scrittura del risultato conta?
3. **Limite del servizio**: la sorgente o il servizio chiamato ha quote o rate limit che il parallelismo supererebbe?

Tre risposte NO → le operazioni sono indipendenti: usa lo Schema A o lo Schema B.
Anche una sola risposta SÌ → resta in serie e dichiaralo (sezione 6).

---

## 3. Schema A — Fan-out con attesa di tutti i rami

Un insieme noto di operazioni indipendenti (per esempio la stessa richiesta a più provider). Si lanciano tutte, con un limite al numero di rami attivi, e si attende il risultato di tutte prima di proseguire.

```text
limite = leggi dalla configurazione
rami   = per ogni elemento: avvia operazione(elemento, segnale di cancellazione)
         dentro il limite di rami attivi
attendi tutti i rami
per ogni ramo: se riuscito → raccogli il risultato
               se fallito  → registra errore con identificativo del ramo
decidi: risultato completo | risultato parziale dichiarato | fallimento
```

---

## 4. Schema B — Producer/consumer con coda e N consumer

Un flusso di elementi da acquisire ed elaborare. Chi acquisisce non elabora: i due lati comunicano da una coda a capienza fissa, e il parallelismo dell'elaborazione si regola con il numero di consumer.

```text
coda       = coda FIFO con capienza massima (contropressione)
producer   = acquisisce elementi → inserisce in coda (attende se piena) → chiude la coda alla fine
consumer×N = finché la coda ha elementi: prendi elemento → elabora → registra esito
attendi tutti i consumer
```

Il numero di consumer N viene dalla configurazione, non è un letterale nel codice.

---

## 5. Vincoli obbligatori (valgono per entrambi gli schemi)

| Vincolo | Regola |
|---|---|
| **Limite esplicito** | Il grado di parallelismo (rami attivi, numero di consumer, capienza della coda) è un valore di configurazione con un default prudente. Mai "tutti insieme senza limite". Segui `no-hardcoded-values.instructions.md` |
| **Cancellazione** | Il segnale di cancellazione arriva a ogni ramo e a ogni consumer, e viene controllato nelle operazioni di I/O. Una cancellazione non è un errore da registrare |
| **Errore per ramo** | L'errore di un ramo non cancella gli altri in silenzio e non viene inghiottito: si raccoglie con l'identificativo del ramo e si riporta. L'esito complessivo (completo, parziale, fallito) è una decisione esplicita |
| **Risorse non condivise** | Una risorsa con stato non thread-safe (connessione o contesto di database, client con stato, buffer) non si condivide fra rami o consumer: un'istanza per ramo o per consumer, creata e rilasciata dentro il ramo |
| **Idempotenza** | Un elemento può essere rielaborato (ripresa dopo errore, riavvio): l'elaborazione di un elemento non deve produrre effetti duplicati |

---

## 6. Quando restare in serie

Si resta in serie quando vale almeno una di queste condizioni:

- l'ordine conta (risultati da scrivere o applicare in sequenza);
- c'è dipendenza dal risultato dell'operazione precedente;
- il volume è piccolo e fisso (per esempio 2-3 operazioni note a priori) e il parallelismo non porta un vantaggio misurabile;
- il servizio chiamato impone un limite che il parallelismo supererebbe.

La serie è una scelta valida, ma **va dichiarata**: una riga nell'output del task, in forma fissa:

> Resto in serie: `<ordine conta | dipendenza dal risultato | volume piccolo e fisso | limite del servizio>`

Nessuna riga di dichiarazione e nessuna parallelizzazione → regola non rispettata.

---

## 7. Pacchetto di dominio

Se un pacchetto di dominio installato ha un'istruzione sulla concorrenza, per sintassi e API comanda quella: questa regola fissa il principio, il pacchetto di dominio dice come si scrive nel linguaggio. I pacchetti installati sono elencati in `.ai/dr-guidelines-packages.json`. Se nessun pacchetto ne ha una, applica questa regola con le API idiomatiche del linguaggio in uso.

---

## 8. Anti-pattern

| Anti-pattern | Perché è un problema |
|---|---|
| Ciclo che attende ogni chiamata prima di avviare la successiva, su elementi indipendenti | Tempo totale = somma delle attese; il parallelismo disponibile resta inutilizzato |
| Avvio di un ramo per ogni elemento senza limite | Satura quote, connessioni e memoria; il servizio chiamato risponde con errori di limite |
| Leggi ed elabora nello stesso ciclo parallelo | Il parallelismo dell'elaborazione colpisce la sorgente: non si regolano più separatamente |
| Una risorsa non thread-safe condivisa fra rami | Errori intermittenti, difficili da riprodurre |
| Errore di un ramo inghiottito o che interrompe gli altri senza decisione | Risultati parziali presentati come completi, oppure lavoro buttato senza motivo |
| Cancellazione non propagata ai rami | Il lavoro continua dopo che il chiamante ha rinunciato |
| Coda senza capienza massima | Se chi acquisisce è più veloce di chi elabora, la memoria cresce senza limite |
| Parallelizzare per principio, senza controllo di indipendenza | Introduce race condition e viola quote o ordine, dove la serie era corretta |

---

## 9. Checklist post-generazione

- [ ] Controllo di indipendenza eseguito (dipendenza, ordine, limite del servizio)
- [ ] Operazioni indipendenti: Schema A o B, con grado di parallelismo letto dalla configurazione
- [ ] Cancellazione propagata a ogni ramo; errore per ramo raccolto e riportato; nessuna risorsa non thread-safe condivisa
- [ ] Se in serie: riga "Resto in serie: <motivo>" presente nell'output

---

*Istruzione v1.0 - Concorrenza - 2026-10-14 — claude-sonnet-5-5*
