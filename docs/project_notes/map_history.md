# Project Map History

Phase-by-phase log of how the project architecture evolved. Split from `project_map.md` so the map stays oriented on current architecture — this file is the time-series log that grows forever.

Append one row per phase completion (discovery, compose, design, execute, verify, or a branch consolidation). Do NOT rewrite existing rows.

| Date | Phase | Branch | Change | Reason |
|------|-------|--------|--------|--------|
| 2026-05-31 | Discovery | trunk | Backfilled Project North Star | Section was blank scaffold; authored and confirmed by user during sprint-6 discovery |
| 2026-05-31 | Discovery (branch) | sprint-6 | Authored Branch Goal | Branch discovery complete |
| 2026-10-28 | Verify (map integrity) | godot-foundation | Removed the 10 capability slices and 13 components that Sprint 7 compose/design had added (town, conversations, character reactions, story memory, recruiting, gym hand-off, content guide and check, stand-in town); restored the verified Sprint 6 wording of 5 slices it had reworded | None of it exists in code: Sprint 7 paused in design and resumes on Godot. Designer chose "match reality"; the plan stays in `branches/sprint-7/` |
| 2026-10-28 | Verify (consolidation) | godot-foundation | Folded the Godot foundation into Overview and Operational Foundation (browser build now described as the frozen reference); renamed the branch's "designer approves" slices to product phrasing; Game host joins the session slice; Smoke run now builds the Game host | Branch consolidated into trunk architecture after verify |
| 2026-10-30 | Verify (map integrity) | sprint-7-godot | Reconciled with the live game: start screen titled with the game's name; Esc menu gains Return to Start and Quit; conversation panel grows to fit and takes arrows/Enter/mouse with no choice numbers; party screen's "Lineup full"; content check's two-round reporting noted; "as designed" wording removed now that the screens are verified | Verify walked the real game in a window (127 checks) and the designer approved these behaviour changes |
| 2026-10-30 | Verify (consolidation) | sprint-7-godot | Folded branch language ("from sprint-7-godot") into Operational Foundation; merged the five frozen browser-build slices (play with no server, browser saves, systems still work, scene stage, one-command scripts) into one "frozen browser build stays playable as the reference" slice; Overview now calls the Godot build the playable game | Coherence pass: the browser build is a frozen reference and its five delivery-era slices fragmented Capabilities |
