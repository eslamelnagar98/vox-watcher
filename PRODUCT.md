# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

One person: the repo owner, in Cairo, on their phone. They open the app in two moments: right after a push alert arrives (to see what opened and book fast), or when a new movie is announced and they want to start watching it. Occasionally they glance at it to confirm the watcher is still alive.

## Product Purpose

VOX Watcher alerts its owner the moment VOX Cinemas Egypt (egy.voxcinemas.com) opens booking for the movie, cinema, date, experience and time they care about, so they can book the best seats before everyone else. It runs by itself on GitHub Actions; alerts arrive through the ntfy app. The web app is the control room: health, current showtimes, watches, and what it has learned. Success means never missing an opening, and never being woken by a false one.

## Positioning

It watches the real booking signal: on the VOX site a showtime is either a crossed-out time (not bookable) or a link (bookable). The watcher remembers every session by its VOX ID and alerts when a crossed-out time becomes a link. It tells apart booking opening for the first time, seats coming back on a sold-out session, and new times listed before they are bookable. It learns when VOX usually opens bookings and how fast each experience sells out.

## Operating Context

- Runs on GitHub Actions: an outside timer (cron-job.org) starts a run every 15 minutes and each run checks VOX about every 2 minutes for 11 minutes (GitHub's own schedule is an hourly backup), through a real Google Chrome (VOX is behind Akamai bot protection).
- Configuration lives in `watches.json` on the `main` branch; the watcher's memory (`state.json`, `events.log`, `INSIGHTS.md`, `CATALOG.md`, `catalog.json`) lives on a separate `state` branch.
- Alerts: ntfy push with a button that opens the exact VOX booking page.
- Times are Africa/Cairo. Dates on VOX have no year; VOX lists after-midnight shows under the previous day.
- The web app is a static site on GitHub Pages. Reading needs no login. Changing watches or starting a check uses a GitHub fine-grained token stored only on the owner's phone.

## Capabilities and Constraints

- Watch filters: cinemas, movies, date range or exact dates, days of week, experiences (GOLD, IMAX, 4DX, MAX, Standard...), start-time window, and which alerts to send (booking opened, seats returned, new showtimes, sold out).
- Experiences are free text matched as VOX shows them; cinema and movie slugs come from the VOX catalog.
- App jobs (all confirmed): health and last check; showtimes per watch with booking links; add, edit or pause watches without JSON; run a check now or send a test alert; insights on opening times and sell-out speed; recent changes.
- English only.
- No server, no database, no paid service. No automatic booking, ever.

## Brand Commitments

- Name: VOX Watcher. It is a personal tool, not affiliated with VOX Cinemas; do not use VOX's logo or brand assets.

## Evidence on Hand

- Real data: `state.json`, `events.log`, `catalog.json` on the `state` branch of eslamelnagar98/vox-watcher (today: Avengers: Doomsday at City Centre Almaza, 3 cinemas, 22 movies).
- Real VOX page captures in `tests/VoxWatcher.Tests/Fixtures/Real/`.
- No testimonials, users, or metrics exist; none may be invented.

## Product Principles

1. Speed to booking beats everything: the bookable showtime and its link come first.
2. Trust is earned by honesty about freshness: always show when the last check ran and whether it worked.
3. No false alarms, no silent failures.
4. Editing a watch must be impossible to get wrong; an invalid `watches.json` stops the watcher.
5. Phone first, one hand, outdoors or in a cinema lobby.

## Accessibility & Inclusion

Must work one-handed on a phone in bright daylight and in a dark cinema lobby; respect the system light/dark setting and reduced motion. WCAG 2.2 AA contrast and touch targets.
