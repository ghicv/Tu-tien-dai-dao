# Tu Tiên Box — Repository Instructions

Read `Docs/GAME_DESIGN.md`, `Docs/WORLD_RULES.md`, `Docs/ROADMAP.md`, and `Docs/DECISIONS.md` when relevant to the task.

## Project goal
Build a Xianxia God Game where the player acts as Heavenly Dao and observes/intervenes in an autonomous cultivation world.

## Mandatory workflow
- Inspect existing files before modifying anything.
- Follow the current phase in `Docs/ROADMAP.md` and decisions in `Docs/DECISIONS.md`.
- Do not implement future phases without explicit approval.
- Explain dependencies and design trade-offs before substantial architecture changes.
- Keep changes focused and testable.
- Verify code/build/tests when tools are available; clearly state what was and was not verified.
- Update documentation when an approved architectural decision changes.

## Architecture principles
- Simulation state is authoritative.
- Rendering, UI, and input must not own simulation truth.
- Simulation time is separate from render frame time.
- Prefer simple data structures and clear system boundaries.
- Avoid premature ECS/DOTS, over-generalization, and speculative frameworks.
- Do not invent project settings, engine versions, package versions, or test results.

---

## This repository (added 2026-10-15; the sections above are the starter pack's, unchanged)

### What exists
- Unity 6000.3.9f1, URP 2D. Code in `Assets/ThienDao` (asmdef `ThienDao`; `Core`, `World`, `Sim`, `Render`, `Player`, `UI`, `Editor`, `Tests`).
- A running simulation: see `Docs/Systems.md` for every system, what it owns, what it reads and writes, and what tests cover it.
- `Docs/ROADMAP.md` has a status table at the top: the pack's phases 0–12 against what is actually built.

### Order of authority when documents disagree
1. The user's latest confirmed decision.
2. `Docs/DECISIONS.md`.
3. `Docs/Vision.md` (the product direction: three pillars, the world loop, few-but-grand sects, opens at hồng hoang).
4. `Docs/GAME_DESIGN.md`, then `Docs/WORLD_RULES.md`, then `Docs/ROADMAP.md`.
5. `Docs/Devlog/*` is history, not authority.

The old design documents (`GDD_ThienDao.md`, `WorldRuleSystem.md`) and the old generated reports were deleted on 2026-10-15 and live only in git history. Do not restore them as a source.
`Docs/WorldRules.md` is the user's own long brief for the rule system; it is not ours to edit or commit.

If documents disagree, say so and ask; do not pick silently.

### Working agreements (confirmed by the user)
- **One small task at a time**, with acceptance criteria, then report and propose the next one. Do not batch features.
- **Do not run the 1000-year balance/perf probe unless asked.** A change is checked by compiling and running the tests.
- **Naming** (creatures, techniques, places, items) follows "Phàm Nhân Tu Tiên" through `Resources/Lore/*.json` and `Sim/Lore.cs`; names of ages follow the Hồng Hoang style. Open issue: IP risk for a commercial release, see `DECISIONS.md`.
- **Pixel art only**: every visual, effects included, is pixel art (8 px per cell, point filter, snapped).
- **Performance first**: measure, stagger, cache; do not cut depth for it.
- **Every simulated activity must be visible on the map**, not only as numbers or card text.
- **A devlog per milestone**: `Docs/Devlog/NN-slug.md` plus a row in `Docs/Devlog/README.md`, the commit hash filled by a follow-up commit.
- **Commits**: never commit `.vsconfig`, `Screenshots/`, the `.gitignore` edit, or `Docs/WorldRules.md`.
- **Determinism**: a run must be replayable from seed + command log. Simulation uses `DetRandom` only (never `UnityEngine.Random`, `System.Random` or wall-clock time); powers go through `IWorldCommand`.
- **Save compatibility is not preserved yet**: structure changes may break old saves (the format has a schema check, and refuses mismatches).

### Unity MCP
- The editor for this project is instance `My project (1)@04716290`. Another Unity project (`unity_project`) is often open on the same machine: at the start of a session call `set_active_instance` and verify `Application.dataPath` ends with `My project (1)/Assets`.
- Long editor jobs must not run inside one MCP call (it times out and is re-sent). Use the menu items backed by `Editor/EditorJob.cs`, and wait on the output file.
- A full test run takes about 5–7 minutes; the editor is unresponsive during it.
