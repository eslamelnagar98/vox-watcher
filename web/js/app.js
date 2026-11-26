// VOX Watcher web app: a reference manual lying open at a tabbed section.
// Reads watches.json (main) and state.json, catalog.json, events.log (state branch).
// Writes go through the GitHub API with a fine-grained token stored only on this device.

import * as M from "./model.js";
import { GitHub, GitHubError } from "./github.js";

const SECTIONS = [
  { id: "now", label: "Now", hue: "#f3c01f" },
  { id: "showtimes", label: "Showtimes", hue: "#e8672a" },
  { id: "watches", label: "Watches", hue: "#5b9b3a" },
  { id: "insights", label: "Insights", hue: "#139e97" },
  { id: "log", label: "Log", hue: "#2853b3" },
  { id: "setup", label: "Setup", hue: "#7650bf" },
];

const TOKEN_KEY = "vox-watcher.token";
const WORKFLOW = "watch.yml";
const STALE_MINUTES = 30;

const isLocal = ["localhost", "127.0.0.1"].includes(location.hostname);
const sampleMode = isLocal && new URLSearchParams(location.search).has("sample");

const app = {
  repo: detectRepo(),
  token: readToken(),
  config: null,
  rawText: null,
  watches: [],
  state: null,
  catalog: null,
  events: [],
  runs: null,
  errors: {},
  loading: false,
  loadedAt: null,
  section: "now",
  edit: null,
  notice: null,
  busy: new Set(),
};

// ---- Small DOM helper: text always goes in as text nodes, never as HTML ------------------------

function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs ?? {})) {
    if (v === undefined || v === null || v === false) continue;
    if (k === "class") el.className = v;
    else if (k.startsWith("on") && typeof v === "function") el.addEventListener(k.slice(2), v);
    else if (k === "style") for (const [p, val] of Object.entries(v)) el.style.setProperty(p, val);
    else if (v === true) el.setAttribute(k, "");
    else el.setAttribute(k, String(v));
  }
  for (const c of children.flat(Infinity)) {
    if (c === null || c === undefined || c === false || c === "") continue;
    el.append(c instanceof Node ? c : String(c));
  }
  return el;
}

/** Map.groupBy, for phones whose browser doesn't have it yet. */
function groupBy(list, keyOf) {
  const map = new Map();
  for (const item of list) {
    const key = keyOf(item);
    if (!map.has(key)) map.set(key, []);
    map.get(key).push(item);
  }
  return map;
}

const arrow = () => h("span", { class: "arrow", "aria-hidden": "true" }, "→");
const out = () => h("span", { class: "arrow", "aria-hidden": "true" }, "↗");

function link(href, text, { external = false, cls = "" } = {}) {
  return h("a", { href, class: cls || null, target: external ? "_blank" : null, rel: external ? "noopener noreferrer" : null }, text, external ? out() : null);
}

function githubLink(url) {
  return typeof url === "string" && url.startsWith("https://github.com/") ? url : null;
}

// ---- Repository, token, client -------------------------------------------------------------

function detectRepo() {
  if (location.hostname.endsWith(".github.io")) {
    const owner = location.hostname.split(".")[0];
    const repo = location.pathname.split("/").filter(Boolean)[0] ?? `${owner}.github.io`;
    return { owner, repo };
  }
  const [owner, repo] = (document.querySelector('meta[name="vox-repo"]')?.content ?? "").split("/");
  return { owner, repo };
}

function readToken() {
  try { return localStorage.getItem(TOKEN_KEY) ?? ""; } catch { return ""; }
}

function client() {
  return new GitHub({ ...app.repo, token: app.token });
}

const site = () => M.prop(app.config, "site") ?? "https://egy.voxcinemas.com";
const agoShort = (iso) => M.ago(iso).replace(" min ago", "m ago").replace(" h ago", "h ago");
const timeZone = () => M.prop(app.config, "timeZone") ?? "Africa/Cairo";
const today = () => M.todayIn(timeZone());
const canWrite = () => Boolean(app.token) && !sampleMode;

// ---- Loading -------------------------------------------------------------------------------

async function fetchSample(name) {
  const res = await fetch(`sample/${name}`, { cache: "no-store" });
  if (!res.ok) throw new GitHubError(res.status, `Sample file ${name} is missing.`);
  return res.text();
}

/** Sample runs are stored as "minutes ago" so the preview always looks current. */
function sampleRun(r) {
  const at = new Date(Date.now() - r.minutes_ago * 60000).toISOString();
  return { ...r, created_at: at, updated_at: at };
}

async function load() {
  if (app.loading) return;
  app.loading = true;
  renderHead();
  const gh = client();
  const read = (path, ref) => (sampleMode ? fetchSample(path) : gh.readText(path, ref));
  const [watches, state, catalog, events, runs] = await Promise.allSettled([
    read("watches.json", "main"),
    read("state.json", "state"),
    read("catalog.json", "state"),
    read("events.log", "state"),
    sampleMode ? fetchSample("runs.json").then((t) => JSON.parse(t).workflow_runs.map(sampleRun)) : gh.workflowRuns(WORKFLOW),
  ]);
  app.errors = {};

  if (watches.status === "fulfilled") {
    try {
      app.config = M.parseJsonc(watches.value ?? "{}");
      app.rawText = watches.value;
      app.watches = (M.prop(app.config, "watches") ?? []).map(M.normalizeWatch);
    } catch (e) {
      app.errors.watches = `watches.json isn't valid JSON (${e.message}). The watcher stops until it's fixed on GitHub.`;
    }
  } else app.errors.watches = watches.reason.message;

  if (state.status === "fulfilled") {
    try { app.state = state.value ? JSON.parse(state.value) : null; } catch { app.errors.state = "state.json on the state branch is unreadable."; }
  } else app.errors.state = state.reason.message;

  if (catalog.status === "fulfilled") {
    try { app.catalog = catalog.value ? JSON.parse(catalog.value) : null; } catch { app.catalog = null; }
  }

  app.events = events.status === "fulfilled" ? M.parseEvents(events.value) : [];
  if (runs.status === "fulfilled") app.runs = runs.value;
  else app.errors.runs = runs.reason.message;

  app.loading = false;
  app.loadedAt = new Date();
  render();
}

