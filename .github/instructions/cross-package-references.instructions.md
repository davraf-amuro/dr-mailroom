---
applyTo: "**"
---

# Rimandi fra pacchetti — come si citano le regole che vivono altrove

## Il problema che questa regola risolve

Le istruzioni di un pacchetto `dr-*` a volte rimandano a una regola che vive in un altro pacchetto, dichiarandola "fonte unica" per non duplicarla. Ma i pacchetti si installano singolarmente: un progetto può ricevere quello che cita senza ricevere quello citato.

Un rimando che punta a un file assente non si rompe con un errore. L'agente non trova la fonte, non lo dichiara, e produce una risposta plausibile presa dai pattern che conosce. Il codice compila e supera una revisione superficiale; il difetto si manifesta più tardi, spesso in produzione.

## Come si scrive un rimando a un altro pacchetto

Un rimando a una regola di un altro pacchetto è **sempre condizionale**, e la condizione si verifica sul manifest. Mai un rimando secco a un file che potrebbe non esserci.

Il manifest è `.ai/dr-guidelines-packages.json` nella radice del progetto: contiene `installed[]`, e ogni voce ha il campo `package` con il nome del pacchetto. **Manifest assente o illeggibile equivale a pacchetto non installato**: si applica il ripiego e lo si dichiara, senza fermarsi e senza dedurre da altri indizi.

Struttura obbligatoria del rimando:

1. **La condizione.** "Controlla `.ai/dr-guidelines-packages.json`: se elenca `<pacchetto>`…"
2. **Il ramo pieno.** Cosa fare quando il pacchetto c'è: seguire la fonte unica, citata per nome di file e sezione.
3. **Il ramo di ripiego.** Il minimo indispensabile per non lasciare l'agente senza niente quando il pacchetto manca. Dice **cosa** serve, non **come** si fa: un ripiego che entra nel dettaglio diventa una seconda fonte, e due fonti sulla stessa regola divergono al primo aggiornamento di una delle due.
4. **La dichiarazione.** L'agente scrive nel proprio output quale dei due rami ha applicato.

## Perché la dichiarazione è obbligatoria

Senza, "se il pacchetto è installato, seguilo" resta un'istruzione che l'agente può ignorare senza che nessuno se ne accorga: il risultato è identico nei due casi, e non c'è modo di sapere quale fonte abbia usato. Con la dichiarazione, chi legge l'output vede subito se sta ricevendo la regola completa o il ripiego, e può decidere se installare il pacchetto mancante.

Forma minima della dichiarazione, una riga:

> `dr-minimalapi` non risulta nel manifest: applico il ripiego di `docker-swarm-compose.instructions.md` invece della fonte unica.

## Quando invece serve una dipendenza dichiarata

Un rimando condizionale è la regola. La dipendenza nel catalogo (`scaffolding-catalog.json`, campo `dependencies`) serve solo quando il pacchetto citante **non ha senso** senza quello citato, non quando lo cita e basta.

⛔ Prima di dichiarare una dipendenza, verifica due cose:

- **L'installer risolve le dipendenze in automatico e in modo ricorsivo**, senza chiedere conferma: dichiararne una significa installare quel pacchetto e tutti i suoi antenati in ogni host che riceve il citante.
- **L'installer non legge `appliesTo`.** Una dipendenza da un pacchetto `dotnet` dichiarata su un pacchetto `node` o `any` porta i file di progetto .NET nella radice di un host che .NET non è.

Se i due pacchetti hanno `appliesTo` diversi, la dipendenza dichiarata è quasi sempre la scelta sbagliata.

## Regole di perimetro

- Nessun rimando incondizionato a un file di un altro pacchetto, nemmeno quando "in pratica è sempre installato insieme".
- Il ramo di ripiego non duplica la fonte: se ti accorgi di star riscrivendo la regola, fermati — o accorci il ripiego, o quella regola andava in un pacchetto più a monte.
- Un file che contiene un ripiego non può più dichiarare "non duplicare qui": le due affermazioni si contraddicono. Riscrivi anche quella riga.

---

*Istruzione v1.0 - Rimandi fra pacchetti - 2026-09-24 — claude-opus-5*
