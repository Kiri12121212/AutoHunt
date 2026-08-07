# AutoHunt (HuntTrainAuto)

Dalamud plugin for FFXIV hunt trains. **Vanilla Dalamud only** — no ECommons or NightmareUI.

Logic references (not dependencies):

- [HuntTrainAssistant](https://github.com/NightmareXIV/HuntTrainAssistant) — conductor, flags, teleport
- [AutoDuty](https://github.com/erdelf/AutoDuty) — vnavmesh, Rotation Solver Reborn

## Goal

When a conductor posts a map flag:

1. Parse the flag from chat
2. Teleport if needed
3. Mount and vnavmesh to the flag
4. Unmount at the spot
5. Follow the party to the pull
6. Engage with Rotation Solver Reborn

## Required plugins (runtime)

| Plugin | Role |
|--------|------|
| vnavmesh | Pathfinding |
| Rotation Solver Reborn | Auto rotation |
| Teleporter or Lifestream | Teleport / instance change |

## Dev setup

```powershell
git clone https://github.com/KiritsuguuEmiya/AutoHunt.git
cd AutoHunt
powershell -ExecutionPolicy Bypass -File tools/setup-git-hooks.ps1
dotnet build HuntTrainAuto.sln -c Release
```

Copy `HuntTrainAuto/bin/Release/` to `%AppData%\XIVLauncher\installedPlugins\HuntTrainAuto\`.

## Custom plugin repo

Shared repo (DcNotify + HuntTrainAuto): [KiritsuguuEmiya/DalamudPlugins](https://github.com/KiritsuguuEmiya/DalamudPlugins).

Dalamud → Settings → Experimental → Custom Plugin Repositories, add:

```
https://raw.githubusercontent.com/KiritsuguuEmiya/DalamudPlugins/main/pluginmaster.json
```

Download links point at GitHub Releases `latest/download/HuntTrainAuto.zip` — **placeholder until a release zip is published**; until then use the local copy path above.

## Commands

- `/hta` — toggle config window

## Stack

- `IDalamudPluginInterface` — config save/load
- `WindowSystem` + `Window` — UI
- `ICommandManager` — slash commands
- `ICallGateSubscriber` — IPC to vnavmesh, RSR, Teleporter (to be added)

## Git hooks

Co-authored commit trailers are blocked. After clone, run `tools/setup-git-hooks.ps1` once.