// ---- Board and acetate ---------------------------------------------------------------------

const darkQuery = matchMedia("(prefers-color-scheme: dark)");

function applyBoard() {
  const section = SECTIONS.find((s) => s.id === app.section) ?? SECTIONS[0];
  const dark = darkQuery.matches;
  const p = M.palette(section.hue, dark);
  const root = document.documentElement.style;
  root.setProperty("--board", M.rgbToHex(p.board));
  root.setProperty("--hue", section.hue);
  root.setProperty("--on-hue", M.rgbToHex(p.onHue));
  root.setProperty("--on-board", M.rgbToHex(p.onBoard));
  root.setProperty("--leaf", `rgb(${p.leaf.join(" ")} / ${p.alpha})`);
  root.setProperty("--leaf-solid", M.rgbToHex(p.leafSolid));
  root.setProperty("--text", M.rgbToHex(p.text));
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", M.rgbToHex(p.board));
  // Tabs keep full-strength hue in both modes; only the open division's board darkens at night.
  for (const s of SECTIONS) {
    const tab = document.querySelector(`.tab[data-id="${s.id}"]`);
    tab?.style.setProperty("--tab", s.hue);
    tab?.style.setProperty("--on-tab", M.rgbToHex(M.palette(s.hue, false).onHue));
  }
}

// ---- Derived facts -------------------------------------------------------------------------

function allSessions() {
  if (!app.state) return [];
  return app.watches.filter((w) => w.enabled).flatMap((w) => M.sessionsOf(app.state, w, today()).map((s) => ({ ...s, watch: w })));
}

function health() {
  const runs = app.runs ?? [];
  const finished = runs.filter((r) => r.status === "completed" && !["cancelled", "skipped"].includes(r.conclusion));
  const lastOk = finished.find((r) => r.conclusion === "success");
  const lastFinished = finished[0];
  const active = runs.find((r) => ["in_progress", "queued", "waiting", "pending"].includes(r.status));
  const hs = M.prop(app.state, "Health") ?? {};
  const staleMin = lastOk ? (Date.now() - new Date(lastOk.updated_at)) / 60000 : Infinity;
  return {
    lastOk, lastFinished, active, staleMin,
    failures: M.prop(hs, "ConsecutiveFailures") ?? 0,
    lastError: M.prop(hs, "LastError"),
    runFailed: lastFinished?.conclusion === "failure",
  };
}

function extents() {
  const sessions = allSessions();
  return {
    now: sessions.filter((s) => s.bookable).length + 1,
    showtimes: sessions.length,
    watches: app.watches.length,
    insights: (M.prop(app.state, "Openings") ?? []).length,
    log: app.events.length,
    setup: 1,
  };
}

// ---- Rendering -----------------------------------------------------------------------------

function render(turn = false) {
  renderRail();
  applyBoard();
  renderHead();
  const main = document.getElementById("main");
  const section = SECTIONS.find((s) => s.id === app.section);
  const body = {
    now: renderNow, showtimes: renderShowtimes, watches: renderWatches,
    insights: renderInsights, log: renderLog, setup: renderSetup,
  }[app.section]();
  main.replaceChildren(...[
    h("h1", { class: "board-title" }, section.label),
    body.intro ? h("p", { class: "board-intro" }, body.intro) : null,
    app.notice ? slip(app.notice) : null,
    sampleMode ? h("p", { class: "board-intro" }, "Sample data: a local preview, nothing here is live and nothing can be saved.") : null,
    h("div", { class: "leaves" }, body.leaves),
  ].filter(Boolean));
  if (turn && !matchMedia("(prefers-reduced-motion: reduce)").matches) {
    main.classList.remove("turning");
    void main.offsetWidth;
    main.classList.add("turning");
  }
}

function renderHead() {
  const head = document.querySelector(".running-head");
  if (!head) return;
  const hs = app.runs ? health() : null;
  let status = "reading…";
  let flag = "idle";
  if (hs && !app.loading) {
    if (hs.active) { status = "checking now"; flag = "busy"; }
    else if (hs.failures > 0 || hs.runFailed || hs.staleMin > STALE_MINUTES) { status = hs.lastOk ? `problem · ${agoShort(hs.lastOk.updated_at)}` : "problem"; flag = "bad"; }
    else if (hs.lastOk) { status = `checked ${agoShort(hs.lastOk.updated_at)}`; flag = "ok"; }
    else status = "no checks yet";
  } else if (app.loading && app.loadedAt) status = "refreshing…";
  head.replaceChildren(
    h("a", { class: "brand", href: "#now" }, "VOX Watcher"),
    h("p", { class: `freshness is-${flag}` }, h("span", { class: "dot", "aria-hidden": "true" }), status),
    h("button", { class: "refresh", type: "button", "aria-label": "Refresh", disabled: app.loading, onclick: () => load() }, "↻"),
  );
}

function renderRail() {
  const rail = document.querySelector(".rail");
  const ext = extents();
  // Tab height follows how much each division holds, like a manual's divider tabs.
  rail.replaceChildren(...SECTIONS.map((s) => h("a", {
    class: "tab", href: `#${s.id}`, "data-id": s.id,
    "aria-current": s.id === app.section ? "page" : null,
    style: { "--grow": String(1 + Math.log2(1 + (ext[s.id] ?? 0))) },
  }, h("span", null, s.label))));
}

