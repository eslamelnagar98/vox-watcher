---
name: VOX Watcher
description: The watcher's control room as a reference manual lying open at a tabbed section.
colors:
  chrome-yellow: "#f3c01f"
  oxide-orange: "#e8672a"
  grass: "#5b9b3a"
  teal: "#139e97"
  ultramarine: "#2853b3"
  violet: "#7650bf"
  chrome-yellow-night: "#b48c00"
  oxide-orange-night: "#8f3400"
  grass-night: "#2c6500"
  teal-night: "#00635e"
  ultramarine-night: "#234dad"
  violet-night: "#6038a5"
  vermilion: "#c4311a"
  on-vermilion: "#fff8ee"
  ink: "#161412"
  paper: "#f7f2e6"
  smoke: "#141210"
  ivory: "#efe9dc"
typography:
  display:
    fontFamily: "Archivo, Univers Condensed, Helvetica Neue, Arial Narrow, sans-serif"
    fontSize: "clamp(52px, 16vw, 96px)"
    fontWeight: 820
    lineHeight: 0.86
    letterSpacing: "-0.01em"
    fontVariation: "'wdth' 64"
  title:
    fontFamily: "Archivo, Univers Condensed, Helvetica Neue, Arial Narrow, sans-serif"
    fontSize: "17px"
    fontWeight: 760
    lineHeight: 1.15
    letterSpacing: "0.08em"
    fontVariation: "'wdth' 72"
  label:
    fontFamily: "Archivo, Univers Condensed, Helvetica Neue, Arial Narrow, sans-serif"
    fontSize: "17px"
    fontWeight: 720
    lineHeight: 1
    letterSpacing: "0.08em"
    fontVariation: "'wdth' 76"
  tab:
    fontFamily: "Archivo, Univers Condensed, Helvetica Neue, Arial Narrow, sans-serif"
    fontSize: "15px"
    fontWeight: 750
    lineHeight: 1
    letterSpacing: "0.1em"
    fontVariation: "'wdth' 70"
  body:
    fontFamily: "Sabon, Iowan Old Style, Palatino Linotype, Palatino, Book Antiqua, Charter, Sitka Text, Georgia, serif"
    fontSize: "17px"
    fontWeight: 400
    lineHeight: "26px"
    fontFeature: "lnum"
  time:
    fontFamily: "ui-monospace, SF Mono, Cascadia Mono, Menlo, Consolas, monospace"
    fontSize: "17px"
    fontWeight: 600
    lineHeight: 1
  mono-label:
    fontFamily: "ui-monospace, SF Mono, Cascadia Mono, Menlo, Consolas, monospace"
    fontSize: "14px"
    fontWeight: 500
    lineHeight: 1.2
rounded:
  hairline: "2px"
  sm: "3px"
  tab: "0 9px 9px 0"
  leaf: "3px 10px 10px 3px"
  circle: "50%"
spacing:
  tab-step: "3px"
  chip-gap: "8px"
  gap: "10px"
  leaf-gap: "14px"
  leaf-gap-wide: "18px"
  leaf-pad: "16px"
  field-gap: "22px"
  binding-phone: "26px"
  binding: "30px"
  rail: "46px"
  rail-wide: "60px"
components:
  button-primary:
    backgroundColor: "{colors.chrome-yellow}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "10px 16px"
    height: "48px"
  button-outline:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "10px 16px"
    height: "48px"
  button-small:
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "6px 12px"
    height: "40px"
  button-danger-armed:
    backgroundColor: "{colors.vermilion}"
    textColor: "{colors.on-vermilion}"
    typography: "{typography.label}"
    rounded: "{rounded.sm}"
    padding: "10px 16px"
    height: "48px"
  time-chip-open:
    backgroundColor: "{colors.chrome-yellow}"
    textColor: "{colors.ink}"
    typography: "{typography.time}"
    rounded: "{rounded.sm}"
    padding: "8px 10px"
    height: "44px"
  time-chip-down:
    backgroundColor: "transparent"
    typography: "{typography.time}"
    rounded: "{rounded.sm}"
    padding: "8px 10px"
    height: "44px"
  choice-chip:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "8px 14px 8px 12px"
    height: "44px"
  status-tag-on:
    backgroundColor: "{colors.chrome-yellow}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.hairline}"
    padding: "3px 8px"
  text-input:
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "10px 12px"
    height: "48px"
  leaf:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.leaf}"
    padding: "16px 16px 18px 42px"
  leaf-night:
    backgroundColor: "{colors.smoke}"
    textColor: "{colors.ivory}"
    rounded: "{rounded.leaf}"
    padding: "16px 16px 18px 42px"
  tab:
    backgroundColor: "{colors.oxide-orange}"
    textColor: "{colors.ink}"
    typography: "{typography.tab}"
    rounded: "{rounded.tab}"
  errata-slip:
    backgroundColor: "{colors.vermilion}"
    textColor: "{colors.on-vermilion}"
    typography: "{typography.body}"
    rounded: "{rounded.hairline}"
    padding: "10px 14px"
  note-slip:
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.hairline}"
    padding: "10px 14px"
