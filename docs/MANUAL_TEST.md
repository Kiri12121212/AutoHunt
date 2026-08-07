# Manual test checklist

In-game QA for the hunt-train loop. Requires vnavmesh, Rotation Solver Reborn, and Teleporter or Lifestream. Assign a conductor before each scenario.

Use a Debug tab / log if available: confirm state transitions, follow target, mount/unmount events.

## Prerequisites

- [ ] Plugin loads; `/hta` opens config
- [ ] Conductor set (UI, context menu, or command)
- [ ] Required plugins enabled and IPC healthy
- [ ] Plugin master toggle on

---

## 1. Happy path (flying zone)

Zone that allows flight (e.g. most ARR/HW/SB/EW hunt maps with flying unlocked).

Conductor posts a map flag in another territory (or far same-zone so TP triggers).

- [ ] Flag parsed; map opens / flag set
- [ ] Teleport to nearest aetheryte (or skip TP if already close same-zone)
- [ ] Same-territory, different-instance flag → `SwitchInstance` (Lifestream instance change; no zone TP)
- [ ] Mount after arrival / before nav
- [ ] vnavmesh pathfinding starts **flying** (mounted + fly supported)
- [ ] Arrives within flag tolerance; path stops
- [ ] Unmounts at the spot
- [ ] Follows party (conductor preferred when present)
- [ ] On party engage / mob in range: stops follow, engages with RSR
- [ ] Next conductor flag: combat clears; loop restarts (TP/nav as needed)

Fail if: stays mounted into follow, starts RSR at the flag before party pulls, or never flies in a fly-capable zone when mounted.

---

## 2. Ground-only zone

Territory where flight is unavailable (or flying not unlocked for the character).

Conductor posts a flag that forces mount → ground nav.

- [ ] Teleport (if needed) completes
- [ ] Mounts when configured
- [ ] Pathfinds on **ground** (no fly attempt / no stuck waiting to take off)
- [ ] Unmounts at flag
- [ ] Follow → fight as in happy path

Fail if: plugin tries to fly, hangs in mount/fly state, or refuses to navigate.

---

## 3. Conductor offline / missing fallback

After unmount at a flag, with follow enabled. Conductor must **not** be a usable follow target — name-match on any visible player wins even if they are out of party. Setup: conductor offline, wrong name, different instance, or otherwise out of the object table (not “not in party”).

Expected priority: **conductor → party leader → nearest in-combat party ally**.

### 3a. Party leader fallback

- [ ] Conductor not visible / not name-matched (see setup above)
- [ ] Party has a leader other than you
- [ ] Plugin follows the **party leader**
- [ ] When leader (or party) engages: enter combat / RSR as designed

### 3b. In-combat ally fallback

- [ ] Conductor not visible / not name-matched
- [ ] Party leader also missing / not followable (or you are leader)
- [ ] At least one party ally is in combat nearby
- [ ] Plugin follows the **nearest in-combat ally**
- [ ] Engages that pull when in range

### 3c. Soft-fail

- [ ] No conductor, no leader, no in-combat ally nearby
- [ ] Follow clears / stays disabled (no crash, no endless path spam)

Fail if: hard-locks on missing conductor, follows a random non-party NPC, or throws / disables the plugin.

---

## Smoke (optional)

- [ ] Disable optional IPC (e.g. Lifestream) — plugin degrades without crash
- [ ] Leave hunting territory — full leave cleanup always clears conductors (no toggle)
- [ ] Reload plugin mid-train — recovers cleanly or stops safely
