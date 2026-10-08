# Design dr-mailroom-fe — direzione "Hangar Federale"

Allegato al [piano](plan.md). Descrive **come deve apparire e comportarsi** il frontend; il *cosa* resta in [frontend-specifiche.md](../../../docs/frontend-specifiche.md).

> In una frase: **una plancia di lancio pulita, dove ogni giro è una sortita.**

Ispirazione: estetica della Federazione Terrestre di Mobile Suit Gundam (Universal Century) — tricolore bianco/blu/rosso con giallo d'avvertimento, panel lines, matricole stencil, console di plancia. È un'**ispirazione**, non un tema licenziato: vedi "Proprietà intellettuale".

---

## Principi

1. **Audace nella struttura, sobrio nel colore.** L'impatto viene da tipografia larga, gerarchia netta e una sola cella "eroe" per vista. Il colore pieno è riservato agli stati e alle azioni primarie.
2. **La metafora vive nel lessico e nei momenti chiave, non in ogni pixel.** Collegamento della prima casella, lancio di un giro, fine di un giro: lì si fa spettacolo. Il resto è un'interfaccia pulita.
3. **Etichetta chiara sempre accanto alla metafora.** "Esegui ora" (testo della specifica), con "Sortita" come sopratitolo; mai solo "Sortita".
4. **Non è un pannello admin.** Niente tabelle a tutta pagina come primo contenuto: ogni vista apre con una sintesi leggibile (frase, numero grande, stato), la tabella sta sotto.

---

## Lessico

| Dominio | Sopratitolo (metafora) | Etichetta operativa |
|---|---|---|
| Caselle | Hangar | Caselle collegate |
| Singola casella | Unità `MR-0003` (matricola = id) | indirizzo email |
| Giro (manuale) | Sortita | Esegui ora |
| Giro schedulato | Pattuglia | Giro automatico |
| Riapplica | Missione di recupero | Riapplica allo storico |
| Filtri | Regole d'ingaggio | Filtri |
| Esecuzioni | Diario di bordo | Esecuzioni |
| Statistiche | Intelligence | Statistiche |
| Banner DB giù | Comunicazioni interrotte | Database non raggiungibile |
| Backfill | Primo pattugliamento | Lettura dello storico |

Matricola: `MR-` + id a 4 cifre (`MR-0003`), in font mono, come numero di serie dipinto sull'unità.

---

## Token di design

Definiti come CSS custom properties in `src/assets/styles/tokens.css`, esposti a Tailwind v4 con `@theme`. Tema chiaro di default ("hangar"), scuro opzionale ("ponte notturno") — `prefers-color-scheme` + scelta manuale persistita in `localStorage` (preferenza UI, non dato sensibile).

| Token | Light | Dark | Uso |
|---|---|---|---|
| `--c-bg` | `#F6F7F9` | `#0B1020` | fondo |
| `--c-surface` | `#FFFFFF` | `#131A33` | pannelli |
| `--c-panel-line` | `#D5DAE3` | `#26304F` | panel lines, bordi |
| `--c-ink` | `#0B1020` | `#E8ECF6` | testo |
| `--c-ink-muted` | `#4A5470` | `#A3ADC8` | testo secondario |
| `--c-federal` | `#0A2989` | `#7C93FF` | primario, link, focus |
| `--c-alert` | `#C00D1E` | `#FF6B70` | errori, distruttivo |
| `--c-caution` | `#FCE300` | `#FCE300` | **solo fondo** con testo `--c-ink` light; strisce d'avvertimento |
| `--c-ok` | `#0F7B4F` | `#4CD69A` | completato |
| `--c-neutral` | `#5B6478` | `#9AA3B8` | annullato (`Cancelled`), in pausa (`Disabled`) |
| `--c-control-border` | `#6E7890` | `#7480A3` | bordi di input e controlli (WCAG 1.4.11, ≥ 3:1) |
| `--c-on-federal` / `--c-on-alert` / `--c-on-ok` | `#FFFFFF` | `#0B1020` | testo su fondo pieno primario, errore, completato |
| `--c-on-caution` | `#0B1020` | `#0B1020` | testo sul giallo (unico ammesso) |