function leaf(title, children, { wide = false, cls = "", id } = {}) {
  const headingId = id ?? `leaf-${title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
  return h("section", { class: `leaf${wide ? " leaf--wide" : ""}${cls ? " " + cls : ""}`, "aria-labelledby": headingId },
    h("h2", { class: "leaf-title", id: headingId }, title),
    children);
}

function slip(notice) {
  return h("p", { class: notice.kind === "error" ? "errata" : "note-slip", role: notice.kind === "error" ? "alert" : "status" },
    notice.kind === "error" ? h("strong", null, "Problem. ") : null, notice.text,
    notice.href ? [" ", link(notice.href, notice.linkText ?? "Open", { external: true })] : null);
}

function loadErrors(...keys) {
  return keys.filter((k) => app.errors[k]).map((k) => slip({ kind: "error", text: app.errors[k] }));
}

// ---- Now -----------------------------------------------------------------------------------

function renderNow() {
  const sessions = allSessions();
  const bookable = sessions.filter((s) => s.bookable);
  const waiting = sessions.filter((s) => !s.bookable);
  const leaves = [];

  const bookLeaf = bookable.length ? bookNowLeaf(bookable) : null;

  const watching = app.watches.filter((w) => w.enabled);
  const statusLeaf = leaf("The watcher", [
    loadErrors("watches", "state", "runs"),
    healthLines(),
    watching.length
      ? h("ul", { class: "plain" }, watching.map((w) => h("li", null, h("strong", null, w.name), " — ", M.describeWatch(w, app.catalog))))
      : h("p", null, "No watches are switched on. ", link("#watches", "Add one")),
    h("div", { class: "actions" },
      actionButton("check", "Check now", () => runWorkflow("watch")),
      link(`${client().repoUrl}/actions/workflows/${WORKFLOW}`, "Run history", { external: true, cls: "btn" })),
  ]);

  if (bookLeaf) leaves.push(bookLeaf, statusLeaf);
  else leaves.push(statusLeaf, leaf("Book now", h("p", null,
    sessions.length || !app.state ? "Nothing you're watching can be booked yet. You'll get an alert the moment it opens."
      : "Nothing is listed in your watches' ranges yet. You'll get an alert as soon as VOX adds showtimes.")));

  if (waiting.length) {
    const dates = [...new Set(waiting.map((s) => s.date))];
    leaves.push(leaf("Listed, not bookable yet", [
      h("p", null, `${waiting.length} showtime${waiting.length === 1 ? " is" : "s are"} on VOX but can't be booked yet, on ${dates.slice(0, 4).map(M.formatDate).join(", ")}${dates.length > 4 ? ` and ${dates.length - 4} more days` : ""}.`),
      h("p", null, link("#showtimes", "See every showtime"), " "),
    ]));
  }
  return { leaves };
}

function healthLines() {
  if (!app.runs && !app.errors.runs) return h("p", null, "Reading the check history…");
  const hs = health();
  const lines = [];
  if (hs.active) lines.push(h("p", null, h("strong", null, "Checking VOX right now. "), hs.lastOk ? `The last check finished ${M.ago(hs.lastOk.updated_at)}.` : ""));
  else if (hs.lastOk) lines.push(h("p", null, h("strong", null, `Last checked ${M.ago(hs.lastOk.updated_at)}. `), "A short run starts every 5 minutes and checks VOX twice."));
  else if (app.runs) lines.push(h("p", null, "The watcher hasn't finished a check yet."));
  if (hs.failures > 0) lines.push(slip({ kind: "error", text: `The last ${hs.failures === 1 ? "check" : `${hs.failures} checks`} couldn't read VOX: ${hs.lastError ?? "no details"}` }));
  if (hs.runFailed) lines.push(slip({ kind: "error", text: "The latest run failed on GitHub.", href: githubLink(hs.lastFinished.html_url), linkText: "Open the run" }));
  if (!hs.active && hs.staleMin > STALE_MINUTES && app.runs?.length) {
    lines.push(slip({ kind: "error", text: `No successful check for ${Math.round(hs.staleMin)} minutes. The timer may have stopped; see Setup.` }));
  }
  return lines;
}

const BOOK_NOW_LIMIT = 6;

/** Soonest first. Movie and cinema are said once when every row shares them. */
function bookNowLeaf(bookable) {
  const movies = new Set(bookable.map((s) => s.movie));
  const cinemas = new Set(bookable.map((s) => s.cinema));
  const shared = [movies.size === 1 ? [...movies][0] : null, cinemas.size === 1 ? [...cinemas][0] : null].filter(Boolean);
  const shown = app.showAllBookings ? bookable : bookable.slice(0, BOOK_NOW_LIMIT);
  return leaf("Book now", [
    shared.length ? h("p", { class: "muted" }, shared.join(" at ")) : null,
    h("ul", { class: "bookings" }, shown.map((s) => bookingRow(s, cinemas.size > 1, movies.size > 1))),
    bookable.length > shown.length
      ? h("button", { class: "btn btn-small", type: "button", onclick: () => { app.showAllBookings = true; render(); } }, `Show ${bookable.length - shown.length} more`)
      : null,
  ], { wide: true });
}

function bookingRow(s, showCinema, showMovie) {
  const url = M.bookingUrl(site(), s.id);
  return h("li", { class: "booking" },
    h("span", { class: "booking-when" },
      h("span", { class: "time" }, M.formatTime(s.time)),
      h("span", { class: "booking-date" }, M.formatDate(s.date))),
    h("span", { class: "booking-what" },
      h("span", { class: "exp" }, s.experience),
      showCinema ? ` ${s.cinema}` : null,
      showMovie ? h("span", { class: "muted" }, ` · ${s.movie}`) : null),
    url ? h("a", { class: "btn btn-primary", href: url, target: "_blank", rel: "noopener noreferrer", "aria-label": `Book ${s.experience} ${M.formatTime(s.time)} on ${M.formatDate(s.date)}` }, "Book", arrow()) : null);
}

