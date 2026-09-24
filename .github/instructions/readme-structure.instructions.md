---
applyTo: "README.md"
---

# Struttura README — dr-guidelines

Questo file definisce la struttura obbligatoria del `README.md` di questo repository (`dr-guidelines`, pacchetto core della suite `dr-*`).
Quando crei o aggiorni il README, rispetta esattamente questa struttura. Non aggiungere sezioni non previste. Non rimuovere sezioni esistenti.

---

## Struttura obbligatoria

Il README deve contenere queste sezioni, in questo ordine:

| # | Sezione | Emoji | Scopo |
|---|---------|-------|-------|
| 1 | Titolo + tagline | — | Nome repo + una riga che spiega cos'è |
| 2 | Pacchetti dr-* disponibili | 🧩 | Tabella dei 7 pacchetti della suite (core + domini), repo e scope |
| 3 | Avvio Rapido — Nuovo Progetto | 🚀 | Come usarlo su un progetto nuovo |
| 4 | Progetto Esistente — Aggiungere le Guidelines | 🔧 | Come installare `dr-guidelines` (e opzionalmente altri pacchetti) su un progetto già esistente |
| 5 | Cosa viene configurato | 📦 | Tabella dei file installati da `dr-guidelines-install.ps1` (pacchetto core) |
| 6 | Aggiornare le Guidelines | 🔄 | Come aggiornare i pacchetti installati |
| 7 | Istruzioni Modulari (Copilot / Claude) | 🤖 | Tabella dei file `.instructions.md` presenti **nel core** |
| 8 | Claude Code Skills | 🤖 | Una voce per ogni skill in `.claude/skills/` **del core** |
| 9 | MCP Servers | 🔌 | Una voce per ogni server in `.mcp.json` |
| 10 | Documentazione | 📄 | Tabella di tutti i file `.md` in `docs/` con descrizione |
| 11 | FAQ | ❓ | Domande frequenti in formato Q/A |
| 12 | Footer | — | `*Documento aggiornato: Mese Anno — Revisione vN — YYYY-MM-DD — modello*` |

---

## Regole per sezione

### 1 — Titolo + tagline
- H1 con il nome del repository
- Una sola riga di descrizione, concreta e diretta

### 2 — Pacchetti dr-* disponibili
- Tabella con colonne: `Pacchetto | Repo | Scope`
- Una riga per ciascuno dei 7 pacchetti della suite (`dr-guidelines` core + i 6 pacchetti dominio), incluso quello corrente
- Link markdown al repo GitHub per ciascun pacchetto (`[dr-nome](https://github.com/davraf-amuro/dr-nome)`)
- Nota eventuali dipendenze tra pacchetti (es. `dr-minimalapi`/`dr-winsvc` → `dr-dotnet-backend`, installata automaticamente)
- Aggiorna la tabella se un pacchetto viene aggiunto, rinominato o rimosso dalla suite

### 3 — Avvio Rapido (nuovo progetto)
- Lo scaffolding di un progetto nuovo **non è uno script**: è guidato dall'agente AI. Indica l'invocazione `/dr-scaffold` (Claude Code) e rimanda a `.github/prompts/dr-scaffold.prompt.md` per GitHub Copilot
- Elenca i passi del flusso (lista numerata, breve): gate prerequisiti → rilevamento stato cartella → raccolta risposte → dry-run + conferma unica → esecuzione
- Cita `scaffolding-catalog.json` come fonte delle tipologie di progetto e dei pacchetti disponibili
- Rimanda ai `docs/scaffolding-*.md` per la struttura generata, senza duplicarne il contenuto
- Non descrivere script di bootstrap dismessi né il modello a submodule

### 4 — Progetto Esistente
- Comando PowerShell:
  ```powershell
  irm https://raw.githubusercontent.com/davraf-amuro/dr-guidelines/main/dr-guidelines-install.ps1 | iex
  ```
