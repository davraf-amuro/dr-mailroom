# Copilot Instructions (AI Agent)

Language: Italian — Rispondi sempre in italiano.

Queste istruzioni valgono per **qualsiasi progetto**, indipendentemente dal linguaggio e dallo stack. Le regole specifiche di un dominio (backend .NET, frontend, firmware, contenuto documentale) arrivano dal pacchetto `dr-*` installato per quel dominio.

## Dominio del progetto

Prima di generare o modificare codice, stabilisci in quale dominio stai lavorando:

1. Leggi `.ai/dr-guidelines-packages.json` — elenca i pacchetti `dr-*` installati in questo progetto.
2. Leggi le istruzioni modulari che quei pacchetti hanno portato in `.github/instructions/`.
3. L'istruzione del dominio è quella che comanda su struttura, convenzioni di linguaggio e comandi di verifica.

Nessun pacchetto di dominio installato e il task richiede codice applicativo → fermati e chiedi quale dominio, invece di assumerne uno.

Il rilevamento vale per i task che generano o modificano **codice applicativo**. Per task di sola documentazione o configurazione (markdown, file di config), non porre la domanda: prosegui con l'istruzione modulare pertinente al file (es. `doc-versioning`, `readme-structure`).

## Convenzioni essenziali (tutti i domini)

- `async`/`await` (o l'equivalente idiomatico del linguaggio) per ogni operazione di I/O
- Logging strutturato con placeholder — mai interpolazione di stringa dentro il log
- Naming coerente con le convenzioni del linguaggio in uso, applicate in modo uniforme in tutto il progetto
- Validazione esplicita di ogni input che arriva dall'esterno; segui `input-validation.instructions.md`
- Valori letterali con significato centralizzati; segui `no-hardcoded-values.instructions.md`
- Dati sensibili: segui sempre `sensitive-data.instructions.md`; credenziali **mai** in file committati

## Checklist Pre-Task (obbligatoria)

Fonte unica: `dev-cycle.instructions.md` — Fase 0. Compila quella checklist nell'output prima di qualsiasi azione. Anche una sola risposta NO → fermati e completa il passo prima di procedere.

## Checklist Post-Generazione

- [ ] Dominio individuato dai pacchetti installati, istruzione modulare letta
- [ ] Ho seguito le istruzioni modulari pertinenti
- [ ] Logging strutturato e gestione asincrona dell'I/O usati dove servono

## Verifica post-modifica (qualsiasi file)

Dopo ogni modifica a un file:
1. Rileggi il file modificato
2. Confronta il contenuto con quanto richiesto
3. Solo se corrispondono, dichiara la modifica completata

## Ciclo di sviluppo obbligatorio

Ogni task segue il ciclo definito in `dev-cycle.instructions.md`:
- **Dichiara** scope e file prima di agire
- **Esegui** un'operazione alla volta
- **Verifica** (rileggi) dopo ogni modifica
- **Segnala** incertezza - non assumere silenziosamente

Task con >= 2 operazioni: crea piano su disco in `.ai/plans/<YYYY-MM-DD>-<slug>/` prima di procedere.
Segui `plan-tracking.instructions.md` per struttura e verifica finale.

## Brief dell'utente — cartella `briefs/`

`briefs/` in radice contiene le richieste che l'utente scrive in Markdown, con nome e forma liberi. L'agente non la esplora da solo: usa il brief che l'utente indica nel prompt.

Brief indicato → leggilo, chiedi approfondimenti se trovi lacune o problemi, crea **sempre** il piano (anche per una sola operazione) con il link al brief sotto il titolo, ed esegui solo dopo l'approvazione esplicita dell'utente. Vale anche quando il brief è l'argomento di un prompt. Regola completa: `plan-tracking.instructions.md`, sezione "Piani da un brief".

## Gate di Push — Verifica obbligatoria

⛔ Prima di qualsiasi `git push`, esegui il comando di verifica dichiarato dall'istruzione di dominio del progetto (lint, formattazione, analisi statica).

| Exit code | Azione |
|-----------|--------|
| `0` | Verifica pulita — push consentita |
| Non-zero | **BLOCCA la push** — segnala le violazioni |

In caso di blocco:
1. Elenca i file con violazioni (dall'output del comando)
2. Chiedi conferma prima di applicare correzioni automatiche
3. Riesegui il check, poi procedi con la push

Nessun comando di verifica applicabile al progetto (per esempio un repository di soli documenti) → **dichiaralo esplicitamente nell'output** prima della push. L'assenza di target si dichiara, non si salta in silenzio.

> Regola assoluta: nessun `git push` senza verifica pulita o assenza di target dichiarata.

*Template v2.0 - agnostico dallo stack - Token-optimized for AI agents* - Last Update 2026-09-24 — claude-opus-5-5
