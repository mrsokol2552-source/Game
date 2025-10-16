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

## Controls\n\n- HUD (top-left) shows resources.\n- Keys (Input System aware):\n  - M: +10 Materials\n  - F: +5 Food\n  - B: Attempt to place Test Building (cost from Building Config or default)\n- Buttons: Save/Load use Application.persistentDataPath/save.json.\n\n## Structure

- Domain: pure C# (no Unity API)
- Application: services/use-cases (no Unity API)
- Infrastructure: persistence/config adapters
- Presentation: MonoBehaviours (UI/Input/Views)

## Sprint 2 (Scaffolds)

- Units: `UnitStats` (Domain), `UnitView` (Presentation)
- Spawner/Commander: spawn a unit prefab on LMB; set destination on RMB (placeholder)


