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

---
*Revisione v1.0 — 2026-10-06 08:42 — claude-opus-5-5*