// ---- Showtimes -----------------------------------------------------------------------------

function renderShowtimes() {
  const leaves = [...loadErrors("watches", "state")];
  if (!app.watches.length) leaves.push(leaf("No watches", h("p", null, "Add a watch to start following showtimes. ", link("#watches", "Add a watch"))));
  for (const w of app.watches) {
    const sessions = app.state ? M.sessionsOf(app.state, w, today()) : [];
    const byDate = groupBy(sessions, (s) => s.date);
    const vox = M.showtimesUrl(site(), w);
    leaves.push(leaf(w.name, [
      h("p", { class: "muted" }, w.enabled ? "" : "Paused. ", M.describeWatch(w, app.catalog)),
      byDate.size
        ? [...byDate].map(([date, list]) => dayBlock(date, list))
        : h("p", null, "Nothing listed in this watch's range yet. You'll get an alert as soon as VOX adds showtimes."),
      vox ? h("p", null, link(vox, "Open on VOX", { external: true })) : null,
    ], { wide: true, id: `st-${M.slugify(w.name)}` }));
  }
  return { intro: "Filled: book now. Face-down: listed, not bookable yet. Struck through: sold out. Days with nothing to book are folded; tap to open.", leaves };
}

/** A day with something bookable lies open; a day with nothing to book folds to one line. */
function dayBlock(date, list) {
  const rows = [...groupBy(list, (s) => s.experience)].map(([exp, shows]) => h("div", { class: "exp-row" },
    h("span", { class: "exp" }, exp),
    h("ul", { class: "times" }, shows.map(timeChip))));
  const open = list.filter((s) => s.bookable).length;
  if (open) {
    return h("div", { class: "day" },
      h("h3", { class: "day-title" }, M.formatDate(date), h("span", { class: "muted" }, ` · ${open} bookable`)),
      rows);
  }
  const sold = list.filter((s) => s.everBookable).length;
  return h("details", { class: "day day--folded" },
    h("summary", null,
      h("span", { class: "day-title" }, M.formatDate(date)),
      h("span", { class: "muted" }, `${list.length} listed, ${sold ? `${sold} sold out, ` : ""}none bookable yet`)),
    rows);
}

function timeChip(s) {
  const label = M.formatTime(s.time);
  if (s.bookable) {
    const url = M.bookingUrl(site(), s.id);
    return h("li", null, h("a", { class: "time-chip is-open", href: url, target: "_blank", rel: "noopener noreferrer", "aria-label": `Book ${s.experience} ${label} on ${M.formatDate(s.date)}` }, label, out()));
  }
  const soldOut = s.everBookable;
  return h("li", null, h("span", { class: `time-chip is-down${soldOut ? " is-sold" : ""}`, title: soldOut ? "Sold out" : "Not bookable yet" },
    label, h("span", { class: "sr-only" }, soldOut ? " (sold out)" : " (not bookable yet)")));
}

// ---- Watches -------------------------------------------------------------------------------

function renderWatches() {
  if (app.edit) return { leaves: [renderEditor()] };
  const leaves = [...loadErrors("watches")];
  if (!canWrite()) leaves.push(leaf("Read only", h("p", null, sampleMode ? "This is sample data." : "To add or change watches from here, add your GitHub token. ", sampleMode ? null : link("#setup", "Open Setup"))));
  app.watches.forEach((w, i) => {
    const alerts = [w.alerts.bookingOpened && "booking opens", w.alerts.seatsReturned && "seats come back", w.alerts.newShowtimes && "new showtimes", w.alerts.soldOut && "sold out"].filter(Boolean);
    leaves.push(leaf(w.name, [
      h("p", { class: `onoff ${w.enabled ? "is-on" : "is-off"}` }, w.enabled ? "Watching" : "Paused"),
      h("p", null, M.describeWatch(w, app.catalog)),
      h("p", { class: "muted" }, alerts.length ? `Alerts when ${alerts.join(", ")}.` : "All alerts are off."),
      h("div", { class: "actions" },
        h("button", { class: "btn", type: "button", onclick: () => startEdit(i) }, "Edit", arrow()),
        actionButton(`toggle-${i}`, w.enabled ? "Pause" : "Resume", () => togglePause(i), { requiresToken: true })),
    ], { id: `w-${i}` }));
  });
  leaves.push(leaf(app.watches.length ? "Another movie?" : "Your first watch", [
    h("p", null, "Pick the movie, cinema, dates and formats. You'll get a summary on your phone within a few minutes."),
    h("div", { class: "actions" }, h("button", { class: "btn btn-primary", type: "button", onclick: () => startEdit(-1) }, "New watch", arrow())),
  ]));
  return { intro: "Changes save to watches.json on GitHub and start a check straight away.", leaves };
}

function startEdit(index) {
  const draft = index >= 0 ? structuredClone(app.watches[index]) : M.emptyWatch();
  app.edit = { index, draft, filter: "", errors: [], pending: false, confirmDelete: false };
  app.notice = null;
  if (location.hash !== "#watches") location.hash = "#watches";
  else render(true);
}

function stopEdit(notice) {
  app.edit = null;
  app.notice = notice ?? null;
  render(true);
}

function chip(name, label, checked, onchange, { type = "checkbox", note } = {}) {
  return h("label", { class: "chip" },
    h("input", { type, name, checked, onchange: (e) => onchange(e.target.checked) }),
    h("span", { class: "hole", "aria-hidden": "true" }),
    h("span", null, label, note ? h("span", { class: "muted" }, ` ${note}`) : null));
}

