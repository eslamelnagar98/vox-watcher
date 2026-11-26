# VOX Watcher

Get a push notification on your phone the moment VOX Cinemas Egypt opens booking for the movie, cinema, date and experience you care about. Book the good seats before everyone else.

It runs by itself on GitHub Actions every 5 minutes. No laptop needs to stay on. Your phone only receives the alerts through the free [ntfy](https://ntfy.sh) app.

---

## What it does

- **Watches any combination** of cinemas, movies, dates, days of the week, experiences (GOLD, IMAX, 4DX, MAX, Standard...) and start times.
- **Detects the real signal.** On the VOX site, a showtime is either a crossed-out time (not bookable) or a clickable link (bookable). The watcher remembers every showtime by its ID and alerts you when a crossed-out time becomes clickable.
- **Knows the difference** between:
  - booking opening for the first time,
  - seats coming back on a sold-out session (people cancel, or unpaid seats are released),
  - new dates or showtimes being listed before they are bookable.
- **Sends one grouped message per change**, not 20 separate pushes. The message has a button that opens the booking page of that exact session.
- **Learns the pattern.** Every opening is logged with the day and time, so after a few weeks `state/INSIGHTS.md` tells you when VOX usually opens bookings and how fast each experience sells out.
- **Protects you from silent failure.** If VOX changes its website or blocks the checks for 15 minutes (at least 3 failed checks in a row), you get a warning, and another message when it works again. A short blip sends nothing.
- **Stays polite.** Only a few requests per run, with a pause between them. It never books anything for you.

## How it works

```
 every 5 min
 GitHub Actions ──► VOX showtimes pages ──► parse showtimes ──► compare with state/state.json
                                                                       │
                       your iPhone ◄── ntfy push ◄── only if something changed
                                                                       │
                                     state/ saved back to the repo ◄───┘
```

1. For each watch, it opens `https://egy.voxcinemas.com/showtimes?c=<cinema>&m=<movie>`.
2. It reads the date tabs, then opens each date that matches your filters.
3. For every showtime it reads: movie, cinema, experience, date, time, bookable or not, and the booking link.
4. It compares this with what it saw last time, then sends alerts and saves the new state.

## What you get on your phone

| Alert | When | Priority |
|---|---|---|
| **BOOK NOW: Avengers: Doomsday** | A showtime that matches your watch became bookable | Urgent (5), loud |
| **Seats are back: ...** | A session that sold out is bookable again | Urgent (5) |
| **New showtimes: ...** | New dates or times are listed but not bookable yet | Normal (3) |
| **Sold out: ...** | A bookable session sold out. Off by default | Low (2) |
| **New on VOX: ...** | A movie you are watching appears on the site for the first time | High (4) |
| **Watching: ...** | First run of a new watch: a summary of what it found | Low, or high if something is already bookable |
| **VOX watcher has a problem** | Checks have failed for 15 minutes (at least 3 in a row) | High (4) |

Example:

```
BOOK NOW: Avengers: Doomsday
1 showtime(s) just became bookable. Be quick.
Fri 18 Dec | CC Almaza | GOLD: 7:15pm

Also listed but not bookable yet: 9 showtime(s) on Thu 24 Dec.
[ Book Fri 18 7:15pm ]  [ All showtimes ]
```

Tap the notification and the booking page opens directly.

---

## One-time setup

### 1. Phone

1. Install **ntfy** (by Philipp Heckel) from the App Store or Google Play.
2. Allow notifications.
3. Tap **+** and subscribe to a long random topic, for example `my-vox-alerts-<random letters>`. Anyone who knows the name can read it, so keep it random.
4. On iPhone: Settings > Notifications > ntfy > turn on **Time Sensitive Notifications**, so urgent alerts can come through Focus modes.

### 2. GitHub repository settings

1. **Settings > Secrets and variables > Actions > New repository secret**
   - Name: `NTFY_TOPIC`
   - Value: your topic name (only the name, not the link)
2. **Settings > Actions > General > Workflow permissions**: choose **Read and write permissions** and save.
3. Keep the repository **Public**. GitHub Actions minutes are free for public repos. A check every 5 minutes uses more minutes than a private repo gets for free. Nothing secret is stored in the code; the topic is a secret.

### 3. First run

1. **Actions > VOX watcher > Run workflow**, choose `test-notify`. Your phone should ring within a minute.
2. Run it again with `watch`. You get a **Watching: ...** summary for each watch.
3. From now on it runs every 5 minutes, and you only hear from it when something changes.

---

## Configure what to watch: `watches.json`

Edit `watches.json` on GitHub (open it and click the pencil icon). Saving the file starts a run right away, so you get a **Watching** summary for any new watch within a minute or two.

```jsonc
{
  "site": "https://egy.voxcinemas.com",
  "timeZone": "Africa/Cairo",
  "announceNewMovies": false,

  "watches": [
    {
      "name": "Doomsday at Almaza",
      "enabled": true,
      "cinemas": ["city-centre-almaza"],
      "movies": ["avengers-doomsday"],
      "dates": { "from": "2026-12-16", "to": "2027-01-15" },
      "daysOfWeek": [],
      "experiences": [],
      "time": { "after": "12:00", "before": "23:59" },
      "alerts": { "bookingOpened": true, "seatsReturned": true, "newShowtimes": true, "soldOut": false }
    }
  ]
}
```

Comments (`// ...`) and trailing commas are allowed.

### Fields

| Field | Meaning | Empty or missing means |
|---|---|---|
| `name` | Any unique name. Shown in alerts | Required |
| `enabled` | `false` pauses this watch | `true` |
| `cinemas` | Cinema slugs, e.g. `["mall-of-egypt", "city-centre-almaza"]` | All cinemas |
| `movies` | Movie slugs, e.g. `["avengers-doomsday"]` | All movies |
| `dates.from` / `dates.to` | Date range, format `YYYY-MM-DD` | No limit |
| `dates.on` | Exact dates only, e.g. `["2026-12-24", "2026-12-25"]` | Any date |
| `daysOfWeek` | e.g. `["Thu", "Fri", "Sat"]` | Any day |
| `experiences` | Text as shown on the site: `GOLD`, `IMAX`, `4DX`, `MAX`, `Standard`... Partial match, case-insensitive | Any experience |
| `time.after` / `time.before` | Start time window, 24h, e.g. `"18:00"` | Any time |
| `alerts.bookingOpened` | Push when a showtime becomes bookable | `true` |
| `alerts.seatsReturned` | Push when a sold-out session is bookable again | `true` |
| `alerts.newShowtimes` | Push when new dates or times are listed (not bookable yet) | `true` |
| `alerts.soldOut` | Push when a session sells out | `false` |

Top-level `announceNewMovies: true` pushes a message whenever any new movie is added to the VOX site.

### Finding slugs

Two easy ways:

1. **From the website.** Pick a cinema and a movie on the VOX showtimes page and look at the address bar:
   `egy.voxcinemas.com/showtimes?c=city-centre-almaza&m=avengers-doomsday`
   The value after `c=` is the cinema slug. The value after `m=` is the movie slug.
2. **From this repo.** Open `state/CATALOG.md`. It lists every cinema and movie slug on the site and is updated automatically.

A movie that is not on the site yet is fine. You get a warning in the **Watching** message, and then a **New on VOX** alert when it appears.

### Recipes

Any movie at one cinema, weekends only:

```json
{ "name": "Weekends at Mall of Egypt", "cinemas": ["mall-of-egypt"], "daysOfWeek": ["Thu", "Fri", "Sat"] }
```

One movie, IMAX only, evenings, any cinema:

```json
{ "name": "Dune IMAX evenings", "movies": ["dune-part-three"], "experiences": ["IMAX"], "time": { "after": "18:00" } }
```

Exact dates for a family outing, only tell me when it is bookable:

```json
{
  "name": "Christmas trip",
  "cinemas": ["city-centre-almaza"],
  "movies": ["the-cat-in-the-hat"],
  "dates": { "on": ["2026-12-25", "2026-12-26"] },
  "alerts": { "newShowtimes": false }
}
```

---

## Files the watcher writes (`state/`)

| File | What it is |
|---|---|
| `state/state.json` | Memory between runs: every showtime seen, bookable or not, and when it opened or sold out. Do not edit by hand |
| `state/events.log` | One line per change, in Cairo time. Easy to read on the phone |
| `state/INSIGHTS.md` | Current status per watch, **when VOX usually opens bookings**, and how fast each experience sells out |
| `state/CATALOG.md` | All cinema and movie slugs currently on the site |

Deleting `state/state.json` makes the next run a fresh, silent baseline.

### Learning when VOX opens bookings

`INSIGHTS.md` keeps a table of every opening with the day and time it was detected. After a few openings, you will see a line like:

> **Most common:** Mondays, between 11:00 and 11:59 (4 of 5 openings).

Use it to be ready at that time, with the app open, logged in, and your card saved.

Note: a crossed-out time on the VOX site can mean "sales not open yet" or "sold out". The watcher treats both the same way: the moment it becomes clickable, you get an alert. If the watcher saw it bookable before, the alert says **Seats are back**.

---

## Running it manually

**On GitHub:** Actions > VOX watcher > Run workflow, then pick:

| Mode | What it does |
|---|---|
| `watch` | A normal check (same as the schedule) |
| `test-notify` | Sends a test push to your phone |
| `catalog` | Rebuilds `state/CATALOG.md` and prints all slugs in the run log |

**On your computer** (needs the .NET 10 SDK), from the repo root:

```bash
# print all cinema and movie slugs
dotnet run --project src/VoxWatcher -- catalog

# a full check that prints the alerts instead of sending them
dotnet run --project src/VoxWatcher -- watch --dry-run --state ./local-state

# send a real test push
NTFY_TOPIC=your-topic dotnet run --project src/VoxWatcher -- test-notify
```

Options: `--dry-run`, `--config <file>`, `--state <dir>`, `--fixtures <dir>` (offline HTML), `--now <ISO time>`.

Options for repeating inside one process: `--loop-seconds <n>` (keep checking for about n seconds) and `--interval-seconds <n>` (gap between checks, plus up to 15 s of jitter).

Environment variables:

| Variable | Meaning |
|---|---|
| `NTFY_TOPIC` | Required to send pushes |
| `NTFY_SERVER` | Default `https://ntfy.sh`. Set it for a self-hosted server |
| `NTFY_TOKEN` | For a protected topic |
| `VOX_FETCHER` | `http`, `browser` or `auto` (default). `auto` tries plain HTTP with a 10 s timeout, then switches to the browser for the rest of the run |
| `VOX_BROWSER_CHANNEL` | Browser used by `browser` mode. Default `chrome` (the installed Google Chrome). Also `msedge` or `chromium` |

In GitHub, add `NTFY_TOKEN` as a secret and `NTFY_SERVER` as a variable if you need them.

### Why a real browser

VOX is served through Akamai. Tests from a GitHub runner (AWS, ubuntu-24.04) showed:

| Client | Result |
|---|---|
| curl, .NET HttpClient | Timeout, no bytes |
| Playwright's bundled Chromium (headless shell) | `ERR_HTTP2_PROTOCOL_ERROR` |
| Installed Google Chrome, headless, normal user agent | 200, full page |

So the workflow runs with `VOX_FETCHER=browser` and `VOX_BROWSER_CHANNEL=chrome`. Google Chrome is preinstalled on the `ubuntu-24.04` runner image, which is why the workflow pins that image instead of `ubuntu-latest`.

## Testing

From the repo root (needs the .NET 10 SDK):

```bash
dotnet test
```

The tests never touch the network or a browser, and never read your `watches.json`. They live in `tests/VoxWatcher.Tests` (xUnit v3):

| File | What it covers |
|---|---|
| `ParserTests.cs` | Real pages captured from egy.voxcinemas.com: date tabs, bookable vs crossed-out times, booking links, the "no showtimes" page, the cinema and movie catalog |
| `ChangeDetectorTests.cs` | Listed, opened, sold out, seats back; a show that already started is not a sell-out, including after-midnight shows |
| `ScenarioTests.cs` | The whole watcher, run after run, against a fake VOX site: first-run summary, BOOK NOW, sell-out, seats back, a broken page (no alert, nothing lost, no repeat alerts later), the 15-minute problem alert and recovery |

`.github/workflows/ci.yml` runs the tests on every pull request and on code changes to `main`. It is separate from the watcher, so a failing test never stops your alerts.

---

## Limits and good behavior

- **Timing.** The schedule is every 5 minutes, but GitHub often starts scheduled runs 10 to 20 minutes late. To cover the gaps, each job checks about 3 times, every 100 to 115 seconds, over 4 minutes. State is committed once at the end of the job.
- **Inactive repos.** GitHub disables scheduled workflows after 60 days with no repository activity. `keepalive.yml` runs weekly and commits `state/keepalive.txt` if nothing was committed in the last 30 days.
- **Blocking.** If VOX ever blocks GitHub's servers even for a real browser, runs fail and you get the problem alert. The fix then is to run the watcher from a home machine or another network.
- **Load.** Each check makes 1 request for the catalog, 1 for the watch's first page, plus 1 per other watched date, with a 1.2 second pause between requests.
- **Broken pages.** A page that does not look like a real showtimes page (no showtimes and no "no showtimes" message) fails the check instead of being trusted, so a glitch never makes old showtimes look new.
- **Sold out vs started.** When a show's start time passes, VOX makes it unavailable. That is not counted as a sell-out. Shows before 05:00 (VOX lists a 00:00 show under the previous day) count as starting the next night.
- **No automatic booking.** The watcher only watches. Booking stays with you, which also keeps your VOX account safe.

## Troubleshooting

| You see | Do this |
|---|---|
| Red run, "Config problems" | Fix `watches.json`. The log lists each problem |
| Red X on **Save state** | Settings > Actions > General > Workflow permissions > Read and write |
| Green runs but no pushes | The `NTFY_TOPIC` secret does not match the topic in the app. Check for spaces. Then run `test-notify` |
| "Unknown cinema" warning | Use a slug from `state/CATALOG.md` |
| "VOX watcher has a problem" | Open the latest run log. A 403 means blocking; "layout" means VOX changed the page and the parser needs an update |
| Scheduled runs stopped | Actions tab > VOX watcher > Enable workflow |

## Project structure

```
VoxWatcher.slnx                  solution (open this in Visual Studio or Rider)
Directory.Build.props            settings shared by all projects (net10.0, nullable, warnings as errors)
global.json                      .NET 10 SDK, and Microsoft.Testing.Platform for dotnet test
watches.json                     what to watch (you edit this)
.github/workflows/watch.yml      schedule, build, run, save state
.github/workflows/ci.yml         build and test on pull requests and code changes
.github/workflows/keepalive.yml  keeps the schedule from being disabled
src/VoxWatcher/
  Program.cs                     entry point and modes
  Configuration/WatchConfig.cs   config model, validation, filters
  Vox/ShowtimesParser.cs         reads the VOX showtimes HTML
  Vox/HtmlSources.cs             HTTP with retries and pacing, auto mode, offline fixtures
  Vox/PlaywrightHtmlSource.cs    real browser fetcher (Google Chrome through Playwright)
  Engine/WatchRunner.cs          one run: fetch, filter, detect, notify, save
  Engine/ChangeDetector.cs       compares now vs last time
  Engine/InsightsWriter.cs       INSIGHTS.md and CATALOG.md
  Notifications/                 message wording and ntfy publishing
  State/WatcherState.cs          state model and storage
tests/VoxWatcher.Tests/
  ParserTests.cs                 real VOX HTML -> expected showtimes
  ChangeDetectorTests.cs         opened, sold out, seats back, show started
  ScenarioTests.cs               the whole watcher, run after run
  TestSupport.cs                 fake VOX site and scenario runner
  Fixtures/Real/                 pages captured from egy.voxcinemas.com
  Fixtures/Scenario/             trimmed pages the fake site is built from
```