Vincoli:
- `--c-caution` **mai** come colore del testo.
- Contrasti **misurati il 2026-10-08** (formula WCAG 2.x, Fase 7):

  | Coppia | Light | Dark |
  |---|---|---|
  | ink su bg / surface | 17.66 / 18.93 | 16.01 / 14.52 |
  | ink-muted su bg / surface | 7.01 / 7.52 | 8.45 / 7.66 |
  | federal su bg / surface (link, focus) | 11.58 / 12.42 | 6.74 / 6.11 |
  | alert su surface | 6.33 | 6.20 |
  | ok su surface | 5.29 | 9.31 |
  | neutral su surface | 5.93 | 6.79 |
  | on-federal su federal | 12.42 | 6.74 |
  | on-alert su alert | 6.33 | 6.84 |
  | on-ok su ok | 5.29 | 10.27 |
  | on-caution su caution | 14.54 | 14.54 |
  | control-border su surface | 4.42 | 4.38 |
  | panel-line su surface | 1.40 | 1.32 — **solo decorativo**, mai unico confine di un controllo |

  Tutte le coppie di testo ≥ 4.5:1, focus e bordi dei controlli ≥ 3:1.
- Focus: anello 3 px `--c-federal`, offset 2 px, su tutti gli elementi interattivi.
- Bersagli di click ≥ 44 px (minimo WCAG 24 px).

### Tipografia

| Ruolo | Font | Note |
|---|---|---|
| Titoli, numeri grandi | **Saira** variabile, larghezza Expanded (`font-stretch: 125%`), peso 600–800 | sostituto open di Eurostile/Microgramma |
| Testo, form, tabelle | **Saira** larghezza 100%, peso 400–500 | mai Condensed nei testi lunghi |
| Matricole, conteggi, date tecniche | **JetBrains Mono** | `font-variant-numeric: tabular-nums` |