function toggleIn(list, value, on) {
  const i = list.indexOf(value);
  if (on && i < 0) list.push(value);
  if (!on && i >= 0) list.splice(i, 1);
}

function renderEditor() {
  const e = app.edit;
  const d = e.draft;
  const movies = M.prop(app.catalog, "Movies") ?? [];
  const cinemas = M.prop(app.catalog, "Cinemas") ?? [];
  const seenExperiences = new Set([...M.COMMON_EXPERIENCES, ...allSessions().map((s) => s.experience), ...d.experiences]);
  const fieldErr = (f) => e.errors.filter((x) => x.field === f).map((x) => h("p", { class: "field-error" }, x.message));

  const movieList = h("div", { class: "chips", id: "movie-chips" });
  const fillMovies = () => {
    const q = e.filter.trim().toLowerCase();
    const slugOf = (m) => M.prop(m, "Slug");
    // Unfiltered, only the picked movies show, so Save stays close. Search or "Show all" lists the rest.
    const known = q
      ? movies.filter((m) => M.prop(m, "Name").toLowerCase().includes(q) || slugOf(m).includes(q))
      : e.showAllMovies ? movies : movies.filter((m) => d.movies.includes(slugOf(m)));
    const hidden = !q && !e.showAllMovies ? movies.length - known.length : 0;
    const unknown = d.movies.filter((slug) => !movies.some((m) => slugOf(m) === slug));
    movieList.replaceChildren(...[
      ...unknown.map((slug) => chip("movie", slug, true, (on) => toggleIn(d.movies, slug, on), { note: "(not on VOX yet)" })),
      ...known.map((m) => chip("movie", M.prop(m, "Name"), d.movies.includes(slugOf(m)), (on) => toggleIn(d.movies, slugOf(m), on))),
      hidden ? h("button", { class: "btn btn-small", type: "button", onclick: () => { e.showAllMovies = true; fillMovies(); } }, `Show all ${movies.length} movies`) : null,
      !known.length && q ? h("p", { class: "muted" }, "No movie on VOX matches. Add it by its VOX name below if it isn't listed yet.") : null,
    ].filter(Boolean));
  };
  fillMovies();

  const addSlug = h("input", { type: "text", id: "add-movie", placeholder: "e.g. avengers-doomsday", autocomplete: "off", autocapitalize: "none", spellcheck: "false" });
  const addExp = h("input", { type: "text", id: "add-exp", placeholder: "e.g. ScreenX", autocomplete: "off" });

  const form = h("form", { class: "editor", novalidate: true, onsubmit: (ev) => { ev.preventDefault(); saveDraft(); } },
    e.errors.length ? h("div", { class: "errata", role: "alert" }, h("strong", null, "Fix before saving. "), e.errors.map((x) => x.message).join(" ")) : null,

    h("div", { class: "field" },
      h("label", { for: "w-name" }, "Name"),
      h("input", { id: "w-name", type: "text", value: d.name, required: true, maxlength: 60, autocomplete: "off", oninput: (ev) => { d.name = ev.target.value; } }),
      fieldErr("name")),

    h("div", { class: "field" }, chip("enabled", "Watching (switch off to pause)", d.enabled, (on) => { d.enabled = on; })),

    h("fieldset", { class: "field" },
      h("legend", null, "Movies"),
      h("p", { class: "muted" }, "None picked means every movie."),
      h("input", { type: "search", "aria-label": "Find a movie", placeholder: "Find a movie", value: e.filter, oninput: (ev) => { e.filter = ev.target.value; fillMovies(); } }),
      movieList,
      h("div", { class: "inline-add" },
        h("label", { for: "add-movie" }, "Not on VOX yet? Its VOX name from the address bar:"),
        addSlug,
        h("button", { class: "btn", type: "button", onclick: () => {
          const slug = M.slugify(addSlug.value);
          if (slug) { toggleIn(d.movies, slug, true); addSlug.value = ""; fillMovies(); }
        } }, "Add"))),

    h("fieldset", { class: "field" },
      h("legend", null, "Cinemas"),
      h("p", { class: "muted" }, "None picked means every cinema."),
      h("div", { class: "chips" }, cinemas.map((c) => chip("cinema", M.prop(c, "Name"), d.cinemas.includes(M.prop(c, "Slug")), (on) => toggleIn(d.cinemas, M.prop(c, "Slug"), on))),
        d.cinemas.filter((slug) => !cinemas.some((c) => M.prop(c, "Slug") === slug)).map((slug) => chip("cinema", slug, true, (on) => toggleIn(d.cinemas, slug, on), { note: "(unknown)" })))),

    h("fieldset", { class: "field" },
      h("legend", null, "Dates"),
      h("div", { class: "pair" },
        h("label", null, "From", h("input", { type: "date", value: d.dates.from, oninput: (ev) => { d.dates.from = ev.target.value; } })),
        h("label", null, "To", h("input", { type: "date", value: d.dates.to, oninput: (ev) => { d.dates.to = ev.target.value; } }))),
      d.dates.on.length ? h("p", null, `Only on ${d.dates.on.map(M.formatDate).join(", ")}. `,
        h("button", { class: "btn btn-small", type: "button", onclick: () => { d.dates.on = []; render(); } }, "Allow any date")) : null,
      fieldErr("dates")),

    h("fieldset", { class: "field" },
      h("legend", null, "Days"),
      h("p", { class: "muted" }, "None picked means any day."),
      h("div", { class: "chips" }, M.DAY_NAMES.map((day) => chip("day", day, d.daysOfWeek.some((x) => M.parseDay(x) === M.parseDay(day)),
        (on) => { d.daysOfWeek = d.daysOfWeek.filter((x) => M.parseDay(x) !== M.parseDay(day)); if (on) d.daysOfWeek.push(day); }))),
      fieldErr("days")),

    h("fieldset", { class: "field" },
      h("legend", null, "Formats"),
      h("p", { class: "muted" }, "As VOX writes them. None picked means any format."),
      h("div", { class: "chips" }, [...seenExperiences].map((x) => chip("exp", x, d.experiences.includes(x), (on) => toggleIn(d.experiences, x, on)))),
      h("div", { class: "inline-add" },
        h("label", { for: "add-exp" }, "Another format:"),
        addExp,
        h("button", { class: "btn", type: "button", onclick: () => {
          const v = addExp.value.trim();
          if (v) { toggleIn(d.experiences, v, true); render(); }
        } }, "Add"))),

    h("fieldset", { class: "field" },
      h("legend", null, "Start time"),
      h("div", { class: "pair" },
        h("label", null, "After", h("input", { type: "time", value: d.time.after, oninput: (ev) => { d.time.after = ev.target.value; } })),
        h("label", null, "Before", h("input", { type: "time", value: d.time.before, oninput: (ev) => { d.time.before = ev.target.value; } }))),
      fieldErr("time")),

    h("fieldset", { class: "field" },
      h("legend", null, "Send an alert when"),
      h("div", { class: "chips" },
        chip("a1", "Booking opens", d.alerts.bookingOpened, (on) => { d.alerts.bookingOpened = on; }),
        chip("a2", "Seats come back", d.alerts.seatsReturned, (on) => { d.alerts.seatsReturned = on; }),
        chip("a3", "New showtimes are listed", d.alerts.newShowtimes, (on) => { d.alerts.newShowtimes = on; }),
        chip("a4", "A show sells out", d.alerts.soldOut, (on) => { d.alerts.soldOut = on; }))),

    h("div", { class: "actions editor-actions" },
      h("button", { class: "btn btn-primary", type: "submit", disabled: !canWrite() || e.pending, "aria-describedby": canWrite() ? null : "no-token" }, e.pending ? "Saving…" : "Save watch", arrow()),
      h("button", { class: "btn", type: "button", disabled: e.pending, onclick: () => stopEdit() }, "Cancel"),
      canWrite() ? null : h("p", { class: "muted", id: "no-token" }, "Saving needs your GitHub token · ", link("#setup", "Add it in Setup"))),
    e.index >= 0 ? h("div", { class: "actions" },
      h("button", { class: `btn btn-danger${e.confirmDelete ? " is-armed" : ""}`, type: "button", disabled: !canWrite() || e.pending,
        onclick: () => { if (e.confirmDelete) deleteWatch(); else { e.confirmDelete = true; render(); } } }, e.confirmDelete ? "Tap again to delete this watch" : "Delete this watch")) : null);

  return leaf(e.index >= 0 ? `Edit: ${app.watches[e.index].name}` : "New watch", form, { wide: true, cls: e.pending ? "is-pending" : "", id: "editor" });
}

