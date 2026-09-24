---
agent: 'agent'
description: 'Crea o aggiorna il README.md usando solo dati presenti nel repository'
tools: ['search/codebase']
---

# Prompt: README Generator (AI Agent)

Crea o aggiorna README.md usando solo dati presenti nel repository. Non inventare.

## Obiettivo
- Titolo progetto
- Panoramica breve
- Sezione Documentazione (docs/) con elenco file .md
- Sezione Quick Links: preservata se già presente nel README corrente

## Istruzioni operative
1) Panoramica breve e concreta
2) Elenca tutti i .md in docs/ (escludi readme.md se autoreferenziale)
3) Una descrizione breve per ogni file
4) Quick Links: se il README esistente contiene già la sezione, mantienila invariata; se il README è nuovo o non ha Quick Links, ometti la sezione (non inventarla)
5) Nessuna sezione extra

## Footer
Usa data e ora correnti nel formato `YYYY-MM-DD HH:MM` (fuso locale):
```markdown
---
*Revisione v{N} — {YYYY-MM-DD HH:MM} — {modello-llm}*
```
La versione template e in fondo a questo file.

## Regole
- Non inventare dati
- Tono conciso
- Se un file non esiste, non inserirlo

## ✅ Checklist Post-Generazione
- [ ] README.md aggiornato con titolo e panoramica
- [ ] Documentazione: tutti i .md in docs/ elencati
- [ ] Quick Links preservati se esistenti (omessi se assenti)
- [ ] Nessuna sezione extra
- [ ] Footer con data e LLM

*Template v1.3 - .NET 10 - Token-optimized for AI agents* - Last Update 2026-07-02 00:03 - claude-fable-5
