# Prompt per generare l'icona di dr-mailroom con un generatore di immagini

Allegato alla Fase 9.1 del [piano](plan.md). Da incollare in ChatGPT (o un altro generatore). Il risultato si riporta nel progetto, dove viene ridisegnato in SVG.

---

Design an original app icon for "dr-mailroom", a personal tool that reads Gmail inboxes in read-only mode, applies filters and extracts attachments.

Visual direction: a clean, industrial-aerospace "hangar control console" look. Think painted panel on a vehicle: flat colors, crisp geometry, stencil lettering. It is an inspiration only: no robots, no mecha, no mobile suits, no military insignia, no existing logos or emblems, no recognizable franchise elements.

Composition:
- a square panel with corners chamfered at 45° (not rounded), thin darker border like a panel line
- centered monogram "MR" in a wide, geometric stencil typeface (Eurostile / Microgramma feel), with stencil gaps
- a tricolor accent: deep federal blue #0A2989 as the main field, white #FFFFFF lettering, a small red #C00D1E stripe
- one small yellow #FCE300 warning notch or diagonal hazard tab in a corner
- optional: a subtle envelope hint built into the geometry (a V-shaped panel line), only if it stays simple

Constraints:
- flat vector style, no gradients, no 3D, no shadows, no texture, no photorealism
- must stay legible at 16×16 pixels: few shapes, thick strokes, high contrast
- plain transparent or solid background, icon centered, generous padding
- output: 1024×1024 PNG

Give me 4 variations: two with the "MR" monogram, one with only the envelope/panel symbol (no letters, for the 16 px favicon), one more minimal.

---

## Cosa fare col risultato

1. Salva l'immagine scelta in `dr-mailroom-fe/design/icon-source.png` (cartella fuori da `public/`).
2. Dì all'agente quale variante hai scelto.
3. L'agente la ridisegna come `favicon.svg` (varianti chiaro/scuro), genera i PNG 180/192/512 e la `favicon.ico`, e controlla la leggibilità a 16 px e l'assenza di elementi protetti prima del Checkpoint C1.

*Prompt v1.0 — 2026-10-08 — claude-opus-5-5*
