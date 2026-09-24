<!-- dr-guidelines -->
## Lingua
- Rispondi sempre in **italiano**

## Comportamento generale
- Termine tecnico errato o impreciso → segnala gentilmente + forma corretta

## Regola fondamentale — Compatibilità duale agente

⛔ OGNI regola, istruzione, convenzione o linea guida creata o modificata in questo progetto **deve essere compatibile sia con Claude Code che con GitHub Copilot**.

- Preferire sintassi e struttura neutra, leggibile da entrambi
- No feature esclusive di un solo tool
- Verifica compatibilità prima di proporre o applicare regola
- Compatibilità non garantita → **fermati, chiedi all'utente** — no assunzioni, no azione autonoma

> **Esenzione — Claude Code Skills.** I file in `.claude/skills/` sono artefatti specifici di Claude Code *by design* (usano `$ARGUMENTS`, sub-agenti, `AskUserQuestion`, `EnterPlanMode`) e non sono portabili su GitHub Copilot: sono **esenti** da questa regola. La compatibilità duale resta obbligatoria per `.github/instructions/*.md`, `.github/prompts/*.prompt.md` e ogni altra regola/documento condiviso.

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
- .github/instructions/database-provider.instructions.md  ✓
```

File non letto che andava letto → dichiara `✗ non letto` e leggilo prima di procedere. No elenco visibile = no procedere.

---

### Checklist pre-task (obbligatoria)

Fonte unica: `dev-cycle.instructions.md` — Fase 0. Compila quella checklist nell'output prima di ogni task. Anche una sola risposta NO → fermati e completa il passo mancante prima di procedere.

---

⛔ Task con ≥ 2 operazioni (stessa soglia di `plan-tracking.instructions.md`) richiede piano approvato:

1. Usa `EnterPlanMode` per proporre piano
2. Dichiara: scope, file da modificare, motivazione, perimetro negativo
3. Attendi approvazione esplicita utente
4. Usa `ExitPlanMode` per procedere

Operazione singola → dichiarazione inline (dev-cycle Fase 1), nessun piano richiesto.

**Esenzioni** (nessun `EnterPlanMode` richiesto):
- Cartella `.ai/` — piani e file di contesto si scrivono senza blocchi
- Skill invocate esplicitamente dall'utente (es. `/dr-promote-to`, `/dr-professor`) — l'invocazione è l'approvazione; la skill segue i propri passi e le proprie conferme interne

## Piano obbligatorio su disco

⛔ OGNI task con ≥ 2 operazioni richiede piano persistito su disco **prima** di EnterPlanMode.

Segui `plan-tracking.instructions.md`:
1. Crea `.ai/plans/<YYYY-MM-DD>-<slug>/plan.md` con obiettivo, scope, fasi, criteri di verifica
2. Entra in EnterPlanMode e proponi piano all'utente
3. Durante esecuzione, marca `[x]` ogni fase completata nel piano
4. A task completato, verifica ogni criterio → aggiorna `Stato: COMPLETATO`
5. Dichiara: `"Piano [slug] verificato. Tutti i criteri soddisfatti."`

Piano `IN CORSO` in `.ai/plans/` all'avvio sessione → riprendi da ultima fase incompleta.

## Citazione fonti e modello

Fine risposta, se letti file o consultati documenti:
- Cita file usati come fonti (path relativo)
- Indica modello LLM usato (es. `claude-sonnet-4-6`)

## Invocazione automatica delle skill

Intento utente corrisponde a skill disponibile → **invoca direttamente** senza conferma. Usa contesto conversazione come argomento.

| Se l'utente dice qualcosa come... | Invoca |
|-----------------------------------|--------|
| "vai professor", "scrivi la doc", "aggiorna il README", "genera la scheda del progetto", "documenta gli endpoint", "prepara l'onboarding" | `/dr-professor [richiesta]` |
| "consulta il warroom", "sentiamo le opinioni", "apri il tavolo", "cosa ne pensano gli esperti", "discutiamo questa scelta" | `/dr-warroom [domanda o contesto]` |
| "chiedi al tattico", "rivedi questo prompt", "migliora il prompt", "scrivi un prompt per", "perché questo prompt non funziona" | `/dr-prompt-engineer [prompt o descrizione]` |
| "pianifica il rilascio", "prepara l'ambiente", "come si deploya", "configura Docker", "procedura di deploy" | `/dr-tech [task]` |
| "promote", "promuovi il branch", "crea la PR verso", "merge su", "porta su master/main/staging" | `/dr-promote-to [target-branch] [--merge] [--delete]` |
| "audit api", "fai l'audit del backend", "analizza le api", "cerca dead code", "controlla il codice backend" | `/dr-audit-api [focus opzionale]` |
| "audit frontend", "fai l'audit del fe", "analizza il frontend", "controlla i componenti" | `/dr-audit-fe [focus opzionale]` |
| "aggiorna i pacchetti", "aggiorna le linee guida", "get-latest" | `/dr-get-latest` |
| "segnala una miglioria", "apri una issue", "questa regola non va bene", "manca un pacchetto per" | `/dr-file-feedback [descrizione]` |
| "controlla le issue", "pianifica le issue", "crea i piani dalle issue", "cosa c'è nelle issue aperte" | `/dr-issues-to-plans [numeri] [--repo owner/nome] [--pacchetti]` |
| "verifica il piano", "controlla che il piano sia stato rispettato", "verifica finale" | `/dr-verify-plan` |
| "modifica testi", "aggiorna commenti", "riscrivi il testo", "correggi il testo", "migliora la descrizione", "aggiorna la descrizione", "modifica il commento" | `/dr-professor [richiesta]` |
| "aggiorna snapshot", "refresh contesto", "rigenera il riassunto", "snapshot del progetto", "aggiorna il contesto del progetto" | `/dr-snapshot` |
| "genera i profili di avvio", "crea launch.json", "configura il debug VS Code", "launch profiles" | `/dr-create-launch-profiles [profili]` |
| "crea una nuova solution", "parti da zero", "scaffolding", "nuovo progetto", "crea il workspace" | `/dr-scaffold [richiesta]` |
| "aggiungi un progetto", "aggiungi una minimal api", "aggiungi un worker", "aggiungi il frontend" | `/dr-scaffold-project [tipologia e nome]` |
| "installa le linee guida qui", "aggiungi i pacchetti dr-*", "quali pacchetti mi servono" | `/dr-scaffold-guidelines` |
| "installa le linee guida globali", "aggiorna il CLAUDE.md globale", "linee guida su tutto il PC" | `/dr-install-global [installa\|aggiorna]` |

Invoca skill → passa tutto contesto utile già in conversazione (codice aperto, domanda originale, file citati) — no chiedere all'utente di ripetere.
<!-- /dr-guidelines -->

