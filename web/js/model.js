// Pure logic for the VOX Watcher web app. No DOM here, so Node can test it (tests/web).
// Rules mirror src/VoxWatcher (WatchConfig, ChangeDetector, InsightsWriter); keep them in step.

export const DAY_NAMES = ["Sat", "Sun", "Mon", "Tue", "Wed", "Thu", "Fri"];
const DOTNET_DAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export const COMMON_EXPERIENCES = ["Standard", "GOLD", "IMAX", "MAX", "4DX", "KIDS", "THEATRE"];

export const DEFAULT_ALERTS = Object.freeze({ bookingOpened: true, seatsReturned: true, newShowtimes: true, soldOut: false });

/** Same as WatchDefinition.Key in C#: lower case, every non letter/digit becomes "-", runs collapsed. */
export function slugify(value) {
  return [...String(value ?? "").trim().toLowerCase()]
    .map((c) => (/[\p{L}\p{N}]/u.test(c) ? c : "-"))
    .join("")
    .split("-")
    .filter(Boolean)
    .join("-");
}

/** Parses watches.json, which allows // and /* *\/ comments and trailing commas (like the C# loader). */
export function parseJsonc(text) {
  let out = "";
  let inString = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    const next = text[i + 1];
    if (inString) {
      out += c;
      if (c === "\\") { out += next ?? ""; i++; }
      else if (c === '"') inString = false;
      continue;
    }
    if (c === '"') { inString = true; out += c; continue; }
    if (c === "/" && next === "/") { while (i < text.length && text[i] !== "\n") i++; out += "\n"; continue; }
    if (c === "/" && next === "*") { i += 2; while (i < text.length && !(text[i] === "*" && text[i + 1] === "/")) i++; i++; continue; }
    out += c;
  }
  // Trailing commas: a comma followed only by whitespace before } or ].
  out = out.replace(/,(\s*[}\]])/g, "$1");
  return JSON.parse(out);
}

export function serializeConfig(config) {
  return JSON.stringify(config, null, 2) + "\n";
}

