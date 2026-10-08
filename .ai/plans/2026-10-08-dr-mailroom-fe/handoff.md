# Handoff — creazione di dr-mailroom-fe

Per chi riprende il lavoro sul frontend, in una nuova sessione aperta dal workspace `E:\Davide\Progetti\dr-mailroom.code-workspace`. Questa nota basta da sola: non serve la conversazione che l'ha prodotta.

## Stato al 2026-10-08

| Cosa | Stato |
|---|---|
| Piano | [plan.md](plan.md), **`Stato: PROPOSTO`**. Non è approvato: nessuna fase eseguita |
| Design | [design.md](design.md) — direzione "Hangar Federale", ispirata all'estetica EFSF di Gundam, senza asset protetti |
| Workspace | `E:\Davide\Progetti\dr-mailroom.code-workspace` creato con le cartelle `api · dr-mailroom` e `fe · dr-mailroom-fe`. La seconda **non esiste ancora**: la crea la Fase 2 |
| Repo `dr-mailroom-fe` | da creare (Fasi 2–4) |
| Backend `dr-mailroom` | nessun remote git; `davraf-amuro/dr-mailroom` non esiste su GitHub. L'utente sta chiudendo il lavoro qui prima di passare al frontend |

## Prima di eseguire

1. Fatti approvare il piano dall'utente in modo esplicito. Solo dopo porta `Stato:` a `IN CORSO`.
2. Chiudi le domande aperte del piano:
   - **D-1**: il repo FE è privato o pubblico?
   - **D-2**: il backend va pubblicato su GitHub prima? È un'operazione fuori da questo piano.
   - **D-3**: si conferma il font Saira?
3. Controlla i piani `IN CORSO` del backend (`2026-10-02-mailroom-gmail-reader`, `2026-10-08-stop-worker-endpoint`). Le regole vietano di eseguire un nuovo piano mentre un altro è `IN CORSO`: chiedi all'utente se sono chiusi, archiviati o da completare.
4. Le Fasi 6 e 16 richiedono il backend avviato su `http://localhost:5180`, con DB raggiungibile e una chiave API valida in `public/config.json`.

## Insidie note

- **`create-vue` va eseguito prima del core `dr-guidelines`**, perché scrive i propri `.editorconfig`, `.gitignore` e `.gitattributes`. Su PowerShell il `--` va quotato: `'--'`.
- **TypeScript resta alla `~6.0.0`**: con TS 7 `vue-tsc` non funziona.
- **Lo script `lint` di create-vue usa `--fix`**, quindi non vale come gate. Il gate è `lint:check`, aggiunto nella Fase 5.
- **Usa `@lucide/vue`, non `lucide-vue-next`**, che è deprecato. Niente `@vueuse/motion` (fermo dal 2025) né `@headlessui/vue` (fermo dal 2024).
- **`autoRefetch` di Pinia Colada non mette in pausa il polling a scheda nascosta**: va combinato con `enabled` legato a `useDocumentVisibility`.
- **`motion-v` ha `reducedMotion="never"` di default**: va impostato `"user"` in `MotionConfig`.
- **Il giallo `#FCE300` non va mai usato come colore del testo.** I contrasti di design.md sono stime: vanno misurati nella Fase 7.
- **Nessun nome, logo o sagoma protetti** in `src/`: la verifica finale fa un grep.
- **Nessun `git push` dall'esecutore**: la fa l'utente oppure `/dr-promote-to`.

## Fonti di verità

- Cosa deve fare il frontend: `dr-mailroom/docs/frontend-specifiche.md`
- Organizzazione del codice: `frontend-organization.instructions.md` del pacchetto `dr-fe` (arriva nel repo FE con la Fase 4)
- Contratto API: `http://localhost:5180/openapi/v1.json`, documentazione interattiva su `/scalar/v1`

*Handoff v1.0 — 2026-10-08 — claude-opus-5-5*
