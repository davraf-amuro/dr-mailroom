# dr-mailroom

dr-mailroom è un servizio .NET 10 che legge una o più caselle Gmail **in sola lettura**. Lo stato letto/non letto delle mail non cambia mai.

Per ogni casella:
- conta le mail ricevute da ogni mittente, in totale e mese per mese;
- applica filtri sul mittente, con al più una condizione sull'oggetto (`StartsWith`, `Contains` o `Regex`);
- sulle mail filtrate esegue i processi: salva gli allegati in una cartella e registra un'allerta, che poi un software esterno notifica all'utente.

Il primo giro indicizza tutto lo storico e riprende dal punto salvato se si interrompe. I giri successivi leggono solo le mail nuove.

Il servizio gira da solo con un cron configurabile e si può avviare a mano via API REST (chiave API, documentazione Scalar). Gira in locale o in container ed è composto da `dr-mailroom-api` (Minimal API + Quartz), `dr-mailroom-core` e `dr-mailroom-tests`, con database SQL Server. Il frontend `dr-mailroom-fe` è un progetto separato, per ora solo specificato.

## Documentazione

| File | Contenuto |
|---|---|
| [docs/architettura.md](docs/architettura.md) | Componenti, decisioni (D1–D18, T1–T12, C1–C12), flussi di giro, backfill e riapplica, modello dati |
| [docs/avvio-e-configurazione.md](docs/avvio-e-configurazione.md) | Configurazione e segreti, migration, avvio locale e in container, test, gate di push |
| [docs/setup-google-oauth.md](docs/setup-google-oauth.md) | Preparazione di Google Cloud passo passo e collegamento delle caselle |
| [docs/frontend-specifiche.md](docs/frontend-specifiche.md) | Specifica della SPA `dr-mailroom-fe`: viste, convenzioni API, mappa degli endpoint |
| [docs/onboarding.md](docs/onboarding.md) | Ingresso nel progetto per uno sviluppatore senior: stack, struttura, convenzioni, flusso di lavoro |
| [docs/card-dr-mailroom-api.md](docs/card-dr-mailroom-api.md) | Scheda dell'API: endpoint group, pacchetti, database, servizi esterni, hosting |
| [docs/card-dr-mailroom-core.md](docs/card-dr-mailroom-core.md) | Scheda della libreria di dominio: pacchetti, database, Gmail |
| [docs/endpoint-status.md](docs/endpoint-status.md) | Endpoint di salute: `/health`, stato del database e nuovo tentativo di connessione |
| [docs/endpoint-mailboxes.md](docs/endpoint-mailboxes.md) | Endpoint delle caselle: elenco, collegamento OAuth, pausa, ripresa, eliminazione |
| [docs/endpoint-filters.md](docs/endpoint-filters.md) | Endpoint dei filtri: CRUD, anteprima, riapplica |
| [docs/endpoint-runs.md](docs/endpoint-runs.md) | Endpoint dei giri: avvio manuale, annullamento, cronologia |
| [docs/endpoint-stats.md](docs/endpoint-stats.md) | Endpoint delle statistiche per mittente, totali e mensili |
| [docs/endpoint-alerts.md](docs/endpoint-alerts.md) | Endpoint delle allerte per il software di notifica |
| [docs/endpoint-messages.md](docs/endpoint-messages.md) | Endpoint degli allegati salvati di una mail |

---
*Documento aggiornato: Ottobre 2026 — Revisione v1.2 — 2026-10-08 — claude-opus-5-5*
