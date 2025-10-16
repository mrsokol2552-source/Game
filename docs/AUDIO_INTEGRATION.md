# Audio Integration (Music & SFX)

Purpose: integrate the supplemental music/SFX guidance into our architecture while keeping our project standards and scope priorities.

## Scope & Priorities

- Priority stays on core gameplay (our current Sprint 2). Audio integration lands incrementally and non‑blocking.
- Domain/Application remain Unity‑free; audio lives in Presentation/Infrastructure.
- Addressables are planned (labels per groups) but optional until content scale requires them.

## Music

- States: Calm (A), Tense (B), MainMenu/WorldMap (C), Stealth (D), Aftermath/Ruins.
- Mixing: `AudioMixer` with snapshots for states; transitions via `AudioMixer.TransitionToSnapshots()` (0.25–0.40s).
- Assets: organize under `Assets/Audio/Music/` with optional Addressables labels: `Music_A`, `Music_B`, `Music_C`, `Music_D`, `Music_Aftermath`.
- Import: 48kHz WAV sources; in Unity use Vorbis/Streaming or Compressed In Memory; Load In Background.

Planned components:
- `MusicManager` (Presentation): select state, cross‑fade tracks, drive mixer snapshots; later: Addressables load.
- `MusicConfig` (Infrastructure, optional): maps game states to clip lists or Addressables labels.

## SFX

- Groups: UI, World, Combat, Voice, Ambience; mixers: `SFX_UI`, `SFX_World`, `SFX_Combat`, `SFX_Voice`, `SFX_Ambience`; optional `SFX_Tension`, `SFX_War`, `OcclusionLPF`.
- Naming: `SFX_<Category>_<Name>_Var##.wav` (e.g., `SFX_UI_Click_V03.wav`).
- Import: 48kHz/24‑bit WAV, UI one‑shots ~1s, whooshes/alerts 180–600ms; Decompress on Load for one‑shots, Streaming for long loops.
- Folder structure:

```
Assets/Audio/
  Mixers/
  Music/
  SFX/
    UI/
    World/
    Combat/
    Voice/
    Ambience/
  Addressables/ (optional)
```

Planned components:
- `SfxManager` (Presentation): play by id/clip with random pitch/level variance; routes to mixer groups.
- Event routing: Application raises events (UI/Combat/World); Presentation subscribes and plays SFX.

## Integration Plan (Incremental)

1) Sprint 2 (optional, P3)
- Add `Audio/Mixers` asset(s) with Music/SFX groups and 2–3 snapshots (Calm/Tense).
- Add `MusicManager` (stub) + single looping track in scene; public API: `SetState(MusicState)`.
- Add `SfxManager` (stub) + UI click sound; public API: `PlayUiClick()`.

2) Later (when content grows)
- Addressables labels for music/SFX packs; async load/unload.
- Tension/War snapshot automation; occlusion/LPF for off‑screen.

## Source Notes

- Supplemental docs referenced:
  - <root>/…/G/MUSIC_INTEGRATION.md
  - <root>/…/G/SFX_INTEGRATION.md