/** Rebuilds watches.json from the file on GitHub, so nothing changed elsewhere is lost. */
async function writeWatches(change, message) {
  const gh = client();
  const current = await gh.readFile("watches.json", "main");
  if (app.rawText !== null && current.text !== app.rawText) {
    await load();
    throw new GitHubError(409, "watches.json changed on GitHub since this page loaded. It's reloaded now: make your change again.");
  }
  const config = M.parseJsonc(current.text);
  const key = Object.keys(config).find((k) => k.toLowerCase() === "watches") ?? "watches";
  const raw = [...(config[key] ?? [])];
  change(raw);
  const next = raw.map(M.normalizeWatch);
  const errors = M.validateWatches(next);
  if (errors.length) return errors;
  config[key] = raw;
  const text = M.serializeConfig(config);
  await gh.writeFile("watches.json", "main", text, current.sha, message);
  app.config = config;
  app.rawText = text;
  app.watches = next;
  return [];
}

/** Replaces the known fields of a watch and keeps anything else that was in watches.json. */
function mergeWatch(raw, json) {
  const known = new Set(["name", "enabled", "cinemas", "movies", "dates", "daysofweek", "experiences", "time", "alerts"]);
  const kept = Object.fromEntries(Object.entries(raw ?? {}).filter(([k]) => !known.has(k.toLowerCase())));
  return { ...json, ...kept };
}

async function saveDraft() {
  const e = app.edit;
  const trial = [...app.watches];
  if (e.index >= 0) trial[e.index] = e.draft; else trial.push(e.draft);
  e.errors = M.validateWatches(trial).filter((x) => x.index === (e.index >= 0 ? e.index : trial.length - 1) || x.field === "name");
  if (e.errors.length) { render(); return; }
  e.pending = true;
  render();
  try {
    const name = e.draft.name.trim();
    const errors = await writeWatches((raw) => {
      if (e.index >= 0) raw[e.index] = mergeWatch(raw[e.index], M.watchToJson(e.draft));
      else raw.push(M.watchToJson(e.draft));
    }, `Watch "${name}" ${e.index >= 0 ? "changed" : "added"} in the web app`);
    if (errors.length) { e.pending = false; e.errors = errors; render(); return; }
    stopEdit({ kind: "ok", text: `Saved "${name}". A check starts in about a minute, and your phone gets a summary.` });
  } catch (err) {
    e.pending = false;
    app.notice = { kind: "error", text: err.message };
    render();
  }
}

async function deleteWatch() {
  const e = app.edit;
  const name = app.watches[e.index].name;
  e.pending = true;
  render();
  try {
    await writeWatches((raw) => raw.splice(e.index, 1), `Watch "${name}" deleted in the web app`);
    stopEdit({ kind: "ok", text: `Deleted "${name}".` });
  } catch (err) {
    e.pending = false;
    app.notice = { kind: "error", text: err.message };
    render();
  }
}

