---
applyTo: "**"
---

# No Hardcoded Values — Centralizzazione dei Valori Letterali

Regola trasversale (tutti i linguaggi). Un valore letterale con significato non vive sparso nel codice: vive in un punto nominato e modificabile.

---

## Regola 1 — Perimetro positivo: cosa fare

Ogni valore che rientra in **almeno uno** di questi criteri va estratto in un identificatore nominato:

- **(a) Ha significato di dominio** — non è un numero/stringa neutro, ma rappresenta un concetto (uno stato, un tipo, una soglia di business, un codice, una chiave).
- **(b) È ripetuto in ≥ 2 punti** — la ripetizione di un letterale è un bug di manutenzione in attesa.
- **(c) È un limite/soglia modificabile** — max tentativi, dimensione pagina, timeout logico, capacità.

Un valore così estratto deve poter essere cambiato in **un solo posto**, senza cercare occorrenze nel codice.

---

## Regola 2 — Scegliere il contenitore giusto

Il tipo di contenitore dipende dalla natura del valore. Non esiste una scelta unica: si sceglie **in base al caso**.

| Natura del valore | Contenitore | Quando |
|---|---|---|
| Insieme **chiuso e finito** di valori mutuamente esclusivi (stato, categoria, tipo) — "uno tra N" | **Enum** | Stati ordine, tipi documento, livelli, esiti. Il compilatore garantisce l'esaustività. |
| Raccolta di valori **correlati ma non mutuamente esclusivi**, anche di tipi diversi (soglie, chiavi, nomi, formati, limiti) | **Classe statica** con `const` / `static readonly` | `MaxRetries`, `DefaultPageSize`, `CacheKeyPrefix`, formati data. |
| Valore **singolo**, usato in un solo contesto ma con significato | `const` / `static readonly` **locale** alla classe che lo usa | Evita il magic number inline senza creare un contenitore globale. |
| Valore che **cambia per ambiente** (URL, timeout reale, connection string, feature flag, credenziale) | **Configurazione** (`appsettings` + pattern Options / variabili d'ambiente) — **mai** una `const` hardcoded | Vedi `sensitive-data.instructions.md`. Un valore di configurazione dentro una classe statica è comunque hardcoded. |

> Criterio rapido enum vs classe statica: se la domanda naturale è *"quale dei valori possibili?"* → **enum**. Se è *"quanto / come si chiama / qual è il limite?"* → **classe statica** / `const`.

---

## Regola 3 — Perimetro negativo: cosa NON estrarre

Estrarre tutto genera rumore e nasconde ciò che conta davvero. **Non** creare costanti per:

- Letterali **neutri senza semantica**: `0`, `1`, `-1`, `""`, `null`, incrementi/decrementi, indici di array.
- Identità matematiche ovvie in un'espressione locale (`/ 2` per una media, `* 100` per una percentuale evidente dal contesto).
- Fallback idiomatici già leggibili (`?? "-"`, `?? string.Empty`).

Se un letterale è **usato una sola volta** e **non ha significato di dominio**, lasciarlo inline è corretto.

---

## Esempi

```csharp
// ❌ C# — valori di dominio hardcoded e ripetuti
if (order.Status == "SHIPPED") { ... }
if (retries < 3) { ... }
if (order.Status == "SHIPPED" || order.Status == "DELIVERED") { ... }

// ✅ C# — enum per lo stato (insieme chiuso), static class per i limiti
public enum OrderStatus { Pending, Shipped, Delivered, Cancelled }

public static class OrderPolicy
{
    public const int MaxRetries = 3;
    public const int DefaultPageSize = 50;
}

if (order.Status == OrderStatus.Shipped) { ... }
if (retries < OrderPolicy.MaxRetries) { ... }

// ✅ C# — valore di configurazione: NON in una const, ma in Options
// appsettings.json → "Http": { "TimeoutSeconds": 30 }
public sealed class HttpOptions { public int TimeoutSeconds { get; init; } }
```

```typescript
// ❌ TS — magic string ripetuta
if (user.role === 'admin') { ... }
element.classList.add('is-active');

// ✅ TS — enum per l'insieme chiuso, const object per le chiavi
export enum Role { Admin = 'admin', Editor = 'editor', Viewer = 'viewer' }

export const CssClass = {
  Active: 'is-active',
  Hidden: 'is-hidden',
} as const;

if (user.role === Role.Admin) { ... }
element.classList.add(CssClass.Active);
```

```python
# ❌ Python — soglia di business inline
if attempts < 3:
    ...

# ✅ Python — Enum per lo stato, costante di modulo per il limite
from enum import Enum

class OrderStatus(Enum):
    PENDING = "pending"
    SHIPPED = "shipped"

MAX_RETRIES = 3

if attempts < MAX_RETRIES:
    ...
```

---

## ✅ Checklist pre-commit

- [ ] Nessun valore con significato di dominio compare come letterale inline?
- [ ] Nessun letterale (numero o stringa) è ripetuto in ≥ 2 punti?
- [ ] Gli insiemi chiusi di valori mutuamente esclusivi sono modellati con un `enum`, non con stringhe libere?
- [ ] Le soglie, chiavi e limiti correlati sono raccolti in una classe statica / costanti nominate?
- [ ] I valori che cambiano per ambiente sono in configurazione (`appsettings`/Options), **non** in una `const` hardcoded?
- [ ] Non ho creato costanti superflue per letterali neutri (`0`, `1`, `""`) usati una sola volta?

---

*Istruzione v1.0 - No Hardcoded Values - 2026-07-23 — claude-opus-4-8*