---

# Design System: VOX Watcher

## Overview

**Creative North Star: "The Reference Manual, Open at a Tab"**

VOX Watcher is a one-person control room, and it is drawn as a printed reference manual lying open at one of its divisions. The open division's board, a full-strength coloured divider card, floods the whole page. Content sits on it as milk-acetate leaves hinged on two punched holes, each casting a hard, unblurred cut shadow. Divider tabs step down the right-hand fore edge, one per section, and the open one is cut from the board itself. Everything is set in print ink: condensed bold capitals for heads, a book serif at a single reading size for text, monospace for times. The system refuses the category default for monitoring tools, a dark dashboard of poster tiles and metric cards. Numbers appear as manual content (a list of bookable times, a ledger, a bar chart in ink), never as big-number tiles.

The material does the state work. A bookable showtime is a filled, raised chip in the section's hue. Something listed but not yet bookable lies face-down: hatched, dashed or ruled, muted, with no shadow. A selected option is a punched hole that shows the board through the sheet. A leaf that is saving hangs half-turned on its hinge. A problem is a vermilion errata slip pasted in at a slight angle. Motion is mechanical: a two-frame hinge, never an eased curve.

Density is that of a manual page, not a cockpit: one column of generous leaves on a phone, dealt into two and then four columns on wider screens, with a 17px/26px reading line throughout. Colour is never picked by eye at runtime. The board, the leaf's opacity and every text colour on a coloured surface are computed from the six section hues so that contrast holds on every board, day and night.

**Key Characteristics:**
- Six full-strength divider boards, one per section. Only the open board floods the page; the other hues live on their tabs.
- Milk-acetate leaves whose opacity is solved per board, with a binding strip, two punched holes, a sheen and a hard 3px cut shadow.
- A stepped tab rail on the right edge, with each tab's length following how much its division holds.
- One reading size (17/26). Hierarchy comes from face, width, weight and case.
- Physical states: filled (bookable), face-down hatch (unavailable), punched hole (selected), half-hinged (pending), vermilion slip (error).
- Hard offset shadows only, a two-frame 90ms hinge, and no motion at all under reduced motion.

## Colors

The palette is full-strength stationery: six divider-card hues, print ink, a warm paper, and a single vermilion reserved for errata.

### Primary
The six section boards. Each section owns one hue. By day the hue is the board. At night the board is the same hue re-lit in OKLCH: lightness 0.45, or 0.66 for very light hues (source L ≥ 0.78, which in practice means chrome yellow), keeping as much chroma as sRGB allows. A plain mix toward black would turn the yellow olive.