async function togglePause(i) {
  const w = app.watches[i];
  await busy(`toggle-${i}`, async () => {
    await writeWatches((raw) => { raw[i] = mergeWatch(raw[i], M.watchToJson({ ...w, enabled: !w.enabled })); },
      `Watch "${w.name}" ${w.enabled ? "paused" : "resumed"} in the web app`);
    app.notice = { kind: "ok", text: `${w.enabled ? "Paused" : "Resumed"} "${w.name}".` };
  });
}

// ---- Actions -------------------------------------------------------------------------------

function actionButton(id, label, fn, { requiresToken = true, primary = false } = {}) {
  const blocked = requiresToken && !canWrite();
  const working = app.busy.has(id);
  return h("button", {
    class: `btn${primary ? " btn-primary" : ""}`, type: "button", disabled: blocked || working,
    title: blocked ? "Needs your GitHub token (Setup)" : null, onclick: fn,
  }, working ? "Working…" : label);
}

async function busy(id, fn) {
  app.busy.add(id);
  render();
  try { await fn(); }
  catch (err) { app.notice = { kind: "error", text: err instanceof GitHubError ? err.message : String(err) }; }
  finally { app.busy.delete(id); render(); }
}

async function runWorkflow(mode) {
  await busy(mode === "watch" ? "check" : "test", async () => {
    await client().runWorkflow(WORKFLOW, "main", { mode });
    app.notice = mode === "watch"
      ? { kind: "ok", text: "Check started. Results in about 3 minutes; if a run is already going, yours starts right after it." }
      : { kind: "ok", text: "Test alert on its way. Your phone should ring within a minute or two." };
    setTimeout(load, 15000);
  });
}

// ---- Insights ------------------------------------------------------------------------------

function renderInsights() {
  const tz = timeZone();
  const p = M.openingPattern(M.prop(app.state, "Openings"), tz);
  const leaves = [...loadErrors("state")];
  if (!p.total) {
    leaves.push(leaf("When VOX opens bookings", h("p", null, "No booking openings recorded yet. After VOX opens bookings a few times, this shows the usual day and hour, so you can be ready with the app open.")));
  } else {
    const maxDay = Math.max(...Object.values(p.byDay), 1);
    const maxHour = Math.max(...p.byHour, 1);
    leaves.push(leaf("When VOX opens bookings", [
      h("p", null, h("strong", null, `Most often on ${dayLong(p.top.day)}, between ${pad(p.top.from)}:00 and ${pad(p.top.to)}:59`), ` (${p.top.count} of ${p.total} opening${p.total === 1 ? "" : "s"}, Cairo time).`),
      h("figure", { class: "bars", "aria-label": "Openings by day of the week" },
        M.DAY_NAMES.map((d) => h("div", { class: "bar" },
          h("span", { class: "bar-fill", style: { "--v": String(p.byDay[d] / maxDay) } }),
          h("span", { class: "bar-label" }, d),
          h("span", { class: "sr-only" }, `: ${p.byDay[d]}`)))),
      h("figure", { class: "hours", "aria-label": "Openings by hour" },
        h("div", { class: "hour-bars" }, p.byHour.map((n, hr) => h("span", { class: "hour", title: `${pad(hr)}:00 — ${n}`, style: { "--v": String(n / maxHour) } }))),
        h("figcaption", { class: "hour-axis", "aria-hidden": "true" }, h("span", null, "00"), h("span", null, "06"), h("span", null, "12"), h("span", null, "18"), h("span", null, "23"))),
    ], { wide: true }));
    leaves.push(leaf("Recent openings", h("ul", { class: "ledger" }, p.recent.map((o) => h("li", null,
      h("span", { class: "mono" }, `${o.weekday} ${o.date.slice(8)} ${monthOf(o.date)} ${pad(o.hour)}:${pad(o.minute)}`),
      h("span", null, `${o.movie}: ${o.sessions} showtime${o.sessions === 1 ? "" : "s"} for ${o.dates.map(M.formatDate).join(", ")}`))))));
  }
  const sell = M.sellOutStats(M.prop(app.state, "SellOuts"));
  leaves.push(leaf("How fast shows sell out", sell.length
    ? h("table", { class: "ledger-table" },
      h("thead", null, h("tr", null, h("th", { scope: "col" }, "Format"), h("th", { scope: "col" }, "Sell-outs"), h("th", { scope: "col" }, "Usually"), h("th", { scope: "col" }, "Fastest"))),
      h("tbody", null, sell.map((r) => h("tr", null, h("th", { scope: "row" }, r.experience), h("td", null, String(r.count)), h("td", null, minutes(r.median)), h("td", null, minutes(r.fastest))))))
    : h("p", null, "No sell-outs recorded yet. When a bookable show sells out before it starts, this shows how long each format stays bookable.")));
  return { intro: "What the watcher has learned from VOX. Times are Cairo time.", leaves };
}

const pad = (n) => String(n).padStart(2, "0");
const monthOf = (iso) => ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"][Number(iso.slice(5, 7)) - 1];
const dayLong = (d) => ({ Sat: "Saturdays", Sun: "Sundays", Mon: "Mondays", Tue: "Tuesdays", Wed: "Wednesdays", Thu: "Thursdays", Fri: "Fridays" })[d] ?? d;
const minutes = (m) => (m < 90 ? `${Math.round(m)} min` : `${(m / 60).toFixed(1)} h`);

// ---- Log -----------------------------------------------------------------------------------