- Una nota su cosa fa `dr-guidelines-install.ps1` sui file già presenti (comportamento non distruttivo, skip-se-esiste)
- Un rimando alla sezione 2 per installare anche i pacchetti dominio pertinenti allo stack del progetto
- Finché i repo sono Private, la variante `gh api` — `raw.githubusercontent.com` risponde `404` e il comando sopra da solo non basta:
  ```powershell
  & ([scriptblock]::Create((gh api repos/davraf-amuro/dr-guidelines/contents/dr-guidelines-install.ps1 -H "Accept: application/vnd.github.raw" | Out-String)))
  ```
- Gli installer si chiamano `<pacchetto>-install.ps1`, mai `install.ps1`: il nome porta il pacchetto

### 5 — Cosa viene configurato
- Tabella con colonne: `File/Cartella | Provenienza | Scopo`
- Aggiorna la tabella se `dr-guidelines-install.ps1`/`dr-guidelines-install-lib.ps1` cambia i file che installa

### 6 — Aggiornare le Guidelines
- Comando per aggiornare un singolo pacchetto:
  ```powershell
  & ([scriptblock]::Create((irm https://raw.githubusercontent.com/davraf-amuro/dr-guidelines/main/dr-guidelines-install.ps1))) -Update
  ```
- Nota sulla skill `/dr-get-latest`: aggiorna in un colpo solo tutti i pacchetti tracciati in `.ai/dr-guidelines-packages.json`
- Nessun submodule, nessun comando `git submodule update` — il modello di distribuzione non usa più submodule

### 7 — Istruzioni Modulari
- Tabella con colonne: `File | Quando usarlo`
- Una riga per ogni file `.instructions.md` presente in `.github/instructions/` **di questo repo** (contenuto core, trasversale — le istruzioni specifiche di dominio vivono nei rispettivi pacchetti, vedi sezione 2)
- Aggiorna la tabella quando aggiungi o rimuovi file instruction

### 8 — Claude Code Skills
- Una voce H3 per ogni file in `.claude/skills/` **di questo repo**
- Per ogni skill: descrizione breve, comando d'invocazione, esempio d'uso
- Aggiorna questa sezione quando aggiungi o rimuovi skill

### 9 — MCP Servers
- Una voce H3 per ogni server definito in `.mcp.json`
- Per ogni server: prerequisito di installazione + snippet `.mcp.json` da copiare
- Chiudi sempre con: `Poi riavvia Claude Code per caricare il server.`

### 10 — Documentazione
- Tabella con colonne: `File | Contenuto`
- Una riga per ogni file `.md` in `docs/` (escludi `*-wiki.md` — non committati)
- Link markdown clickable: `[docs/nome.md](docs/nome.md)`
- Aggiorna la tabella quando aggiungi o rimuovi file in `docs/`

### 11 — FAQ
- Formato: `### Q: domanda` / `**A:** risposta`
- Aggiungi una voce quando emerge una domanda ricorrente
- Non rimuovere voci esistenti senza motivo esplicito

### 12 — Footer
- Formato esatto: `*Documento aggiornato: Mese Anno — Revisione vN — YYYY-MM-DD — modello-llm*`
- Aggiorna mese/anno, numero revisione, data e modello ad ogni modifica significativa
- Incrementa la revisione di 0.1 per modifiche normali, di 1.0 per ristrutturazioni (stessa logica di `doc-versioning.instructions.md`)

---

## Quando aggiornare il README

| Evento | Cosa aggiornare |
|--------|-----------------|
| Nuovo pacchetto aggiunto/rimosso dalla suite | Sezione "Pacchetti dr-* disponibili" |
| Nuova skill aggiunta in `.claude/skills/` | Sezione "Claude Code Skills" |
| Nuovo file instruction in `.github/instructions/` | Sezione "Istruzioni Modulari" |
| Nuovo MCP server in `.mcp.json` | Sezione "MCP Servers" |
| `dr-guidelines-install.ps1`/`dr-guidelines-install-lib.ps1` installa nuovi file | Tabella "Cosa viene configurato" |
| Nuovo file in `docs/` | Sezione "Documentazione" |
| Nuova domanda frequente | Sezione FAQ |

*Template v2.2 - dr-guidelines - Last Update 2026-08-09 11:40 — claude-opus-5 — installer rinominati <pacchetto>-install.ps1, aggiunta la variante gh api per i repo Private*
