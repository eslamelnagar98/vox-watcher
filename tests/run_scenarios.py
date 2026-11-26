#!/usr/bin/env python3
"""
Offline end-to-end test for the VOX watcher.

Builds fake "VOX site" snapshots from real HTML captured from egy.voxcinemas.com
(tests/fixtures/source), then runs the watcher against them step by step:

  1. baseline         first run, silent except a "Watching" summary
  2. booking opens    GOLD 7:15pm on Fri 18 Dec becomes bookable + new date 24 Dec listed
  3. sells out        that session becomes unavailable again (logged, no push by default)
  4. seats return     it becomes bookable again ("Seats are back")
  4a. one date page broken   no push, no showtimes lost
  4b. next normal run        no "new showtimes" or "BOOK NOW" re-announcements
  5. site broken x3   health alert after 3 failures in a row
  6. site recovers    "working again" message
  7. show started     a bookable session turns unavailable after its start time: no SoldOut, no sell-out record

Run from the repo root:  python3 tests/run_scenarios.py
"""
import os, re, shutil, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "tests", "fixtures", "source")
BASE_Q = "c_city-centre-almaza_m_avengers-doomsday"


def read(name):
    with open(os.path.join(SRC, name), encoding="utf-8") as f:
        return f.read()


CATALOG, NAV, D18, D20 = read("catalog.html"), read("nav.html"), read("d18.html"), read("d20.html")


def shift_ids(html, offset):
    return re.sub(r"0047-(\d+)", lambda m: f"0047-{int(m.group(1)) + offset}", html)


def make_site(open_7_15=False, add_24=False, broken=False, broken_date=None):
    """Returns {filename: html} for one moment in time."""
    if broken:
        return {"_root.html": "<html><body>Service unavailable</body></html>",
                "_no-showtimes.html": "<html><body>Service unavailable</body></html>"}

    nav = NAV
    if add_24:
        nav = nav.replace("</ol>\n</div>", '<li>\n<a href="/showtimes?c&#x3D;city-centre-almaza&amp;m&#x3D;avengers-doomsday&amp;d&#x3D;20261224">Thu 24 Dec</a>\n</li>\n</ol>\n</div>')

    d18 = D18
    if open_7_15:
        d18 = d18.replace('<li data-id="0047-276906">\n<span class="action showtime unavailable">7:15pm</span>',
                          '<li data-id="0047-276906">\n<a class="action showtime" href="https://egy.voxcinemas.com/booking/0047-276906" data-id="0047-276906">7:15pm </a>')

    days = {16: shift_ids(D18, 5000), 17: shift_ids(D18, 6000), 18: d18, 19: shift_ids(D18, 7000),
            20: D20, 21: shift_ids(D20, 8000), 22: shift_ids(D20, 9000), 23: shift_ids(D20, 10000)}
    if add_24:
        days[24] = shift_ids(D18, 11000)
    if broken_date is not None:
        days[broken_date] = None

    page = lambda body: CATALOG + "\n" + nav + "\n" + body
    files = {"_root.html": CATALOG,
             "_no-showtimes.html": CATALOG + "<p>No showtimes could be found for your current cinema(s), movie(s) and date.</p>",
             f"{BASE_Q}.html": page(days[16])}
    for d, body in days.items():
        files[f"{BASE_Q}_d_202612{d:02d}.html"] = page(body) if body is not None else "<html><body>Access Denied</body></html>"
    return files


def run(step, files, state_dir, now):
    fx = tempfile.mkdtemp()
    for name, html in files.items():
        with open(os.path.join(fx, name), "w", encoding="utf-8") as f:
            f.write(html)
    print(f"\n========== {step} ==========")
    out = subprocess.run(
        ["dotnet", "run", "--project", "src/VoxWatcher", "-c", "Release", "--no-build", "--",
         "watch", "--dry-run", "--fixtures", fx, "--state", state_dir, "--now", now],
        cwd=ROOT, capture_output=True, text=True)
    print(out.stdout.rstrip())
    if out.returncode != 0:
        print(out.stderr)
        sys.exit(out.returncode)
    shutil.rmtree(fx)
    return out.stdout


