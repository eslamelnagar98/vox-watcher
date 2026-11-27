---
name: VOX Watcher
description: A calm, simple control room for the watcher. Neutral surfaces, one accent, colour only where it means something.
colors:
  light: { bg: "#f6f7f9", surface: "#ffffff", text: "#1c2230", muted: "#5f6878", border: "#e3e6ec", accent: "#3159d6", ok: "#1f8a4c", danger: "#c43c3c" }
  dark: { bg: "#0f1115", surface: "#171a21", text: "#e7e9ee", muted: "#9ba3b2", border: "#272c37", accent: "#7d9bff", ok: "#4cc283", danger: "#f08080" }
typography: system font stack (San Francisco on iPhone, Segoe UI on Windows); 16px body; tabular numbers for times
---

# VOX Watcher design

## Principles

1. **Calm by default.** Neutral background and white cards. Dark mode follows the phone.
2. **One accent, with one meaning.** Blue means "you can act": a bookable time, the Book button, the current tab.
3. **Status colours only for status.** Green means healthy or watching. Red means a problem that needs you. Nothing else is coloured.
4. **The job first.** The Now page opens on what you can book, soonest first, with a Book button on each row.
5. **Thumb friendly.** A bottom tab bar on phones (top bar on wider screens). Touch targets are at least 40px. The Save bar in the editor stays in reach.

## Structure

| Tab | What it shows |
|---|---|
| Now | Bookable showtimes with Book buttons, the watcher's health, a Check now button |
| Showtimes | Every showtime per watch, grouped by day and format. Blue = bookable, grey = listed, crossed out = sold out |
| Watches | Watches with Edit and Pause, and the editor (chips for movies, cinemas, days, formats) |
| Activity | When VOX usually opens bookings, sell-out speed, and the log of every change |
| Settings | GitHub token, test alert, timer health, links |

Old links to `#insights` and `#log` open Activity.

## Components

- **App bar:** name, a status pill (green dot = checked recently, blue pulsing = checking, red = needs attention), refresh.
- **Card:** white surface, 1px border, 14px radius, a 16px semibold title.
- **Notice:** soft grey for info, soft green for success, soft red for problems.
- **Buttons:** primary (filled accent), secondary (outlined), danger (red text, filled when armed).
- **Chips:** pill toggles; selected = accent outline with a check mark.
- **Time chips:** filled accent when bookable, grey when not, crossed out when sold out.

## Rules

- No inline styles or scripts (the Content-Security-Policy forbids them). Icons are SVG elements built in JS.
- Text is always inserted as text nodes, never as HTML.
- Respect `prefers-reduced-motion` and `prefers-color-scheme`.
