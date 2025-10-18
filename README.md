# 2D RTS Prototype (Unity)

This repo contains the Unity project under `My project/` and design docs under `docs/`.

## Open & Run

- Open the folder `My project` in Unity Hub.
- Open `Assets/Scenes/SampleScene.unity`.
- In the scene, add an empty GameObject named `Bootstrap` and add components:
  - `CompositionRoot`
  - `HudController`
  - `InputController`
- Optionally create `GameConfig` (Create в†’ Configs в†’ Game Config) and assign it in `CompositionRoot`.

## Controls\n\n- HUD (top-left) shows resources.\n- Keys (Input System aware):\n  - M: +10 Materials\n  - F: +5 Food\n  - B: Attempt to place Test Building (cost from Building Config or default)\n- Buttons: Save/Load use Application.persistentDataPath/save.json.\n\n- R: Start Research (from Test Research config)
  - C: Complete Research (for the same entry)

## Manual Save/Load Checklist

- Setup
  - Open SampleScene; ensure `Bootstrap`, `HudController`, `InputController` present.
  - Ensure `ActionsPanel` exists (Tools → RTS → Setup → Add Actions Panel To Scene) or use HUD → Dev.
- Economy
  - Press M/F to add resources; press Save; press Load → values persist.
- Units
  - Spawn a player unit (LMB) and an enemy (E or ActionsPanel).
  - Move player (RMB) to set a destination; press Save.
  - Move both elsewhere; press Load → units return to saved positions/destinations; enemy stays red; combat resumes.
- Research
  - Open Research panel (HUD → Research). Start a research (Queued); press Save; press Load → status remains Queued.
- Clear Save
  - ActionsPanel → Clear Save (deletes file at `UnityEngine.Application.persistentDataPath/save.json`).

## Structure

- Domain: pure C# (no Unity API)
- Application: services/use-cases (no Unity API)
- Infrastructure: persistence/config adapters
- Presentation: MonoBehaviours (UI/Input/Views)

## Sprint 2 (Scaffolds)

- Units: `UnitStats` (Domain), `UnitView` (Presentation)
- Spawner/Commander: spawn a unit prefab on LMB; set destination on RMB (placeholder)