def showtime_count(output):
    m = re.search(r"\| showtimes: (\d+) \| bookable: (\d+)", output)
    return (int(m.group(1)), int(m.group(2))) if m else None


def no_push(output, step):
    if "[PUSH" in output:
        print(f"FAIL: {step} should not push anything"); sys.exit(1)
    print("  ok: no push")


def expect(output, text):
    if text not in output:
        print(f"\nFAIL: expected to see: {text}")
        sys.exit(1)
    print(f"  ok: {text}")


if __name__ == "__main__":
    subprocess.run(["dotnet", "build", "src/VoxWatcher", "-c", "Release", "-v", "q", "-nologo"], cwd=ROOT, check=True)
    state = tempfile.mkdtemp()

    o = run("1 baseline", make_site(), state, "2026-11-26T10:00:00Z")
    expect(o, "[PUSH p4] Watching: Doomsday at Almaza")
    expect(o, "Sun 20 Dec | CC Almaza | Standard: 3:30pm, 11:00pm")

    o = run("2 booking opens + new date", make_site(open_7_15=True, add_24=True), state, "2026-11-30T09:05:00Z")
    expect(o, "[PUSH p5] BOOK NOW: Avengers: Doomsday")
    expect(o, "Fri 18 Dec | CC Almaza | GOLD: 7:15pm")
    expect(o, "Also listed but not bookable yet")

    o = run("3 sells out", make_site(add_24=True), state, "2026-11-30T09:47:00Z")
    expect(o, "SoldOut")
    if "[PUSH" in o:
        print("FAIL: sold out should not push by default"); sys.exit(1)

    o = run("4 seats return", make_site(open_7_15=True, add_24=True), state, "2026-11-30T13:20:00Z")
    expect(o, "[PUSH p5] Seats are back: Avengers: Doomsday")

    counts_before = showtime_count(o)

    o = run("4a one date page broken", make_site(open_7_15=True, add_24=True, broken_date=20), state, "2026-11-30T13:25:00Z")
    expect(o, "FAILED (1 in a row)")
    expect(o, "Sun 20 Dec")
    no_push(o, "4a")

    o = run("4b next normal run", make_site(open_7_15=True, add_24=True), state, "2026-11-30T13:30:00Z")
    no_push(o, "4b")
    if "SessionListed" in o or "BookingOpened" in o:
        print("FAIL: 4b re-announced showtimes after a broken page"); sys.exit(1)
    if showtime_count(o) != counts_before:
        print(f"FAIL: showtime count changed {counts_before} -> {showtime_count(o)}"); sys.exit(1)
    print(f"  ok: showtimes unchanged {counts_before}")

    for i, t in enumerate(["14:00", "14:05", "14:10"], 1):
        o = run(f"5.{i} site broken", make_site(broken=True), state, f"2026-11-30T{t}:00Z")
    expect(o, "[PUSH p4] VOX watcher has a problem")

    o = run("6 site recovers", make_site(open_7_15=True, add_24=True), state, "2026-11-30T14:15:00Z")
    expect(o, "VOX watcher is working again")

    # 7. Fri 18 Dec 19:30 Cairo (17:30 UTC): the 7:15pm GOLD show started, VOX closes it. Not a sell-out.
    o = run("7 show already started", make_site(add_24=True), state, "2026-12-18T17:30:00Z")
    if "SoldOut" in o:
        print("FAIL: a show that already started was counted as sold out"); sys.exit(1)
    print("  ok: no SoldOut for a started show")
    no_push(o, "7")
    insights = open(os.path.join(state, "INSIGHTS.md"), encoding="utf-8").read()
    expect(insights, "| GOLD | 1 |")  # still only the one real sell-out from step 3

    print("\n---------- INSIGHTS.md ----------")
    print(open(os.path.join(state, "INSIGHTS.md"), encoding="utf-8").read())
    print("---------- events.log ----------")
    print(open(os.path.join(state, "events.log"), encoding="utf-8").read())
    print("ALL SCENARIOS PASSED")
