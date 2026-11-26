// Tests for web/js/model.js. Run from the repo root: node --test tests/web/
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import * as M from "../../web/js/model.js";

const watch = (over = {}) => ({ ...M.emptyWatch(), name: "Doomsday at Almaza", ...over });
const session = (over = {}) => ({ id: "0047-1", date: "2026-12-18", time: "19:15:00", experience: "GOLD", ...over });

test("watch keys match the C# slug, so the app finds each watch's showtimes in state.json", () => {
  assert.equal(M.slugify("Doomsday at Almaza"), "doomsday-at-almaza");
  assert.equal(M.slugify("  Dune IMAX: weekend evenings (example) "), "dune-imax-weekend-evenings-example");
  assert.equal(M.slugify("!!!"), "");
});

test("the repo's watches.json parses, comments and all, and survives a save round trip", () => {
  const config = M.parseJsonc(readFileSync(new URL("../../watches.json", import.meta.url), "utf8"));
  const watches = config.watches.map(M.normalizeWatch);
  assert.ok(watches.length > 0);
  assert.deepEqual(M.validateWatches(watches), []);

  const again = M.parseJsonc(M.serializeConfig({ ...config, watches: watches.map(M.watchToJson) })).watches.map(M.normalizeWatch);
  assert.deepEqual(again, watches);
});

test("comment markers inside strings are kept", () => {
  assert.deepEqual(M.parseJsonc('{ "site": "https://x.com/a//b", /* note */ "n": [1, 2,], }'), { site: "https://x.com/a//b", n: [1, 2] });
});

test("validation catches what would stop the watcher", () => {
  const problems = M.validateWatches([
    watch(),
    watch({ name: "doomsday at almaza" }),
    watch({ name: "", dates: { from: "2027-01-02", to: "2026-12-01", on: [] } }),
    watch({ name: "Times", time: { after: "23:00", before: "18:00" } }),
    watch({ name: "Days", daysOfWeek: ["Fr"] }),
  ]);
  const fields = problems.map((p) => `${p.index}:${p.field}`);
  assert.ok(fields.includes("1:name"), "duplicate name, case-insensitive");
  assert.ok(fields.includes("2:name"), "missing name");
  assert.ok(fields.includes("2:dates"), "from after to");
  assert.ok(fields.includes("3:time"), "after later than before");
  assert.ok(fields.includes("4:days"), "day names need at least 3 letters");
});

test("filters match WatchDefinition.Matches", () => {
  assert.ok(M.matchesWatch(watch(), session()));
  assert.ok(!M.matchesWatch(watch({ dates: { from: "2026-12-24", to: "", on: [] } }), session()));
  assert.ok(!M.matchesWatch(watch({ daysOfWeek: ["Sat"] }), session()), "18 Dec 2026 is a Friday");
  assert.ok(M.matchesWatch(watch({ daysOfWeek: ["Friday"] }), session()));
  assert.ok(M.matchesWatch(watch({ experiences: ["gold"] }), session({ experience: "GOLD Class" })), "partial, case-insensitive");
  assert.ok(!M.matchesWatch(watch({ time: { after: "19:30", before: "" } }), session()));
  assert.ok(M.matchesWatch(watch({ time: { after: "12:00", before: "23:59" } }), session()));
});

test("booking links only ever point at VOX", () => {
  assert.equal(M.bookingUrl("https://egy.voxcinemas.com", "0047-276906"), "https://egy.voxcinemas.com/booking/0047-276906");
  assert.equal(M.bookingUrl("https://evil.example", "0047-1"), null);
  assert.equal(M.bookingUrl("http://egy.voxcinemas.com", "0047-1"), null);
  assert.equal(M.bookingUrl("https://egy.voxcinemas.com", "../../x"), null);
});

test("sessions come from the watch's key in state.json, in range, in order", () => {
  const state = { Watches: { "doomsday-at-almaza": { Sessions: {
    b: { Movie: "M", Cinema: "C", Experience: "GOLD", Date: "2026-12-18", Time: "22:30:00", Bookable: true },
    a: { Movie: "M", Cinema: "C", Experience: "GOLD", Date: "2026-12-18", Time: "19:15:00", Bookable: false },
    old: { Movie: "M", Cinema: "C", Experience: "GOLD", Date: "2026-11-01", Time: "19:15:00", Bookable: true },
  } } } };
  assert.deepEqual(M.sessionsOf(state, watch(), "2026-11-26").map((s) => s.id), ["a", "b"]);
});

test("events.log lines become newest-first entries", () => {
  const events = M.parseEvents(
    "2026-11-26 Thu 15:50 | BookingOpened | Doomsday at Almaza | Avengers: Doomsday | Wed 16 Dec | City Centre Almaza | Standard | 23:00 | 0047-276869\n" +
    "2026-11-26 Thu 16:10 | SoldOut | Doomsday at Almaza | Avengers: Doomsday | Wed 16 Dec | City Centre Almaza | Standard | 23:00 | 0047-276869\n");
  assert.equal(events[0].kind, "SoldOut");
  assert.deepEqual([events[1].day, events[1].clock, events[1].label, events[1].time], ["2026-11-26", "15:50", "Booking opened", "23:00"]);
});

test("opening pattern is counted in Cairo time", () => {
  const p = M.openingPattern([
    { At: "2026-11-26T09:20:00+00:00", Movie: "M", Sessions: 3, Dates: [] }, // Thu 11:20 Cairo
    { At: "2026-12-03T09:50:00+00:00", Movie: "M", Sessions: 1, Dates: [] }, // Thu 11:50
    { At: "2026-12-05T20:00:00+00:00", Movie: "M", Sessions: 1, Dates: [] }, // Sat 22:00
  ], "Africa/Cairo");
  assert.deepEqual(p.top, { day: "Thu", count: 2, from: 11, to: 11 });
  assert.equal(p.byHour[22], 1);
});

test("times and dates read like VOX", () => {
  assert.equal(M.formatTime("19:15:00"), "7:15pm");
  assert.equal(M.formatTime("00:00:00"), "12:00am");
  assert.equal(M.formatDate("2026-12-18"), "Fri 18 Dec");
});

test("every section's acetate keeps text at least 7:1, light and dark", () => {
  const ink = M.hexToRgb("#161412"), paper = M.hexToRgb("#f7f2e6"), smoke = M.hexToRgb("#141210"), ivory = M.hexToRgb("#efe9dc");
  for (const hue of ["#f3c01f", "#e8672a", "#5b9b3a", "#139e97", "#2853b3", "#7650bf"]) {
    const board = M.hexToRgb(hue);
    const light = M.composite(paper, M.solveLeafAlpha(paper, board, 0.8), board);
    assert.ok(M.contrast(light, ink) >= 7, `light ${hue}`);
    const darkBoard = M.mix(board, smoke, 0.42);
    const dark = M.composite(smoke, M.solveLeafAlpha(smoke, darkBoard, 0.02, true), darkBoard);
    assert.ok(M.contrast(dark, ivory) >= 7, `dark ${hue}`);
  }
});
