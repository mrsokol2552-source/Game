# Music Integration (Unity)

This document defines the music-side integration rules for the project.

Use it together with:

- [../README.md](../README.md)
- [../supplements_index.json](../supplements_index.json)
- [README.md](./README.md)
- [sfx_integration.md](./sfx_integration.md)
- [../../docs/gameplay_current_state.md](../../docs/gameplay_current_state.md)
- [../../docs/unity_mcp_tools.md](../../docs/unity_mcp_tools.md)

## Purpose

The current music system is built around a small set of clearly mapped soundtrack banks:

- `A = Calm`
- `B = Tense`
- `C = Main Menu / World Map`
- `D = Stealth`
- `Aftermath / Ruins = Aftermath`

Important rule:

- `C` is not part of the combat/state machine.
- Until dedicated war tracks exist, `War` reuses the `B` bank and is differentiated through mixer snapshots and exposed parameters.

## Source Asset Layout

The soundtrack source folders live under:

- [soundtrack_variants](./soundtrack_variants)

Current folder mapping:

- [aftermath_ruins](./soundtrack_variants/aftermath_ruins) -> aftermath / post-battle mood
- [variant_a_short_c_minor_100_bpm](./soundtrack_variants/variant_a_short_c_minor_100_bpm) -> calm baseline candidate
- [variant_b_short_d_sharp_minor](./soundtrack_variants/variant_b_short_d_sharp_minor) -> tense baseline candidate
- [variant_c_extended_world_map_loop_friendly](./soundtrack_variants/variant_c_extended_world_map_loop_friendly) -> main menu / world map candidate
- [variant_d_extended_orchestral_focus](./soundtrack_variants/variant_d_extended_orchestral_focus) -> heavier orchestral candidate

## Import Rules

Recommended source format:

- `48 kHz / 24-bit WAV`

Unity import defaults for long BGM:

- compression: `Vorbis`
- load type: `Streaming`
- `Load In Background` enabled where useful

Do not place background music in `Resources/`.

Use `Addressables` instead.

## Addressables Layout

Suggested labels:

- `Music_A`
- `Music_B`
- `Music_C`
- `Music_D`
- `Music_Aftermath`
- `Music_Ruins`

Suggested runtime loading:

```csharp
Addressables.LoadAssetsAsync<AudioClip>(label, clip =>
{
    // Register clip into the proper state bank.
});
```

Important rule:

- release `AsyncOperationHandle` when the scene or music bank is unloaded.

## Mixer Structure

Create a dedicated `MusicMixer` with at least these groups:

- `Main`
- `Drums`
- `Bass`
- `Strings`
- `Lead`
- `Atmos`
- `FX`

Create snapshots:

- `Calm`
- `Tense`
- `War`
- `Stealth`
- `Aftermath`

Transition between snapshots through:

```csharp
mixer.TransitionToSnapshots(new[] { targetSnapshot }, new[] { 1f }, 0.35f);
```

Recommended snapshot transition time:

- `0.25s .. 0.40s`

Useful exposed parameters:

- `Music_Tension`
- `Music_War`

## State Machine Rules

Suggested priority order:

1. `Aftermath`
2. `War`
3. `Stealth`
4. `Tense`
5. `Calm`

Suggested thresholds:

- `War in = 0.65`
- `War out = 0.55`
- `Tense in = 0.45`
- `Tense out = 0.35`
- `Stealth in = 0.60`
- `Stealth out = 0.50`
- `Hold = 12s`
- `Aftermath hold = 20s`

Hard constraints:

- `War` may select clips only from `Music_B` until a separate `Music_War` bank exists.
- `MainMenu / WorldMap` uses only `Music_C`.

## Seamless Looping and Click-Free Crossfades

Use DSP-scheduled playback with two `AudioSource` instances.

Example:

```csharp
double now = AudioSettings.dspTime;
double start = now + 0.10;

nextSource.clip = nextClip;
nextSource.PlayScheduled(start);

if (currentSource.isPlaying)
    currentSource.SetScheduledEndTime(start + 0.25);
```

This gives:

- sample-accurate starts
- reliable loop points
- click-free transitions

## Runtime Configuration Example

Suggested config file:

- `Assets/Configs/MusicConfig.json`

Example:

```json
{
  "labelMap": {
    "MainMenu": "Music_C",
    "Calm": "Music_A",
    "Tense": "Music_B",
    "War": "Music_B",
    "Stealth": "Music_D",
    "Aftermath": ["Music_Aftermath", "Music_Ruins"]
  },
  "thresholds": {
    "warIn": 0.65,
    "warOut": 0.55,
    "tenseIn": 0.45,
    "tenseOut": 0.35,
    "stealthIn": 0.60,
    "stealthOut": 0.50,
    "holdSeconds": 12,
    "aftermathHold": 20
  },
  "fallback": {
    "War": "Tense",
    "Tense": "Calm",
    "Stealth": "Calm",
    "Aftermath": "Calm"
  },
  "rotation": "DeterministicHash"
}
```

When dedicated war tracks arrive, only the mapping should change.

The code should not need structural rewrites.

## Scene Integration Checklist

1. Create a `MusicDirector` object in the scene.
2. Add two `AudioSource` components for A/B crossfading.
3. Assign the `MusicMixer` and snapshots.
4. Load clip banks by Addressables labels.
5. Run a preflight validation:
   - each required state has at least one clip
   - if `War` is empty, fallback to `Tense`
6. Keep `MainMenu / WorldMap` outside the runtime combat state machine.
7. Build Addressables before testing real loading.

## Deterministic Rotation Pattern

To avoid repeating the same track too often:

```csharp
int index = Mathf.Abs(Hash(sectorId) + cycleCounter) % clips.Count;
var clip = clips[index];
```

This keeps per-sector flavor stable while still rotating over time.

## QA Checklist

- `Music_C` is never used as a battle-state bank.
- `War` currently resolves only to `Music_B` or future `Music_War`.
- All loading is label-driven, not path-driven.
- Snapshot transitions are smooth.
- Loop boundaries are click-free.
- Music assets are not stored in `Resources/`.
- Addressables bundles are built and validated before runtime testing.

## Why This Setup

- `Addressables + labels` gives scalable loading and unloading.
- `AudioMixer + snapshots` gives smooth intensity changes.
- `DSP scheduling` gives precise loops and transitions.
- strict bank mapping prevents accidental state leakage.
