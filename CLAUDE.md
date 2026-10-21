<!-- dr-guidelines -->
## Lingua
- Rispondi sempre in **italiano**

## Comportamento generale
- Termine tecnico errato o impreciso → segnala gentilmente + forma corretta

## Regola fondamentale — Agente primario e compatibilità

⛔ **Claude Code è l'agente primario**: ogni regola, istruzione, convenzione o linea guida creata o modificata deve funzionare con Claude Code.

GitHub Copilot e Codex sono **secondari, best-effort**:
- Nei file condivisi preferire sintassi e struttura neutra, leggibile da tutti gli agenti
- Feature esclusiva di Claude Code in un file condiviso → ammessa solo con l'alternativa per gli altri agenti accanto, oppure con il limite dichiarato (es. "Su Copilot: <alternativa>" / "Su Copilot non disponibile")
- Compatibilità secondaria costosa o incerta → **dichiara il limite e procedi**: non blocca il lavoro

> **Artefatti specifici per agente.** `.claude/skills/` (Claude Code) usa `$ARGUMENTS`, sub-agenti, `AskUserQuestion`, `EnterPlanMode` *by design*. `.agents/skills/` e `AGENTS.md` (Codex) sono **copie generate** da quelli Claude nel repo `dr-guidelines` con `scripts/sync-codex.ps1` e non si modificano a mano; nei progetti host l'installer porta `.agents/skills/`, non `AGENTS.md`. Restano condivisi `.github/instructions/*.md`, `.github/prompts/*.prompt.md` e ogni altra regola/documento non specifico di un agente.

## Standard di progetto (trasversali)
@.github/copilot-instructions.md

## Regola MCP Server
@.github/instructions/mcp-server-discovery.instructions.md

## Modifiche al codice

⛔ STOP — Prima di scrivere codice, completa tre passi e documentali nell'output:

1. **Leggi** `.github/copilot-instructions.md` e **cita** sezione rilevante per task corrente.
2. **Identifica e leggi** file `.github/instructions/*.md` pertinente. Se incerto, elenca file disponibili e scegli.
3. **Dichiara** scope, file da modificare e cosa NON toccare — formato obbligatorio:
   > "Modificherò `[file]` per `[motivo]`. Non toccherò `[fuori scope]`."

No procedere finché tre passi non completati e visibili nell'output.

⛔ OBBLIGO DI RENDICONTO — Prima di scrivere codice, elenca nell'output tutti i file letti:

```
File letti:
- .github/copilot-instructions.md  ✓
- .github/instructions/<istruzione-pertinente>.instructions.md  ✓
```

File non letto che andava letto → dichiara `✗ non letto` e leggilo prima di procedere. No elenco visibile = no procedere.

---

### Checklist pre-task (obbligatoria)

Fonte unica: `dev-cycle.instructions.md` — Fase 0. Compila quella checklist nell'output prima di ogni task. Anche una sola risposta NO → fermati e completa il passo mancante prima di procedere.

---

## Piano obbligatorio su disco

⛔ Task con **≥ 2 operazioni** → piano su disco approvato prima di agire. Fonte unica: `plan-tracking.instructions.md` (soglia, definizione di "operazione", template, branch, verifica finale). Passi su Claude Code:

1. Crea `.ai/plans/<YYYY-MM-DD>-<slug>/plan.md` (template di plan-tracking)
2. `EnterPlanMode`: proponi il piano con scope, perimetro negativo e proposta di branch dedicato (`plan/<slug>`); la scelta dell'utente va nel campo `Branch:`
3. `ExitPlanMode` = richiesta di approvazione esplicita; senza approvazione non si procede
4. Durante l'esecuzione marca `[x]` ogni fase; a fine task verifica ogni criterio → `Stato: COMPLETATO` e dichiara `"Piano [slug] verificato. Tutti i criteri soddisfatti."`

Operazione singola → dichiarazione inline (dev-cycle Fase 1), nessun piano. Piano `IN CORSO` in `.ai/plans/` all'avvio sessione → riprendi dall'ultima fase incompleta.

**Esenzioni** (nessun `EnterPlanMode`):
- Cartella `.ai/` — piani e file di contesto si scrivono senza blocchi
- Skill invocate **esplicitamente** dall'utente con `/nome` (es. `/dr-promote-to`) — l'invocazione è l'approvazione; la skill segue le proprie conferme interne. **Non vale** per le skill avviate da una frase (vedi "Invocazione automatica") né quando l'argomento è un brief

## Brief dell'utente — cartella `briefs/`

Brief indicato dall'utente → piano **sempre**, anche per una sola operazione, con link al brief sotto il titolo; esecuzione solo dopo approvazione esplicita. Vale anche quando il brief è l'argomento di una skill. Regola completa: `plan-tracking.instructions.md`, sezione "Piani da un brief".