Self-hosted via Fontsource (niente chiamate a Google Fonts a runtime: l'app gira in LAN).

### Elementi grafici originali

- **Panel lines**: bordi da 1 px con angoli smussati a 45° (`clip-path`) sui pannelli principali.
- **Crocini e righelli**: decorazione SVG negli angoli della cella eroe.
- **Strisce d'avvertimento**: diagonale giallo/inchiostro **solo** su stati di pericolo (casella con token revocato, DB giù, conferma di eliminazione).
- **Stencil**: matricole e sopratitoli in maiuscolo spaziato (`letter-spacing: .12em`).
- Icone d'interfaccia: Lucide, sempre accompagnate da testo, **tratto uniforme 1.75 e dimensioni da una scala sola (16/20/24 px)**: nessuna icona fuori scala o di stile diverso.
- **Marchio dell'applicazione** (favicon, icona PWA, barra laterale): disegnato da zero, mai l'icona di default del framework. Pannello smussato a 45°, monogramma "MR" stencil, accento tricolore e tacca gialla. Variante semplificata per 16 px.

---

## Layout

**Guscio**: barra laterale stretta con 4 voci (icona + etichetta) a sinistra, testata con il **banner Comunicazioni** sempre visibile (verde-silenzioso quando tutto va, striscia d'avvertimento + "Riprova connessione" quando il DB è giù). Su schermi stretti la barra diventa una barra inferiore.

**Griglia "baie d'hangar"**: bento grid a 12 colonne; ogni vista ha una sola cella eroe.

| Vista | Cella eroe | Sotto |
|---|---|---|
| Caselle `/mailboxes` | stato complessivo: "3 unità operative · 1 da ricollegare" | card per casella (matricola, indirizzo, badge stato, avanzamento backfill, ultimo giro); tabella come vista alternativa |
| Filtri `/filters` | costruttore a frase del filtro aperto + anteprima live | elenco filtri come card-frase ("Quando scrive **fatture@x.it** e l'oggetto contiene «fattura» → estrai allegati in `fatture/`") |
| Esecuzioni `/runs` | **ponte di lancio**: select casella + "Esegui ora" + giri in corso con avanzamento e "Annulla" | diario di bordo (cronologia) |
| Statistiche `/stats` | frase-titolo calcolata ("Ad agosto 3 mittenti hanno scritto il 40% delle mail") | classifica a barre orizzontali + heatmap mittente × mese con tabella alternativa |

### Stato vuoto (nessuna casella)

Schermata di "prima attivazione": hangar vuoto disegnato a linee, titolo grande "Nessuna unità in hangar", una riga "Collega una casella Gmail: dr-mailroom la legge in **sola lettura**, non modifica né cancella nulla", un solo pulsante "Collega la prima casella". Avviso che il primo giro legge tutto lo storico.

### Costruttore di filtri a frase

> **Quando** arriva una mail da `[mittente]` su `[tutte le caselle ▾]`
> **e** l'oggetto `[contiene ▾]` `[testo]`
> **allora** `[☑ estrai allegati in cartella ___]` `[☐ crea un'allerta]`

Ogni segnaposto è un controllo vero (`<input>`, `<select>`, checkbox) con `<label>` visivamente nascosta ma presente, errore collegato con `aria-describedby`. La modalità "form classico" resta disponibile per chi preferisce.

---

## Motion

| Momento | Animazione | Durata | Con `prefers-reduced-motion` |
|---|---|---|---|
| Esegui ora | "catapulta": il pulsante si carica (striscia che scorre), la card del giro scivola nel ponte | < 600 ms | dissolvenza 150 ms |
| Avanzamento giro | contatore `messagesRead` che scorre (tabular nums) | continuo | aggiornamento istantaneo |
| Giro completato | timbro "MISSIONE COMPLETATA" stencil sulla card | 400 ms | testo statico |
| Cambio vista | View Transitions API sul contenuto | 200 ms | nessuna |
| Prima casella collegata | luci dell'hangar che si accendono sulla card nuova | 800 ms, una volta | nessuna |

Mai animazioni in loop sullo sfondo. Nessun parallasse.

---

## Accessibilità (in aggiunta alla specifica)

- Ogni stato = icona + testo + colore (mai solo colore).
- Regioni `aria-live` create al caricamento della vista; il polling annuncia solo i cambi di fase e al massimo ogni 10 s.
- `<progress>` nativo per l'avanzamento, `max` assente finché il totale non è noto.
- Modali (Reka UI Dialog): focus intrappolato, Esc, focus restituito.
- Heatmap e grafici: sempre con tabella equivalente raggiungibile da tastiera.

---

## Proprietà intellettuale

- Nessun logo, emblema, nome registrato o citazione: niente "Gundam", "EFSF", "Federazione", "White Base", "Minovsky", sagome di mobile suit, font o suoni ufficiali.
- Matricole, stencil, strisce, crocini e illustrazione dell'hangar disegnati da zero.
- L'ispirazione si limita a palette tricolore, linguaggio grafico "industriale-aerospaziale" e lessico generico (hangar, sortita, plancia, diario di bordo).

---

## Fonti

- Ricerca di design del 2026-10-08 (subagente): tendenze, pattern UX, palette RX-78-2 fan-made da https://huepick.app/models/gundam-hueforge-rx-78-2, Saira https://fonts.google.com/specimen/Saira/about, pattern progress https://learn.microsoft.com/en-us/windows/win32/uxguide/progress-bars, filtri a frase https://aiuxplayground.com/pattern/nl-filter
- [frontend-specifiche.md](../../../docs/frontend-specifiche.md)

*Revisione v1.1 — 2026-10-08 — claude-opus-5-5* (etichetta "Esegui ora", token `--c-neutral` per annullato/in pausa)
