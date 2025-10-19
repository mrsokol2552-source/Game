# Persistence Tests Checklist (Manual)

Scope: Verify Save/Load covers Economy, Units (pos/dest/faction/HP), and Research statuses via HUD buttons.

Prerequisites
- In scene, `CompositionRoot` assigned with: `DefaultUnitPrefab`, `PlayerSprite`/`EnemySprite` (optional but recommended).
- HUD visible (from `HudController`), Dev Actions panel available (toggle via HUD → Dev).
- Save path: `UnityEngine.Application.persistentDataPath/save.json`.

Economy
- Start: Note stocks for Materials and Food shown in HUD.
- Change: press `M` (+10 Materials), `F` (+5 Food).
- Save, then Load.
- Expect: HUD shows changed values preserved after Load.

Units: Single
- Spawn Player (Dev → Spawn Unit) and Enemy (Dev → Spawn Enemy). They should appear within camera view.
- Verify: HP bars visible; Player tinted white (or player sprite), Enemy tinted red (or enemy sprite).
- Right‑click near each unit to give different destinations (if using `UnitSpawnerCommander` flow) or let them fight.
- Save, then Load.
- Expect: Units restored at saved positions; destinations preserved (if set); factions and HP restored; HP bars visible.

Units: Multiple
- Spawn 3–5 mixed units at different areas; give some destinations.
- Save, then Load.
- Expect: Count matches; per‑unit position/destination/faction/HP restored; last spawned unit bound to commander selection (optional convenience).

Research
- Open Research panel, Start first research item; optionally Complete it.
- Save, then Load.
- Expect: Research statuses preserved (Queued/Done) and reflected in UI.

Edge Cases
- No save present: press Load before any Save → expect no crash and no state change.
- Clear Save (Dev → Clear Save), confirm file removed; then Load → expect no state change.
- Mixed health: allow combat to damage a unit, Save, Load → HP preserved.

Notes (Implementation References)
- Save/Load entry points: `My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs:57` (Save), `:62` (Load).
- Save format: `My project/Assets/Scripts/Infrastructure/Persistence/SaveSystem.cs` (JsonUtility; DTO includes Stocks, Units, Research).
- Unit capture/restore: `CompositionRoot.CaptureUnitsEx/RestoreUnitsEx` set position, destination, faction, HP, and visuals.
- Dev spawns: random within camera view; HP overlay auto‑attached.

