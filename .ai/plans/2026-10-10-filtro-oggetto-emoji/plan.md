# Piano: "Inizia con" ignora emoji/simboli iniziali + testo oggetto residuo nel costruttore
Data: 2026-10-10
Stato: COMPLETATO

> Bugfix puntuale approvato esplicitamente dall'utente in chat ("procedi con A"), eseguito in parallelo al piano `2026-10-08-dr-mailroom-fe` (IN CORSO, fase 14): perimetro disgiunto.

## Obiettivo
`StartsWith` deve trovare "📄Riepilogo spese telepedaggio…" con valore "Riepilogo spese telepedaggio"; scegliere "Nessuna condizione" non deve lasciare un testo nascosto che blocca la validazione.

## Diagnosi
- Anteprima reale: mittente `no-reply@mycicero.it` → 52; + `StartsWith "Riepilogo spese telepedaggio"` → 0. L'oggetto inizia con U+1F4C4: `StartsWith` ordinale fallisce; il `LIKE` di SQL Server lo trova perché la collation (non `_SC`) ignora i caratteri supplementari.
- FE: il campo testo si nasconde con "Nessuna condizione" ma il valore resta in `form.subjectValue` → errore `subjectTypeMissing` su un campo invisibile.

## Scope
### File da modificare
- [x] `src/dr-mailroom-core/Filtering/FilterMatcher.cs` — StartsWith: secondo tentativo saltando il prefisso decorativo (simboli, emoji, segni, formato, spazi)
- [x] `test/dr-mailroom-tests/Filtering/FilterMatcherTests.cs` — casi con emoji, variation selector, spazi e controllo che la punteggiatura non venga saltata
- [x] `docs/architettura.md` — riga D8 + footer
- [x] `../dr-mailroom-fe/src/composables/filters/useFilterBuilder.ts` — svuota `subjectValue` quando il tipo torna a `''`
- [x] `../dr-mailroom-fe/src/composables/filters/__tests__/useFilterBuilder.spec.ts` — test del comportamento (crea o estende)

### Perimetro negativo
- Non toccherò: `Contains`, `Regex`, validatori BE/FE, collation e dati del DB, altre viste FE, piano FE IN CORSO

## Fasi

### Fase 1: Matcher
- **Stato**: [x]
- **Precondizione**: `SubjectMatches` usa `StartsWith` ordinale
- **File**: `src/dr-mailroom-core/Filtering/FilterMatcher.cs`
- **Operazione**: EDIT
- **Azione**: `StartsWith` → prima confronto diretto, poi confronto dopo il prefisso decorativo dell'oggetto
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok
- **Su divergenza**: STOP

### Fase 2: Test matcher
- **Stato**: [x]
- **Precondizione**: Fase 1 fatta
- **File**: `test/dr-mailroom-tests/Filtering/FilterMatcherTests.cs`
- **Operazione**: EDIT
- **Azione**: aggiungi InlineData al Theory esistente
- **Tool ammessi**: `dotnet test`
- **Verifica passo**: tutti i test verdi
- **Su divergenza**: STOP

### Fase 3: Documento D8
- **Stato**: [x]
- **Precondizione**: Fasi 1-2 verdi
- **File**: `docs/architettura.md`
- **Operazione**: EDIT
- **Azione**: riga D8 descrive la tolleranza del prefisso; footer v1.8
- **Tool ammessi**: nessuno
- **Verifica passo**: rilettura
- **Su divergenza**: STOP

### Fase 4: Costruttore FE
- **Stato**: [x]
- **Precondizione**: nessuna
- **File**: `../dr-mailroom-fe/src/composables/filters/useFilterBuilder.ts`, spec
- **Operazione**: EDIT / CREATE
- **Azione**: watch su `form.subjectMatchType`: se `''` → `form.subjectValue = ''`; test
- **Tool ammessi**: `npm run test:unit`, `npm run lint`/`type-check`
- **Verifica passo**: test, lint e type-check verdi
- **Su divergenza**: STOP

## Criteri di verifica finale
- [x] Test BE verdi, con i nuovi casi emoji
- [x] Test FE verdi; lint e type-check puliti
- [x] Nessun file fuori Scope modificato

## Esito
- BE: 157 test verdi (1 skip preesistente), `dotnet format` pulito sui file toccati
- FE: 209 test verdi, `lint:check` e `type-check` puliti, Prettier applicato al nuovo spec
- 2026-10-11 — verifica a freddo (`/dr-verify-plan`, subagente indipendente): 5 file CORRISPONDONO, 3 criteri SODDISFATTI (BE 173 test, FE 232 test, lint e type-check puliti; commit `cc8da11` e FE `02e92ea` solo su file in Scope). Footer di `architettura.md` ora v1.9 per una revisione successiva

Piano filtro-oggetto-emoji verificato. Tutti i criteri soddisfatti.