/** Reads a property case-insensitively, as the C# loader does. */
export function prop(obj, name) {
  if (!obj || typeof obj !== "object") return undefined;
  if (name in obj) return obj[name];
  const key = Object.keys(obj).find((k) => k.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : obj[key];
}

/** A watch in one predictable shape, whatever casing or omissions watches.json used. */
export function normalizeWatch(raw) {
  const dates = prop(raw, "dates");
  const time = prop(raw, "time");
  const alerts = prop(raw, "alerts") ?? {};
  return {
    name: String(prop(raw, "name") ?? ""),
    enabled: prop(raw, "enabled") !== false,
    cinemas: [...(prop(raw, "cinemas") ?? [])],
    movies: [...(prop(raw, "movies") ?? [])],
    dates: {
      from: prop(dates, "from") ?? "",
      to: prop(dates, "to") ?? "",
      on: [...(prop(dates, "on") ?? [])],
    },
    daysOfWeek: [...(prop(raw, "daysOfWeek") ?? [])],
    experiences: [...(prop(raw, "experiences") ?? [])],
    time: { after: prop(time, "after") ?? "", before: prop(time, "before") ?? "" },
    alerts: {
      bookingOpened: prop(alerts, "bookingOpened") ?? DEFAULT_ALERTS.bookingOpened,
      seatsReturned: prop(alerts, "seatsReturned") ?? DEFAULT_ALERTS.seatsReturned,
      newShowtimes: prop(alerts, "newShowtimes") ?? DEFAULT_ALERTS.newShowtimes,
      soldOut: prop(alerts, "soldOut") ?? DEFAULT_ALERTS.soldOut,
    },
  };
}

export function emptyWatch() {
  return normalizeWatch({});
}

/** The JSON written back to watches.json. Empty optional groups are left out. */
export function watchToJson(w) {
  const out = { name: w.name.trim(), enabled: w.enabled, cinemas: w.cinemas, movies: w.movies };
  const dates = {};
  if (w.dates.from) dates.from = w.dates.from;
  if (w.dates.to) dates.to = w.dates.to;
  if (w.dates.on.length) dates.on = [...w.dates.on].sort();
  if (Object.keys(dates).length) out.dates = dates;
  out.daysOfWeek = w.daysOfWeek;
  out.experiences = w.experiences;
  const time = {};
  if (w.time.after) time.after = w.time.after;
  if (w.time.before) time.before = w.time.before;
  if (Object.keys(time).length) out.time = time;
  out.alerts = { ...w.alerts };
  return out;
}

export function parseDay(value) {
  const v = String(value ?? "").trim();
  if (v.length < 3) return -1;
  return DOTNET_DAYS.findIndex((d) => d.toLowerCase().startsWith(v.toLowerCase()));
}

const TIME_RE = /^([01]?\d|2[0-3]):([0-5]\d)(?::([0-5]\d))?$/;

export function minutesOf(time) {
  const m = TIME_RE.exec(String(time ?? "").trim());
  return m ? Number(m[1]) * 60 + Number(m[2]) : null;
}

/** Same checks as WatchConfig.Validate, plus what the form can catch early. */
export function validateWatches(watches) {
  const errors = [];
  const names = new Set();
  watches.forEach((w, i) => {
    const label = w.name.trim() || `Watch ${i + 1}`;
    if (!w.name.trim()) errors.push({ index: i, field: "name", message: "Give this watch a name." });
    else if (names.has(w.name.trim().toLowerCase())) errors.push({ index: i, field: "name", message: `Another watch is already called "${w.name.trim()}".` });
    else names.add(w.name.trim().toLowerCase());
    if (!slugify(w.name)) errors.push({ index: i, field: "name", message: "The name needs at least one letter or digit." });
    if (w.dates.from && w.dates.to && w.dates.from > w.dates.to) errors.push({ index: i, field: "dates", message: `[${label}] "From" is after "To".` });
    for (const d of w.daysOfWeek) if (parseDay(d) < 0) errors.push({ index: i, field: "days", message: `[${label}] Unknown day "${d}".` });
    for (const key of ["after", "before"]) {
      if (w.time[key] && minutesOf(w.time[key]) === null) errors.push({ index: i, field: "time", message: `[${label}] "${w.time[key]}" is not a time like 18:00.` });
    }
    if (w.time.after && w.time.before && minutesOf(w.time.after) > minutesOf(w.time.before)) {
      errors.push({ index: i, field: "time", message: `[${label}] "Starts after" is later than "Starts before".` });
    }
  });
  return errors;
}

export function dayOfWeek(isoDate) {
  const [y, m, d] = isoDate.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d)).getUTCDay();
}

/** WatchDefinition.MatchesDate + Matches, for a session from state.json. */
export function matchesWatch(w, s) {
  const date = s.date;
  if (w.dates.from && date < w.dates.from) return false;
  if (w.dates.to && date > w.dates.to) return false;
  if (w.dates.on.length && !w.dates.on.includes(date)) return false;
  if (w.daysOfWeek.length && !w.daysOfWeek.map(parseDay).includes(dayOfWeek(date))) return false;
  if (w.experiences.length && !w.experiences.some((e) => s.experience.toLowerCase().includes(String(e).trim().toLowerCase()))) return false;
  const t = minutesOf(s.time);
  if (t !== null && w.time.after && t < minutesOf(w.time.after)) return false;
  if (t !== null && w.time.before && t > minutesOf(w.time.before)) return false;
  return true;
}

export function sessionsOf(state, watch, today) {
  const ws = prop(prop(state, "Watches"), slugify(watch.name));
  const sessions = prop(ws, "Sessions") ?? {};
  return Object.entries(sessions)
    .map(([id, s]) => ({
      id,
      movie: prop(s, "Movie") ?? "",
      cinema: prop(s, "Cinema") ?? "",
      experience: prop(s, "Experience") ?? "",
      date: prop(s, "Date") ?? "",
      time: prop(s, "Time") ?? "",
      bookable: prop(s, "Bookable") === true,
      everBookable: prop(s, "EverBookable") === true,
      openedAt: prop(s, "OpenedAt") ?? null,
    }))
    .filter((s) => s.date >= today && matchesWatch(watch, s))
    .sort((a, b) => (a.date + a.time).localeCompare(b.date + b.time) || a.experience.localeCompare(b.experience));
}

