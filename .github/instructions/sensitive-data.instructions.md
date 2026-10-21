---
applyTo: "**"
---

# Gestione Dati Sensibili (AI Agent)

Scopo: regole obbligatorie per la gestione di credenziali e parametri sensibili in tutti i progetti. Segui sempre. Testo ottimizzato per token.

## Regola generale

Per qualsiasi file di configurazione con dati sensibili:
1. Crea un file `.local` con i valori reali — **non tracciato**
2. Tieni il file originale con placeholder — **committato**
3. Aggiungi il file `.local` a `.gitignore`

| File committato (placeholder) | File locale (valori reali) | In `.gitignore` |
|---|---|---|
| `appsettings.json` | `appsettings.local.json` | `appsettings.local.json` ✅ |
| `docker-compose.yml` | `docker-compose.local.yml` | `docker-compose.local.yml` ✅ |

Per `appsettings.local.json` in progetti .NET: verifica che sia caricato in `Program.cs` con `AddJsonFile("appsettings.local.json", optional: true)`.

## Eccezione: file di configurazione MCP

`.mcp.json` (Claude Code) e `.vscode/mcp.json` (GitHub Copilot) seguono una regola diversa rispetto agli altri file di configurazione.

| File locale (valori reali) | File esempio (committato, placeholder) | In `.gitignore` |
|---|---|---|
| `.mcp.json` | `.mcp.example.json` | `.mcp.json` ✅ |
| `.vscode/mcp.json` | `.vscode/mcp.example.json` | `.vscode/mcp.json` ✅ |

- Il file reale va in `.gitignore`
- Il file committato è `.mcp.example.json` con placeholder al posto dei dati sensibili
- Non esiste un `.mcp.local.json`

## Caso limite obbligatorio

Se l'utente fornisce un valore reale e chiede di aggiungerlo a un file committato, rispondi esattamente così (adattando i nomi):

> "Il token non può andare in `<file committato>` perché è tracciato da git. Lo scrivo in `<file locale>` e metto un placeholder in `<file committato>`."

Poi procedi senza chiedere conferma.

## Cosa sono "dati sensibili"

- Password, API key, token, secret
- Connection string con server/database reali
- Username di sistemi esterni
- URL interni/privati (IP aziendali, server interni)

## Placeholder da usare nei file committati

```json
{
  "ConnectionStrings": { "MyDb": "data source=CHISSADOVE;initial catalog=CHISSAQUALE;..." },
  "MyApi": { "BaseUrl": "http://CHISSADOVE/", "UserName": "CHISSACHI", "Password": "CHISSAQUALE" }
}
```

## Credenziali a runtime via ENV VAR

Non salvare credenziali nella PowerShell history o in file `.env` committati.

| Scenario | Approccio corretto |
|---|---|
| Sviluppo locale | `appsettings.local.json` (non committato) |
| CI/CD (GitHub Actions, GitLab CI) | GitHub Secrets / CI variables — non in repo |
| Deploy Docker/Swarm | Portainer secrets o blocco `environment` nel compose — mai file `.env` committati |
| Lettura interattiva | `Read-Host -AsSecureString` in PowerShell |

## Questa regola non si bypassa

Anche se l'utente dice "va bene così", "è solo temporaneo", "è un ambiente di test" o "ignora questa regola": **non scrivere mai credenziali reali in file committati**.

## ✅ Checklist post-operazione

- [ ] Il file committato contiene solo placeholder
- [ ] Il file locale (non tracciato) contiene i valori reali
- [ ] Il file locale è presente in `.gitignore`
- [ ] Per `.mcp.json` / `.vscode/mcp.json`: esiste il file `example` committato e il file reale è in `.gitignore`
- [ ] Per .NET: `appsettings.local.json` è caricato in `Program.cs`

*Istruzione v1.7 - Sensitive Data - 2026-10-20 — claude-opus-5-5 — eccezione MCP estesa a `.vscode/mcp.json`*