## Citazione fonti e modello

Fine risposta, se letti file o consultati documenti:
- Cita file usati come fonti (path relativo)
- Indica modello LLM usato (es. `claude-opus-5-5`)

## Invocazione automatica delle skill

Intento utente corrisponde a skill disponibile → **invoca direttamente** senza conferma. Usa contesto conversazione come argomento.

⛔ Eccezione — invocazione dedotta da una frase (non `/nome` esplicito) di una skill che fa azioni **irreversibili o esterne** (commit, push, merge, cancellazione di branch, issue su GitHub, scrittura fuori dal repository: `/dr-promote-to`, `/dr-file-feedback`, `/dr-install-global`, `/dr-scaffold*`) → prima conferma in una riga cosa farà (es. "Eseguo commit + push + PR verso `main`?"), poi invoca. Skill che scrivono più file (`/dr-professor`, `/dr-handoff`) partite da una frase seguono la soglia del piano.

Skill segnate *(se installata)* arrivano da un pacchetto di dominio: assente → dillo all'utente e indica il pacchetto (`/dr-scaffold-guidelines`).

| Se l'utente dice qualcosa come... | Invoca |
|-----------------------------------|--------|
| "vai professor", "scrivi la doc", "aggiorna il README", "genera la scheda del progetto", "documenta gli endpoint", "prepara l'onboarding" | `/dr-professor [richiesta]` |
| "consulta il warroom", "sentiamo le opinioni", "apri il tavolo", "cosa ne pensano gli esperti", "discutiamo questa scelta" | `/dr-warroom [domanda o contesto]` |
| "chiedi al tattico", "rivedi questo prompt", "migliora il prompt", "scrivi un prompt per", "perché questo prompt non funziona" | `/dr-prompt-engineer [prompt o descrizione]` |
| "pianifica il rilascio", "prepara l'ambiente", "come si deploya", "configura Docker", "procedura di deploy" | `/dr-tech [task]` |
| "promote", "promuovi il branch", "crea la PR verso", "merge su", "porta su master/main/staging" | `/dr-promote-to [target-branch] [--merge] [--delete]` |
| "audit api", "fai l'audit del backend", "analizza le api", "cerca dead code", "controlla il codice backend" | `/dr-audit-api [focus opzionale]` *(se installata: dr-dotnet-backend)* |
| "audit frontend", "fai l'audit del fe", "analizza il frontend", "controlla i componenti" | `/dr-audit-fe [focus opzionale]` *(se installata: dr-fe)* |
| "aggiorna i pacchetti", "aggiorna le linee guida", "get-latest" | `/dr-get-latest` |
| "segnala una miglioria", "apri una issue", "questa regola non va bene", "manca un pacchetto per" | `/dr-file-feedback [descrizione]` |
| "controlla le issue", "pianifica le issue", "crea i piani dalle issue", "cosa c'è nelle issue aperte" | `/dr-issues-to-plans [numeri] [--repo owner/nome] [--pacchetti]` |
| "verifica il piano", "controlla che il piano sia stato rispettato", "verifica finale" | `/dr-verify-plan` |
| "modifica testi", "riscrivi il testo", "correggi il testo", "migliora la descrizione", "aggiorna la descrizione" (di documenti in `docs/` o README) | `/dr-professor [richiesta]` |
| "passa le consegne", "handoff", "prepara il passaggio di consegne", "documento di passaggio" | `/dr-handoff` |
| "aggiorna snapshot", "refresh contesto", "rigenera il riassunto", "snapshot del progetto", "aggiorna il contesto del progetto" | `/dr-snapshot` |
| "genera i profili di avvio", "crea launch.json", "configura il debug VS Code", "launch profiles" | `/dr-create-launch-profiles [profili]` |
| "crea una nuova solution", "parti da zero", "scaffolding", "nuovo progetto", "crea il workspace" | `/dr-scaffold [richiesta]` |
| "aggiungi un progetto", "aggiungi una minimal api", "aggiungi un worker", "aggiungi il frontend" | `/dr-scaffold-project [tipologia e nome]` |
| "installa le linee guida qui", "aggiungi i pacchetti dr-*", "quali pacchetti mi servono" | `/dr-scaffold-guidelines` |
| "installa le linee guida globali", "aggiorna il CLAUDE.md globale", "linee guida su tutto il PC" | `/dr-install-global [installa\|aggiorna]` |

Invoca skill → passa tutto contesto utile già in conversazione (codice aperto, domanda originale, file citati) — no chiedere all'utente di ripetere.
<!-- /dr-guidelines -->