/** Booking links only ever point at a VOX site, whatever watches.json says. */
export function bookingUrl(site, id) {
  let url;
  try { url = new URL(site); } catch { return null; }
  if (url.protocol !== "https:" || !(url.hostname === "voxcinemas.com" || url.hostname.endsWith(".voxcinemas.com"))) return null;
  if (!/^[\w-]{1,40}$/.test(id)) return null;
  return `${url.origin}/booking/${id}`;
}

export function showtimesUrl(site, watch, date) {
  let url;
  try { url = new URL("/showtimes", site); } catch { return null; }
  if (url.protocol !== "https:" || !url.hostname.endsWith("voxcinemas.com")) return null;
  for (const c of watch.cinemas) url.searchParams.append("c", c);
  for (const m of watch.movies) url.searchParams.append("m", m);
  if (date) url.searchParams.set("d", date.replaceAll("-", ""));
  return url.toString();
}

const EVENT_KINDS = {
  BookingOpened: "Booking opened",
  SeatsReturned: "Seats back",
  SessionListed: "Listed",
  SoldOut: "Sold out",
  DateAdded: "New date",
  MovieListed: "New movie",
};

/** events.log: "2026-11-26 Thu 15:50 | BookingOpened | watch | movie | Wed 16 Dec | cinema | exp | 23:00 | id" */
export function parseEvents(text) {
  return String(text ?? "")
    .split(/\r?\n/)
    .filter((l) => l.includes("|"))
    .map((line) => {
      const [at, kind, watch, movie, date, cinema, experience, time, id] = line.split("|").map((p) => p.trim());
      const [day, , clock] = (at ?? "").split(" ");
      return { day, clock, kind, label: EVENT_KINDS[kind] ?? kind, watch, movie, date, cinema, experience, time, id };
    })
    .reverse();
}

function cairoParts(iso, timeZone) {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone, weekday: "short", hour: "2-digit", hourCycle: "h23", year: "numeric", month: "2-digit", day: "2-digit", minute: "2-digit",
  }).formatToParts(new Date(iso));
  const get = (t) => parts.find((p) => p.type === t)?.value;
  return { weekday: get("weekday"), hour: Number(get("hour")), date: `${get("year")}-${get("month")}-${get("day")}`, minute: Number(get("minute")) };
}

export function todayIn(timeZone, now = new Date()) {
  return cairoParts(now.toISOString(), timeZone).date;
}

/** Same idea as InsightsWriter: the busiest weekday and its hour window. */
export function openingPattern(openings, timeZone) {
  const list = (openings ?? []).map((o) => ({ ...cairoParts(prop(o, "At"), timeZone), movie: prop(o, "Movie"), sessions: prop(o, "Sessions"), dates: prop(o, "Dates") ?? [], at: prop(o, "At") }));
  const byDay = Object.fromEntries(DAY_NAMES.map((d) => [d, 0]));
  const byHour = Array(24).fill(0);
  for (const o of list) { byDay[o.weekday] = (byDay[o.weekday] ?? 0) + 1; byHour[o.hour]++; }
  let top = null;
  for (const d of DAY_NAMES) {
    if (byDay[d] > 0 && (!top || byDay[d] > top.count)) {
      const hours = list.filter((o) => o.weekday === d).map((o) => o.hour);
      top = { day: d, count: byDay[d], from: Math.min(...hours), to: Math.max(...hours) };
    }
  }
  return { total: list.length, byDay, byHour, top, recent: [...list].sort((a, b) => b.at.localeCompare(a.at)).slice(0, 12) };
}

export function sellOutStats(sellOuts) {
  const groups = new Map();
  for (const s of sellOuts ?? []) {
    const exp = prop(s, "Experience") ?? "";
    if (!groups.has(exp)) groups.set(exp, []);
    groups.get(exp).push(Number(prop(s, "MinutesBookable")));
  }
  return [...groups.entries()]
    .map(([experience, mins]) => ({ experience, count: mins.length, median: median(mins), fastest: Math.min(...mins) }))
    .sort((a, b) => a.median - b.median);
}

function median(values) {
  const v = [...values].sort((a, b) => a - b);
  if (!v.length) return 0;
  return v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
}