function renderLog() {
  const leaves = [];
  if (!app.events.length) leaves.push(leaf("Nothing yet", h("p", null, "Every change the watcher sees lands here: bookings opening, seats coming back, new showtimes, sell-outs.")));
  for (const [day, events] of [...groupBy(app.events.slice(0, 300), (e) => e.day)].slice(0, 30)) {
    leaves.push(leaf(M.formatDate(day), h("ul", { class: "ledger" }, events.map((e) => {
      const opened = e.kind === "BookingOpened" || e.kind === "SeatsReturned";
      const url = opened ? M.bookingUrl(site(), e.id) : null;
      return h("li", { class: `ev ev-${e.kind}` },
        h("span", { class: "mono" }, e.clock),
        h("span", { class: `ev-kind${opened ? " is-open" : ""}` }, e.label),
        h("span", null, [e.movie, e.date && e.time ? `${e.date} ${M.formatTime(e.time)}` : e.date, e.experience, e.cinema].filter(Boolean).join(" · "),
          url ? h("span", { class: "ev-link" }, link(url, "Booking page", { external: true })) : null));
    }))));
  }
  return { intro: "Every change the watcher noticed, newest first. Cairo time.", leaves };
}

// ---- Setup ---------------------------------------------------------------------------------

function renderSetup() {
  const repoUrl = client().repoUrl;
  const tokenInput = h("input", { id: "token", type: "password", autocomplete: "off", spellcheck: "false", placeholder: app.token ? "Saved on this device" : "github_pat_…" });
  const leaves = [];

  leaves.push(leaf("Your GitHub token", [
    h("p", null, app.token ? h("strong", null, "A token is saved on this device. ") : null,
      "It lets this page save your watches and start checks. It stays in this browser only and is never sent anywhere but GitHub."),
    h("ol", { class: "steps" },
      h("li", null, link("https://github.com/settings/personal-access-tokens/new", "Create a fine-grained token", { external: true })),
      h("li", null, "Repository access: ", h("strong", null, "Only select repositories"), ` → ${app.repo.repo}.`),
      h("li", null, "Permissions: ", h("strong", null, "Actions: Read and write"), " and ", h("strong", null, "Contents: Read and write"), "."),
      h("li", null, "Expiration: the longest GitHub allows. Paste it below.")),
    h("div", { class: "field" }, h("label", { for: "token" }, "Token"), tokenInput),
    h("div", { class: "actions" },
      h("button", { class: "btn btn-primary", type: "button", onclick: () => saveToken(tokenInput.value.trim()) }, "Save token", arrow()),
      app.token ? h("button", { class: "btn", type: "button", onclick: () => saveToken("") }, "Forget token") : null),
  ], { wide: true }));

  leaves.push(leaf("Alerts on your phone", [
    h("p", null, "Alerts arrive through the ntfy app, on the topic saved in the repository's NTFY_TOPIC secret. Send a test to make sure your phone rings."),
    h("div", { class: "actions" }, actionButton("test", "Send a test alert", () => runWorkflow("test-notify"))),
  ]));

  const since = Date.now() - 2 * 3600 * 1000;
  const recent = (app.runs ?? []).filter((r) => new Date(r.created_at) >= since);
  const byTimer = recent.filter((r) => r.event === "workflow_dispatch").length;
  const bySchedule = recent.filter((r) => r.event === "schedule").length;
  leaves.push(leaf("The timer", [
    app.runs ? h("p", null, `In the last 2 hours, ${byTimer} check${byTimer === 1 ? " was" : "s were"} started by your timer (or by hand) and ${bySchedule} by GitHub's own schedule.`) : loadErrors("runs"),
    app.runs && byTimer + bySchedule === 0
      ? slip({ kind: "error", text: "Nothing started a check in the last 2 hours. Set up the free cron-job.org timer described in the README.", href: `${repoUrl}#a-reliable-timer`, linkText: "How to set it up" })
      : null,
    h("p", { class: "muted" }, "GitHub's built-in schedule is unreliable, so an outside timer starts the watcher every 5 minutes; GitHub's schedule is an hourly backup."),
  ]));

  leaves.push(leaf("About", [
    h("p", null, "VOX Watcher is a personal tool. It isn't made by, or connected to, VOX Cinemas, and it never books anything for you."),
    h("ul", { class: "plain" },
      h("li", null, link(repoUrl, "Code and README", { external: true })),
      h("li", null, link(`${repoUrl}/blob/main/watches.json`, "watches.json", { external: true })),
      h("li", null, link(`${repoUrl}/tree/state`, "The watcher's memory (state branch)", { external: true })),
      h("li", null, link(`${repoUrl}/actions`, "All runs", { external: true }))),
  ]));
  return { leaves };
}

async function saveToken(token) {
  if (token) {
    try {
      await new GitHub({ ...app.repo, token }).checkToken();
    } catch (err) {
      app.notice = { kind: "error", text: err.message };
      render();
      return;
    }
  }
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token); else localStorage.removeItem(TOKEN_KEY);
  } catch { /* private mode: keep it for this visit only */ }
  app.token = token;
  app.notice = { kind: "ok", text: token ? "Token saved on this device. You can now edit watches and start checks." : "Token forgotten on this device." };
  await load();
}

// ---- Routing and refresh -------------------------------------------------------------------

function route() {
  const id = location.hash.replace("#", "").split("/")[0] || "now";
  const next = SECTIONS.some((s) => s.id === id) ? id : "now";
  const changed = next !== app.section;
  if (changed) { app.notice = null; if (next !== "watches") app.edit = null; }
  app.section = next;
  render(changed);
  if (changed) document.getElementById("main").focus({ preventScroll: true });
  if (changed) window.scrollTo({ top: 0 });
}

addEventListener("hashchange", route);
darkQuery.addEventListener("change", () => render());
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "visible" && (!app.loadedAt || Date.now() - app.loadedAt > 60000)) load();
});
setInterval(() => {
  if (document.visibilityState === "visible" && !app.edit) load();
}, app.token ? 60000 : 180000);
// Keep "checked 3 min ago" honest between refreshes.
setInterval(renderHead, 30000);

route();
load();
