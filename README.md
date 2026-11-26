# VOX Watcher state

This branch is written by the watcher (`.github/workflows/watch.yml` on `main`). Do not edit by hand.

| File | What it is |
|---|---|
| `state.json` | Memory between checks: every showtime seen, bookable or not, and health |
| `events.log` | One line per change, in Cairo time |
| `INSIGHTS.md` | Current status, when VOX usually opens bookings, how fast shows sell out |
| `CATALOG.md` / `catalog.json` | Every cinema and movie slug on the VOX site |