/** "19:15:00" -> "7:15pm", the way VOX writes it. */
export function formatTime(time) {
  const m = minutesOf(time);
  if (m === null) return String(time ?? "");
  const h = Math.floor(m / 60);
  const min = String(m % 60).padStart(2, "0");
  return `${h % 12 || 12}:${min}${h < 12 ? "am" : "pm"}`;
}

/** "2026-12-18" -> "Fri 18 Dec", like the watcher's messages. */
export function formatDate(isoDate) {
  const [y, m, d] = isoDate.split("-").map(Number);
  if (!y || !m || !d) return isoDate;
  return `${WEEKDAYS[dayOfWeek(isoDate)]} ${String(d).padStart(2, "0")} ${MONTHS[m - 1]}`;
}

export function formatRange(from, to) {
  if (from && to) return `${formatDate(from)} – ${formatDate(to)}`;
  if (from) return `from ${formatDate(from)}`;
  if (to) return `until ${formatDate(to)}`;
  return "";
}

export function ago(iso, now = new Date()) {
  const s = Math.round((now - new Date(iso)) / 1000);
  if (s < 45) return "just now";
  const min = Math.round(s / 60);
  if (min < 60) return `${min} min ago`;
  const h = Math.round(min / 60);
  if (h < 24) return `${h} h ago`;
  const d = Math.round(h / 24);
  return d === 1 ? "yesterday" : `${d} days ago`;
}

/** One sentence that describes a watch, using catalog names where known. */
export function describeWatch(w, catalog) {
  const nameOf = (list, slug) => {
    const item = (list ?? []).find((x) => prop(x, "Slug") === slug);
    return item ? prop(item, "Name") : slug;
  };
  const movies = w.movies.length ? w.movies.map((m) => nameOf(prop(catalog, "Movies"), m)).join(", ") : "Every movie";
  const cinemas = w.cinemas.length ? w.cinemas.map((c) => nameOf(prop(catalog, "Cinemas"), c)).join(", ") : "every cinema";
  const parts = [`${movies} at ${cinemas}`];
  if (w.dates.on.length) parts.push(`on ${w.dates.on.map(formatDate).join(", ")}`);
  const range = formatRange(w.dates.from, w.dates.to);
  if (range) parts.push(range);
  if (w.daysOfWeek.length) parts.push(w.daysOfWeek.join(", "));
  if (w.time.after || w.time.before) {
    parts.push(w.time.after && w.time.before ? `starting ${formatTime(w.time.after)}–${formatTime(w.time.before)}`
      : w.time.after ? `starting after ${formatTime(w.time.after)}` : `starting before ${formatTime(w.time.before)}`);
  }
  parts.push(w.experiences.length ? w.experiences.join(", ") : "any format");
  return parts.join(" · ");
}

// ---- Acetate over the section board -------------------------------------------------------

export function hexToRgb(hex) {
  const h = hex.replace("#", "");
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16));
}

export function rgbToHex(rgb) {
  return "#" + rgb.map((v) => Math.round(v).toString(16).padStart(2, "0")).join("");
}

export function luminance([r, g, b]) {
  const lin = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

export function contrast(a, b) {
  const [l1, l2] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (l1 + 0.05) / (l2 + 0.05);
}

/** sRGB source-over, the way the browser composites a translucent leaf on the board. */
export function composite(top, alpha, bottom) {
  return top.map((v, i) => v * alpha + bottom[i] * (1 - alpha));
}

/**
 * The least opaque leaf that still lands the reading field in a fixed luminance band:
 * at least `target` in light mode, at most `target` in dark mode. Binary search on alpha.
 */
export function solveLeafAlpha(leaf, board, target, dark = false) {
  const ok = (a) => (dark ? luminance(composite(leaf, a, board)) <= target : luminance(composite(leaf, a, board)) >= target);
  if (ok(0)) return 0;
  let lo = 0, hi = 1;
  for (let i = 0; i < 24; i++) {
    const mid = (lo + hi) / 2;
    if (ok(mid)) hi = mid; else lo = mid;
  }
  return Math.ceil(hi * 1000) / 1000;
}

/** Ink or paper, whichever reads better on a colour. */
export function onColor(bg, ink, paper) {
  return contrast(bg, ink) >= contrast(bg, paper) ? ink : paper;
}

export function mix(a, b, weightA) {
  return a.map((v, i) => v * weightA + b[i] * (1 - weightA));
}