- **Chrome Yellow** (#f3c01f; night board #b48c00): the Now board, and the app's theme and manifest colour.
- **Oxide Orange** (#e8672a; night #8f3400): Showtimes.
- **Grass** (#5b9b3a; night #2c6500): Watches.
- **Teal** (#139e97; night #00635e): Insights.
- **Ultramarine** (#2853b3; night #234dad): Log.
- **Violet** (#7650bf; night #6038a5): Setup.

Inside a leaf, the open section's full-strength hue appears only as a filled affordance: the primary button, a bookable time chip, the "Watching" tag and an "opened" event tag. The same hue fills the tabs and these affordances by day and by night.

Per-section values computed by the build (`palette()` in js/model.js):

| Section | Leaf alpha, day / night | Text on board, day / night | Text on hue (tabs, fills) |
|---|---|---|---|
| Now | 0.772 / 0.837 | ink / ink | ink (10.8:1) |
| Showtimes | 0.903 / 0.652 | ink / ivory | ink (5.6:1) |
| Watches | 0.897 / 0.697 | ink / ivory | ink (5.4:1) |
| Insights | 0.903 / 0.687 | ink / ivory | ink (5.6:1) |
| Log | 0.932 / 0.666 | paper / ivory | paper (6.3:1) |
| Setup | 0.924 / 0.652 | paper / ivory | paper (5.1:1) |

### Secondary
- **Errata Vermilion** (#c4311a), with **Slip Paper** (#fff8ee) text at 5.2:1. It marks something wrong or about to be destroyed, and only that: the errata slip, inline field errors, the "problem" freshness dot and the armed delete button.

### Neutral
- **Print Ink** (#161412): daytime text on leaves (14.9:1 on every solved leaf), text on light boards, borders of controls, insight bars.
- **Book Paper** (#f7f2e6): the daytime leaf, laid over the board at its solved alpha. Also the text colour on ultramarine and violet.
- **Smoke** (#141210): the night leaf, laid over the night board at its solved alpha.
- **Ivory** (#efe9dc): night text on leaves (12.4:1 on every solved leaf) and on dark boards.

Derived tones, computed in CSS from the current text and board colours rather than stored:
- secondary text: text 74% mixed with the board (8.1–12.4:1 on leaves);
- rules: text at 26%;
- hatch: 18% (disabled controls) and 11% (face-down chips and tags);
- binding tint: 6%;
- cut shadow: ink at 30% by day, black at 55% at night;
- leaf edge highlight: white at 70% by day, 10% at night;
- acetate sheen: white at 24% by day, 5% at night.

### Named Rules
**The One Board Rule.** The open section's colour floods the entire page, including the `theme-color` meta. Every other section's hue appears only on its own tab. Two boards are never open at once.

**The Solved Acetate Rule.** Leaf opacity is never chosen by hand. It is the least opacity at which the leaf composited over the board reaches a fixed luminance: at least 0.80 by day (paper) and at most 0.02 at night (smoke). Text contrast is therefore identical on every board.

**The Ink-or-Paper Rule.** Text on any coloured surface is picked by computed contrast, not by taste: ink or paper on day boards and hues, ivory or ink on night boards.

**The Errata Rule.** Vermilion means wrong or destructive. It is never a section colour, never decoration and never emphasis.

## Typography

**Display Font:** Archivo variable, self-hosted (weight 100–900, width 62–125%), with Univers Condensed, Helvetica Neue and Arial Narrow as fallbacks
**Body Font:** a system book serif: Sabon, Iowan Old Style, Palatino Linotype, Palatino, Book Antiqua, Charter, Sitka Text, Georgia, serif
**Label/Mono Font:** ui-monospace, SF Mono, Cascadia Mono, Menlo, Consolas

**Character:** Heads are set in a manual's condensed, heavy capitals and text in a quiet book serif, as on a printed page. Monospace marks anything read off the machine: times, timestamps, axes and the watcher's status.

### Hierarchy
- **Display** (820, clamp(52px, 16vw, 96px), line-height 0.86, width 64%, −0.01em, caps): the section name on its board, once per page. Text wrapping is balanced.
- **Title** (760, 17px, 1.15, width 72%, 0.08em, caps): leaf titles, form legends and labels, experience names (GOLD, STANDARD) at 0.06em, and table heads.
- **Label** (720, 17px, 1, width 76%, 0.08em, caps): button and tag text. The arrow glyph inside a button (→, or ↗ for links that leave the app) drops back to the serif at 400.
- **Tab** (750, 15px on phones and 17px from 720px, width 70%, 0.1em, caps, set vertically): the divider tab labels.
- **Brand** (800, 17px, width 72%, 0.12em, caps): the running-head wordmark "VOX WATCHER".
- **Body** (400, 17px on a 26px line, lining figures): all reading text. Day headings inside leaves use the same serif at 700. Paragraphs are capped at 66ch, and intros and slips at 62ch.
- **Time** (mono, 600, 17px): time chips (700 when bookable) and booking times (700, line-height 1.3).
- **Mono label** (mono, 500, 14px): freshness status, chart axes and day labels, ledger timestamps, and the desktop spine (0.22em, caps).

### Named Rules
**The One Reading Size Rule.** Everything read as text is 17px on a 26px line. Hierarchy comes from face, width, weight and case, never from a ladder of sizes. The only other sizes are the display board title, 14px mono labels, 15px tab labels on phones, and the 20px refresh glyph.

**The Mono Means Machine Rule.** Monospace is for times, timestamps, axes and the watcher's own status. Prose is never set in mono.

**The Condensed Caps Rule.** Every all-caps head is Archivo at reduced width (64–76%) with positive tracking (0.06–0.12em). The display title alone tracks slightly negative.

## Layout

The page is an open book: a grid of a page column (`minmax(0, 1fr)`) plus a fore-edge rail, 46px wide on phones and 60px from 720px. The rail is sticky and full-height (`100dvh`). Its tabs start 62px down, below the running head's rule, and each tab's length is a flex grow of `1 + log2(1 + extent)`, clamped to 62–210px. The extent is how much the division holds: bookable showtimes + 1 for Now, then sessions, watches, recorded openings and log events, with Setup fixed at 1. Tabs stack 3px apart and are stepped in 12px from the page edge. The open tab steps out flush.

At the top of the page column sits the running head (56px minimum, 1.5px rule underneath in the board-text colour), then the display title (22px above, 12px below), an optional intro, any slips, and the leaves.

Leaves are dealt like cards:
- **Under 720px:** one column with a 14px gap. Page side padding is 6px, the binding narrows to 26px, and text starts 34px in, which buys a longer line on phones.
- **From 720px:** two columns with an 18px gap. Page padding is 30px left and 22px right. A wide leaf spans the full row, and showtime rows gain a 96px label column for the experience name.
- **From 1200px:** four columns. The book is capped at 1480px and centred, the page indent grows to 64px, and a wide leaf spans two columns. A fixed vertical spine in 14px mono runs up the left margin: "VOX Watcher — reference manual — egy.voxcinemas.com".

Inside a leaf, padding is 16px, 18px at the bottom, with text starting at the binding width + 12px. Paragraph and title spacing is 10px, action rows use a 10px gap, chip rows an 8px gap, and form fields a 22px gap. Lists inside leaves are separated by 1px rules rather than boxes. Safe-area insets are honoured on every edge.

### Named Rules
**The Fore-Edge Rule.** Section navigation is always the tab rail down the right edge, at every width. There is never a top bar menu and never a bottom tab bar.

**The Dealing Rule.** Leaves deal into 1, 2 or 4 columns at the 720px and 1200px breakpoints. A wide leaf takes the full row at 2 columns and half the row at 4.

## Elevation & Depth

Depth is physical layering, not ambient light. There are three plies:
- the board, with a fine fractal-noise grain (an inline SVG, 180px tile, 9% alpha);
- translucent acetate leaves over it;
- tabs and slips on top.

Every outer shadow is a hard offset with no blur, the way a die-cut sheet throws a shadow. Blur exists in exactly one place: inside the punched holes, where it gives the hole its depth. Slips are pasted in at a slight angle (errata −0.7°, note +0.5°). Pending work hangs a leaf half-turned on its hinge in 3D (perspective 1400px).

### Shadow Vocabulary
- **Leaf cut** (`box-shadow: 3px 3px 0 var(--shadow), inset 1.5px 1.5px 0 var(--edge)`): every leaf. The inset highlight is the acetate's cut edge.
- **Card cut** (`box-shadow: 2px 2px 0 var(--shadow)`): tabs, primary buttons, bookable time chips, errata and note slips.
- **Punch hole** (`box-shadow: inset 0 0 0 1.5px rgb(0 0 0 / 0.32), inset 1px 2px 2px rgb(0 0 0 / 0.45)`): the two binding holes on every leaf.
- **Punched selection** (`box-shadow: inset 1px 1.5px 2px rgb(0 0 0 / 0.55)`): the hole inside a selected choice chip.

### Named Rules
**The Hard Cut Rule.** Outer shadows are unblurred offsets: 3px for leaves, 2px for everything that sits on a leaf or the rail. Blur is only allowed inside a punched hole.

**The Face-Down Rule.** Anything unavailable lies face-down: a 135° hatch (lines every 7px), a dashed or hairline-rule edge, muted text and no shadow. Disabled buttons, not-yet-bookable times, sold-out times (struck through as well) and paused watches all use it.

## Shapes

The form language is cut stationery. Leaves are nearly square on the binding side and gently rounded on the free edge (3px / 10px). Tabs are square where they meet the board and rounded at their outer end (9px). Buttons, inputs, choice chips and time chips share a 3px corner. Slips and status tags are flatter still (2px). Circles are reserved for three things: the punch holes (16px on leaves, 14px in chips), the 11px freshness dot and the 44px refresh control. Lines are drawn at 1.5px for control borders and the running-head rule, and at 1px for rules between list rows.

The binding margin is a recurring silhouette on every leaf: a 30px strip (26px on phones) tinted 6% toward the text colour and closed by a hairline. The two holes sit centred in it, placed at `clamp(14px, 18%, 140px)` from the top and bottom, and show the board straight through the sheet.

## Components

Every component is printed matter on the leaf: ink outlines, condensed caps and physical states. The build defines no hover styles. Feedback is the pressed state, which inverts a control to text-coloured with the leaf showing through, and a 3px solid outline in the current colour, offset 3px, on keyboard focus.

### Buttons
- **Shape:** 3px corners, 1.5px solid border in the text colour, 48px minimum height, 10px 16px padding, with a 10px gap before the arrow glyph.
- **Primary:** filled with the open section's full-strength hue, text in ink or paper by contrast, plus the card-cut shadow. This is the BOOK button on Now, and also Save watch, New watch and Save token.
- **Outline (default):** transparent, text-coloured. Used for secondary actions such as Run history ↗ and Cancel.
- **Small:** 40px minimum height, 6px 12px padding, for "Show N more" and inline adds.
- **Disabled:** face-down. Hatched (18%, 1.5px lines every 7px), dashed border in secondary text, no shadow, `not-allowed` cursor.
- **Danger:** an outline button until it is armed for confirmation. Armed, it fills with vermilion.
- **Pressed:** background becomes the text colour and the text becomes the solid leaf colour.

### Chips
- **Choice chip:** a 44px minimum label with a 1.5px rule-coloured border, 3px corners, serif text and a 14px hole on the left. Unselected, the hole is a dashed ring. Selected, the border turns to full text colour and the hole is punched through: filled with the board colour and deepened with the punched-selection shadow. The real checkbox covers the whole chip invisibly. Focus draws a 3px text-coloured outline, offset 2px.
- **Time chip, bookable:** mono 700, filled with the section hue, a 1.5px ink border, the card-cut shadow and ↗. It links straight to the VOX booking page and is the loudest object on any page.
- **Time chip, not yet bookable:** face-down. Faint hatch (11%), a 1px rule border, secondary text. Sold-out chips add a 2px strike-through.
- **Status tag:** a condensed caps label with 2px corners. "Watching" is filled with the section hue. "Paused" is face-down. A tag never carries a hole, so it cannot be mistaken for a selectable chip.

### Cards / Containers
The container is the **leaf**: a milk-acetate sheet.
- **Corner Style:** 3px on the binding side and 10px on the free edge.
- **Background:** stacked from the top: a 115° sheen fading out by 45%, the binding strip with its hairline, then paper by day or smoke at night at the solved alpha.
- **Shadow Strategy:** leaf cut (see Elevation & Depth).
- **Holes:** two 16px holes in the binding margin, filled with the board colour.
- **Internal Padding:** 16px on top and right (14px right on phones), 18px at the bottom, binding + 12px on the left.
- **Title:** every leaf opens with a condensed-caps title. Lists inside are separated by 1px rules, with no nested boxes.

### Inputs / Fields
- **Style:** 48px minimum height, 1.5px solid border in the text colour, 3px corners, serif 17px text. The background is the leaf colour deepened slightly toward the text colour (`color-mix(in srgb, var(--leaf-solid) 70%, var(--text) 4%)`). Placeholders are in secondary text.
- **Labels and legends:** condensed caps titles. A helper line in secondary text sits under a legend.
- **Pairs:** date and time ranges sit in an auto-fit grid with 140px minimum columns.
- **Error:** a vermilion field-error slip directly under the field. The form also opens with an errata block, "Fix before saving.", listing every problem.
- **Sticky save bar:** the editor's actions stick to the bottom of the leaf on a solid leaf-coloured strip with a top rule, so Save stays reachable while the form scrolls.

### Navigation
- **Tab rail:** vertical labels in condensed caps on full-strength hue tabs, with 9px outer corners and the card-cut shadow. Every section keeps its own hue on its tab in both modes. The open tab steps out flush, takes the board colour and the board's text colour, and gains a 1.5px border on its three outer sides, so it reads as part of the board. Keyboard focus outlines inside the tab (offset −5px).
- **Running head:** the brand at the left. At the right, the freshness line in 14px mono with an 11px dot, then a 44px circular refresh control (↻).
  - **Dot states:** filled when the last check is healthy; vermilion on a problem; two hard frames (1.2s steps) while a check runs.
  - **Mismatch:** that blink is the one animation not yet behind the reduced-motion gate. It should be.

### Slips (signature)
- **Errata slip:** a vermilion slip with on-vermilion text, 10px 14px padding, 2px corners, card-cut shadow, rotated −0.7°. Error notices open with a bold "Problem." and are announced as alerts.
- **Note slip:** a solid leaf-coloured slip with a 1.5px ink border, rotated +0.5°, announced as a status. Used for confirmations and neutral notices.

### Booking list (signature)
Each bookable session is one row:
- the time in mono 700 with the date in secondary text beside it;
- the experience in condensed caps below;
- the primary BOOK → button to the right, spanning both lines.

Rows are separated by 1px rules. Days with nothing bookable fold into a disclosure whose marker is a condensed "+" / "–".

### Hinge motion
Changing section turns the new leaves on their binding. Each leaf rotates from −18° to flat around its left edge in 90ms over two hard frames (`steps(2, jump-end)`), never an eased curve. A leaf that is saving holds a static −10° half-turn. Under `prefers-reduced-motion: reduce` there is no transition and no turn. The pending pose remains as a still state.

### Insight charts
Charts are plain bars in the text colour (ink by day, ivory at night), with no hue fill and no gridlines: a 7-column chart by weekday (150px tall) and a 24-column strip by hour (48px), each with 14px mono axis labels. Sell-out speeds are set as a ruled table with condensed-caps heads.

## Do's and Don'ts

### Do:
- **Do** derive every section's board, leaf opacity and text colours from its single hue through the palette computation. Light leaves must reach luminance ≥ 0.80, dark leaves ≤ 0.02, and night boards must be OKLCH lightness 0.45 (0.66 for hues at L ≥ 0.78) at maximum in-gamut chroma.
- **Do** pick text on any coloured surface by measured contrast: ink or paper by day, ivory or ink on night boards.
- **Do** keep tabs, primary buttons and bookable chips at full-strength hue in both modes. Only the open board darkens at night.
- **Do** put the bookable showtime and its BOOK button first, filled and raised. Everything unavailable lies face-down with a hatch.
- **Do** keep every filled control's 1.5px ink border. Chrome yellow on a day leaf is only 1.4:1, so the border, not the fill, defines the edge.
- **Do** set all reading text at 17/26 and build hierarchy from Archivo condensed caps (width 64–76%), the serif and mono.
- **Do** use hard offset shadows (3px for leaves, 2px for everything else) and keep blur inside punched holes.
- **Do** animate only as a two-frame 90ms hinge, and gate every animation behind `prefers-reduced-motion: no-preference`.
- **Do** keep touch targets at 44px or more (48px for buttons and inputs, 40px only for small inline buttons) and show focus as a 3px current-colour outline.
- **Do** set per-section colours at runtime as custom properties through the CSSOM (`style.setProperty`). Ship textures as `data:` SVG in the stylesheet and fonts from the site itself.

### Don't:
- **Don't** build a dark dashboard of poster tiles and metric cards. Numbers live in lists, ledgers and ink bar charts on leaves.
- **Don't** use vermilion for anything except errors and armed destructive actions, and never use it as a section hue.
- **Don't** hand-pick a leaf opacity or a "dark version" of a hue by mixing toward black. Mixing turns chrome yellow olive.
- **Don't** introduce a second reading size, a heading-size ladder or mono prose.
- **Don't** use blurred or ambient drop shadows, glows, or eased or springy transitions.
- **Don't** make pill-shaped buttons or chips. Circles belong to punch holes, the status dot and the refresh control.
- **Don't** put a punch hole on anything that isn't selectable. State tags stay tags.
- **Don't** move section navigation off the right-edge tab rail into a top bar or bottom tab bar.
- **Don't** write `style=""` attributes or `<style>` blocks into the HTML (the CSP is `style-src 'self'`), and don't load fonts or assets from third-party CDNs.
- **Don't** use VOX Cinemas' logo or brand assets. VOX Watcher is a personal tool with its own identity.
