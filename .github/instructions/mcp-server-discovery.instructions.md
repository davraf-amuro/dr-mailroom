---
applyTo: "**"
---

# MCP Server Discovery & Creation

## Scopo

Questa istruzione si applica ogni volta che un task richiede l'uso di un **MCP server** (Model Context Protocol server) per accedere a strumenti, risorse o capacità esterne all'agente.

---

## Flusso obbligatorio

### 1. Cerca prima di creare

Prima di proporre la creazione di un nuovo MCP server, l'agente **deve cercarlo** in tutti i contesti accessibili, nell'ordine seguente:

1. **Tool già esposti nella sessione** — se i tool del server compaiono tra quelli disponibili, il server è attivo a prescindere da dove è registrato
2. **Configurazione MCP del progetto corrente** — `.mcp.json` (radice progetto) e `.vscode/mcp.json` (chiave `servers`)
3. **Settings locali del progetto corrente** — `.claude/settings.json` / `.claude/settings.local.json` (chiave `mcpServers`)
4. **Settings globali utente** — `~/.claude/settings.json` / `~/.claude/settings.local.json`
5. **Repository workspace** — altri progetti aperti nell'ambiente corrente (working directories elencate nell'environment)
6. **MCP servers già registrati nell'IDE** — configurazioni VS Code, JetBrains o altri ambienti attivi
7. **Server della suite `dr-*`** — elencati nella sezione [Server già disponibili nella suite](#server-già-disponibili-nella-suite): se uno copre il bisogno, si propone quello
8. **Registry pubblici noti** — se si ha accesso a internet, cerca nel [MCP Registry ufficiale](https://github.com/modelcontextprotocol/servers) o repository GitHub/npm correlati

Se il server viene trovato in uno qualsiasi di questi punti:
- **Proponi il server trovato** all'utente, indicando dove si trova e come configurarlo per il progetto corrente.
- Non procedere alla creazione.

---

### Server già disponibili nella suite

Server MCP pubblicati accanto ai pacchetti `dr-*`. Questa tabella è la fonte unica del collegamento fra nome del server e repository: prompt, skill e README rimandano qui.

| Nome del server | Repository | Quando proporlo |
|---|---|---|
| `db-schema` | [davraf-amuro/dr-mcp-dbschema](https://github.com/davraf-amuro/dr-mcp-dbschema) | Il task richiede di leggere la struttura di un database **SQL Server** (tabelle, viste, colonne), interrogarlo in sola lettura o preparare comandi DDL. È il server che `dr-efdb` suggerisce nel catalogo (`mcpSuggestion`) e che lo scaffolding CRUD usa per leggere i campi dell'entità |

**Installazione di `db-schema`.** Non si scrive la configurazione a mano: la scrive lo `setup.ps1` del repository, eseguito una volta nella radice del progetto. Lo script scarica il binario dall'ultima release e ne verifica il checksum, registra il server col nome `db-schema` nel file del client scelto e crea `appsettings.local.json` con le sezioni da compilare.

```powershell
# Claude Code (.mcp.json)
irm https://raw.githubusercontent.com/davraf-amuro/dr-mcp-dbschema/main/setup.ps1 | iex

# VS Code + GitHub Copilot (.vscode/mcp.json)
Invoke-Expression "& { $(irm https://raw.githubusercontent.com/davraf-amuro/dr-mcp-dbschema/main/setup.ps1) } -Client vscode"

# Tutti i client
Invoke-Expression "& { $(irm https://raw.githubusercontent.com/davraf-amuro/dr-mcp-dbschema/main/setup.ps1) } -Client all"
```

Prima di proporlo, dichiara i limiti: `setup.ps1` installa solo il binario `win-x64` (le release pubblicano anche `linux-x64`, ma per quello non c'è una procedura documentata: su Linux va detto all'utente, non improvvisato) e il database deve essere SQL Server raggiungibile in rete. Le operazioni DDL e `run_select` sono disattivate finché non si abilitano nella sezione `Ddl` dell'`appsettings`. Variabili, flag DDL, file da aggiungere al `.gitignore` (`tools/dr-mcp-dbschema/` e i file di configurazione del client) e risoluzione dei problemi: README di `dr-mcp-dbschema`, non questa regola.

L'installazione modifica il progetto (binario in `tools/`, file di configurazione del client): si propone e si esegue solo dopo la conferma dell'utente.

---

### 2. Proponi la creazione se non trovato

Se la ricerca non produce risultati:
- **Comunica esplicitamente** che nessun MCP server adatto è stato trovato.
- **Proponi di crearne uno nuovo**, descrivendo:
  - Nome suggerito (es. `mcp-<dominio>`)
  - Strumenti/risorse che esporrebbe
  - Tecnologia consigliata (TypeScript SDK `@modelcontextprotocol/sdk` oppure Python SDK `mcp`)
- **Prima di procedere**, aspetta conferma dell'utente.

---

### 3. Ingaggia `/dr-warroom` prima di progettare

Se l'utente approva la creazione, **è obbligatorio invocare la skill `/dr-warroom`** con la domanda:

```
/dr-warroom Come dovremmo progettare il server MCP "<nome>" per <obiettivo>?
  Considerare: strumenti esposti, autenticazione, trasporto (stdio/SSE/HTTP),
  naming conventions, struttura del repository.
```

Aspetta l'output del warroom prima di scrivere qualsiasi codice.

---

### 4. Proponi un repository Git dedicato

Dopo la creazione del server MCP (o alla sua approvazione finale):
- **Proponi di pubblicarlo in un repository Git dedicato**, separato dal progetto che lo consuma.
- Il repository deve seguire la convenzione di naming: `mcp-<dominio>` (es. `mcp-portainer`, `mcp-db-schema`).
- Suggerisci di aggiungere il repository come **submodule** nel progetto consumatore, se necessario.
- Segnala che il repository dovrebbe includere:
  - `README.md` con la guida all'installazione e configurazione
  - File di configurazione esempio per `.claude/settings.json`
  - Istruzioni di build/publish

---

## Regole di perimetro

- Non saltare la fase di ricerca nemmeno se il task sembra semplice o urgente.
- Non creare un MCP server inline nel progetto consumatore: i server MCP sono artefatti autonomi.
- Non proporre l'uso di un server trovato senza prima verificare che sia compatibile con il task corrente (strumenti esposti, versione del protocollo, sicurezza).
- Se il warroom produce indicazioni in conflitto tra loro, segnalale all'utente e chiedi una decisione prima di procedere.

---

## Comportamento di fallback

Se non è possibile accedere ad alcuna fonte di ricerca (es. nessun accesso internet, nessun settings file leggibile):
- Dichiara esplicitamente il limite.
- Proponi comunque la creazione, partendo dalla sessione warroom.

---

*Istruzione v1.3 - MCP Server Discovery - 2026-10-16 — claude-opus-5-5 — sezione "Server già disponibili nella suite": `db-schema` → `davraf-amuro/dr-mcp-dbschema`, installazione con il suo `setup.ps1` (davraf-amuro/dr-guidelines#27); limite `win-x64` di `setup.ps1` precisato*
