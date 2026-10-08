---
applyTo: "README.md"
---

# Struttura README — dr-mailroom

Questo file definisce la struttura obbligatoria del `README.md` di questo repository (`dr-mailroom`, servizio .NET 10 che legge caselle Gmail in sola lettura).
Il README è la porta d'ingresso: dice cos'è il servizio e rimanda a `docs/` per tutto il resto. Non duplica il contenuto dei documenti.
Quando crei o aggiorni il README, rispetta esattamente questa struttura. Non aggiungere sezioni non previste. Non rimuovere sezioni esistenti.

---

## Struttura obbligatoria

Il README deve contenere queste sezioni, in questo ordine:

| # | Sezione | Scopo |
|---|---------|-------|
| 1 | Titolo + panoramica | Nome del servizio e cosa fa, in poche righe concrete |
| 2 | Documentazione | Tabella di tutti i file `.md` in `docs/` con descrizione |
| 3 | Quick Links | Solo se già presente: link rapidi (Scalar, health, ecc.) |
| 4 | Footer | `*Documento aggiornato: Mese Anno — Revisione vN — YYYY-MM-DD — modello*` |

---

## Regole per sezione

### 1 — Titolo + panoramica
- H1 con il nome del repository: `# dr-mailroom`
- Un paragrafo su cosa fa il servizio, più una lista breve delle attività per casella
- Cita i progetti della solution (`dr-mailroom-api`, `dr-mailroom-core`, `dr-mailroom-tests`), il database e il frontend separato
- Niente istruzioni di avvio, configurazione o elenchi di endpoint: stanno in `docs/`

### 2 — Documentazione
- Titolo: `## Documentazione`
- Tabella con colonne: `File | Contenuto`
- Una riga per ogni file `.md` in `docs/` che esiste davvero
- Escludi `docs/*-wiki.md`: non sono committati
- Link markdown cliccabile: `[docs/nome.md](docs/nome.md)`
- Contenuto: una riga che dica cosa si trova nel file, non un riassunto

### 3 — Quick Links
- Mantienila invariata se il README la contiene già
- Se non c'è, non crearla

### 4 — Footer
- Formato esatto: `*Documento aggiornato: Mese Anno — Revisione vN — YYYY-MM-DD — modello-llm*`
- Preceduto da una riga `---`
- Incrementa la revisione di 0.1 per modifiche normali, di 1.0 per ristrutturazioni (stessa logica di `doc-versioning.instructions.md`)

---

## Quando aggiornare il README

| Evento | Cosa aggiornare |
|--------|-----------------|
| Nuovo file in `docs/`, o file rimosso | Sezione "Documentazione" |
| Cambia lo scopo del servizio o la composizione della solution | Sezione "Titolo + panoramica" |
| Qualsiasi modifica | Footer |

*Template v1.0 - dr-mailroom - Last Update 2026-10-08 23:05 — claude-opus-5-5 — adattato dal template dr-guidelines v2.2, che descriveva il README del repository dr-guidelines*
