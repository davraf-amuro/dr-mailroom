# Piano: key ring Data Protection condiviso tra dev locale e container
Data: 2026-10-20
Stato: COMPLETATO

## Obiettivo
Un solo key ring per tutti gli ambienti che usano lo stesso DB, così un refresh token cifrato in locale si decifra anche nel container e viceversa.

## Contesto
Errore `The key {f6370560-…} was not found in the key ring` nel giro di cestino della casella 1. La chiave era in `src/dr-mailroom-api/data/keys/`, cioè nel key ring del `dotnet run` locale (path relativo alla content root). Il container usa `./data/keys`. Il DB è lo stesso per entrambi.
Fix immediato già applicato: chiave copiata in `data/keys/` e container riavviato.

## Scope
### File da modificare
- [x] `E:/Davide/VM/docker/dr-mailroom/keys/` — nuova cartella del key ring condiviso (fuori repo, come attachments e logs)
- [x] `.env` (non committato) — `KEYS_HOST_PATH` verso la cartella condivisa
- [x] `src/dr-mailroom-api/appsettings.local.json` (non committato) — `Mailroom:DataProtection:KeyRingPath` verso la stessa cartella
- [x] `docs/avvio-e-configurazione.md` — nota: con un DB condiviso tra locale e container, il key ring deve essere lo stesso
- [x] `.env.example` — commento su `KEYS_HOST_PATH` con lo stesso avviso

### Perimetro negativo
- Non toccherò: codice C# (`TokenProtectionExtensions` è corretto), `Dockerfile`, `docker-compose.yml`, `appsettings.json`, `appsettings.Development.json` (committato: niente percorsi della macchina)
- Non cancellerò le vecchie cartelle `data/keys/` e `src/dr-mailroom-api/data/keys/`: restano come backup, l'utente decide se rimuoverle

## Fasi

### Fase 1: crea il key ring condiviso
- **Stato**: [x]
- **Precondizione**: `E:/Davide/VM/docker/dr-mailroom/` esiste
- **File**: `E:/Davide/VM/docker/dr-mailroom/keys/`
- **Operazione**: CREATE
- **Azione**: crea la cartella e copia le due chiavi (`key-9f807e8d-…xml`, `key-f6370560-…xml`)
- **Tool ammessi**: shell
- **Verifica passo**: la cartella contiene 2 file `key-*.xml`
- **Su divergenza**: STOP

### Fase 2: punta il container alla cartella condivisa
- **Stato**: [x]
- **Precondizione**: Fase 1 completata
- **File**: `.env`
- **Operazione**: EDIT
- **Azione**: aggiungi `KEYS_HOST_PATH=E:/Davide/VM/docker/dr-mailroom/keys`
- **Tool ammessi**: Edit
- **Verifica passo**: `docker compose config` mostra il bind del volume `/data/keys` sulla nuova cartella
- **Su divergenza**: STOP

### Fase 3: punta il dev locale alla cartella condivisa
- **Stato**: [x]
- **Precondizione**: Fase 1 completata
- **File**: `src/dr-mailroom-api/appsettings.local.json`
- **Operazione**: EDIT
- **Azione**: aggiungi `Mailroom:DataProtection:KeyRingPath = E:/Davide/VM/docker/dr-mailroom/keys` (merge con la sezione `Mailroom` che c'è già)
- **Tool ammessi**: Edit
- **Verifica passo**: il JSON è valido e contiene la chiave
- **Su divergenza**: STOP

### Fase 4: ricrea il container
- **Stato**: [x]
- **Precondizione**: Fase 2 completata
- **File**: nessuno
- **Operazione**: EDIT (runtime)
- **Azione**: `docker compose up -d` (ricrea il container con il nuovo volume)
- **Tool ammessi**: shell
- **Verifica passo**: container `Up`; nei log dell'avvio nessun errore Data Protection; nessuna nuova chiave generata nella cartella condivisa (restano 2 file)
- **Su divergenza**: STOP

### Fase 5: documentazione
- **Stato**: [x]
- **Precondizione**: Fasi 1-4 completate
- **File**: `docs/avvio-e-configurazione.md`, `.env.example`
- **Operazione**: EDIT
- **Azione**: aggiungi l'avviso: locale e container sullo stesso DB → stesso `KeyRingPath`, altrimenti i token non si decifrano; aggiorna versione/data in fondo al doc se presente
- **Tool ammessi**: Edit
- **Verifica passo**: rilettura dei due file
- **Su divergenza**: STOP

## Criteri di verifica finale
- [x] `E:/Davide/VM/docker/dr-mailroom/keys/` contiene le 2 chiavi
- [x] il volume `/data/keys` del container punta alla cartella condivisa
- [x] `appsettings.local.json` punta alla stessa cartella
- [x] il container riparte senza errori Data Protection
- [x] la documentazione riporta l'avviso
- [x] nessun file committato contiene percorsi della macchina o segreti
