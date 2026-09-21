# SFX Integration (Unity)

This document defines the SFX-side integration rules for the project.

Use it together with:

- [../README.md](../README.md)
- [../supplements_index.json](../supplements_index.json)
- [README.md](./README.md)
- [music_integration.md](./music_integration.md)
- [../../docs/gameplay_current_state.md](../../docs/gameplay_current_state.md)
- [../../docs/unity_mcp_tools.md](../../docs/unity_mcp_tools.md)

## TL;DR

- master format: `48 kHz / 24-bit WAV`
- target true peak: `<= -1 dBTP`
- one-shot UI assets: usually short, trimmed aggressively
- world/combat assets: multiple variants per event
- mixer routing must be explicit
- use pooling and randomization in playback
- use Addressables for scalable runtime loading

## Folder Structure

Main audio SFX root:

- [sound_effects](./sound_effects)

Recommended Unity-side audio layout:

```text
Assets/Audio/
  Mixers/
  SFX/
    UI/
    World/
    Combat/
    Voice/
    Ambience/
```

Current source-library categories:

- [alert_tensewar_short_stinger](./sound_effects/alert_tensewar_short_stinger)
- [build_complete](./sound_effects/build_complete)
- [build_start_loop](./sound_effects/build_start_loop)
- [city_day_night_loops](./sound_effects/city_day_night_loops)
- [click_select_hover](./sound_effects/click_select_hover)
- [comms_beep_ready](./sound_effects/comms_beep_ready)
- [confirm_success](./sound_effects/confirm_success)
- [convoy_depart_arrive](./sound_effects/convoy_depart_arrive)
- [crowd_panic_loop](./sound_effects/crowd_panic_loop)
- [crowd_safe_loop_distant](./sound_effects/crowd_safe_loop_distant)
- [error_deny](./sound_effects/error_deny)
- [explosion_small_medium](./sound_effects/explosion_small_medium)
- [footsteps_light_heavy_loops](./sound_effects/footsteps_light_heavy_loops)
- [heavy_cannon](./sound_effects/heavy_cannon)
- [impact_dirt_concrete](./sound_effects/impact_dirt_concrete)
- [infection_tick_subtle_system_hit](./sound_effects/infection_tick_subtle_system_hit)
- [open_close_panel](./sound_effects/open_close_panel)
- [path_blocked](./sound_effects/path_blocked)
- [ping_map_marker](./sound_effects/ping_map_marker)
- [place_building_ghost](./sound_effects/place_building_ghost)
- [ptt_click_in_out](./sound_effects/ptt_click_in_out)
- [quarantine_placed_breached](./sound_effects/quarantine_placed_breached)
- [radio_shortwave_loop](./sound_effects/radio_shortwave_loop)
- [research_start_complete](./sound_effects/research_start_complete)
- [ricochet_suppression_whiz](./sound_effects/ricochet_suppression_whiz)
- [rifle_burst](./sound_effects/rifle_burst)
- [shelter_door_gate](./sound_effects/shelter_door_gate)
- [surge_outbreak](./sound_effects/surge_outbreak)
- [trench_dig_loop](./sound_effects/trench_dig_loop)
- [vehicle_idle_move_loops](./sound_effects/vehicle_idle_move_loops)
- [source_website.txt](./sound_effects/source_website.txt)

## Naming Convention

Recommended in-game naming:

```text
SFX_<Category>_<Event>_<Var##>.wav
```

Examples:

- `SFX_UI_Click_V03.wav`
- `SFX_Combat_Explosion_M_V02.wav`

## Content Rules

Recommended loudness targets:

- UI: `-18 .. -16 LUFS`
- in-game SFX: `-16 .. -12 LUFS`

Suggested generation/trim ranges:

- UI clicks: `80 .. 300 ms`
- alerts / whooshes: `180 .. 600 ms`
- impacts / doors: `300 .. 800 ms`
- gunfire / explosions: `0.3 .. 1.2 s`
- seamless short loops: `1.2 .. 1.8 s`
- ambience composites: `6 .. 10 s`

Recommended variation count:

- `5 .. 8` per event

## Import Presets

### One-shots

Use for:

- UI
- short confirmations
- short combat events

Recommended settings:

- load type: `Decompress On Load`
- compression: `PCM` or high-quality `Vorbis`
- mono whenever practical
- preload enabled

### Medium loops

Use for:

- footsteps
- build loops
- engines

Recommended settings:

- load type: `Compressed In Memory`
- compression: `Vorbis`
- mono when acceptable
- loop enabled

### Long ambience

Use for:

- city ambience
- radio loops
- background civilian layers

Recommended settings:

- load type: `Streaming`
- compression: `Vorbis`
- stereo preserved
- loop enabled

### Example editor preset script

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SfxImportPreset
{
    [MenuItem("Audio/Apply SFX Preset Selected")]
    public static void Apply()
    {
        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            var isAmbience = path.Contains("/Ambience/");
            var isUI = path.Contains("/UI/");
            var settings = importer.defaultSampleSettings;

            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.loadType = isAmbience
                ? AudioClipLoadType.Streaming
                : (isUI ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory);
            settings.compressionFormat = isUI ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
            settings.quality = isUI ? 1.0f : 0.7f;

            importer.forceToMono = !path.Contains("/Combat/") && !isAmbience && !path.Contains("_ST_");
            importer.defaultSampleSettings = settings;
            importer.preloadAudioData = !isAmbience;
            importer.SaveAndReimport();
        }
    }
}
#endif
```

## Mixer Routing

Suggested mixer groups:

- `SFX_UI`
- `SFX_World`
- `SFX_Combat`
- `SFX_Voice`
- `SFX_Ambience`

Useful exposed parameters:

- `SFX_Tension`
- `SFX_War`
- `OcclusionLPF`

Suggested behavior:

- music ducks gently under SFX in `Calm` / `Tense`
- war states reduce or disable ducking if needed

## Event-to-Sound Mapping

Examples:

- `OnUiClick` -> UI click bank
- `OnResearchStarted` -> research start cue
- `OnResearchCompleted` -> research complete cue
- `OnConvoyDepart` / `OnConvoyArrive` -> convoy cues
- `OnBuildPlaced` / `OnBuildStarted` / `OnBuildCompleted` -> building cues
- `OnFootstep(unit, mat)` -> footsteps by weight/material
- `OnFire(weapon)` -> gun bank
- `OnHit(surface)` -> impact bank
- `OnExplosionSmall` / `OnExplosionMedium` -> explosion bank
- `OnInfectionTick` -> infection pulse
- `OnOutbreak` -> outbreak cue
- `OnRadioPtt(in/out)` -> radio click bank

## Example SfxManager API

```csharp
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

public class SfxManager : MonoBehaviour
{
    public static SfxManager I;

    public AudioMixer mixer;
    public AudioMixerGroup sfxUI, sfxWorld, sfxCombat, sfxVoice, sfxAmbience;

    public List<AudioClip> uiClick;
    public List<AudioClip> buildDone;
    public List<AudioClip> rifleBurst;

    [Range(0f, 0.05f)] public float randPitch = 0.03f;
    [Range(0f, 1.5f)] public float randVolDb = 1.0f;

    private readonly Queue<AudioSource> pool = new();

    void Awake() => I = this;

    public void PlayUI(AudioClip clip)
    {
        var src = GetOneShotSource(sfxUI);
        SetupRandom(src);
        src.PlayOneShot(clip);
    }

    public void PlayWorldAt(AudioClip clip, Vector3 pos)
    {
        var src = GetOneShot3DSource(sfxWorld, pos);
        SetupRandom(src);
        src.PlayOneShot(clip);
    }

    public AudioSource PlayLoopAt(AudioClip clip, Vector3 pos, AudioMixerGroup grp)
    {
        var src = GetPooledSource();
        src.outputAudioMixerGroup = grp;
        src.transform.position = pos;
        src.loop = true;
        src.spatialBlend = grp == sfxUI ? 0f : 1f;
        src.clip = clip;
        src.Play();
        return src;
    }

    public void StopLoop(AudioSource src)
    {
        if (!src) return;
        src.Stop();
        ReturnToPool(src);
    }

    AudioSource GetPooledSource()
    {
        if (pool.Count > 0) return pool.Dequeue();
        var go = new GameObject("SFX_AudioSource");
        go.transform.parent = transform;
        var src = go.AddComponent<AudioSource>();
        src.rolloffMode = AudioRolloffMode.Custom;
        return src;
    }

    void ReturnToPool(AudioSource src)
    {
        src.clip = null;
        src.loop = false;
        pool.Enqueue(src);
    }

    AudioSource GetOneShotSource(AudioMixerGroup grp)
    {
        var src = GetPooledSource();
        src.outputAudioMixerGroup = grp;
        src.spatialBlend = 0f;
        return src;
    }

    AudioSource GetOneShot3DSource(AudioMixerGroup grp, Vector3 pos)
    {
        var src = GetPooledSource();
        src.outputAudioMixerGroup = grp;
        src.transform.position = pos;
        src.spatialBlend = 1f;
        return src;
    }

    void SetupRandom(AudioSource src)
    {
        src.pitch = 1f + Random.Range(-randPitch, randPitch);
        float volDb = Random.Range(-randVolDb, randVolDb);
        src.volume = Mathf.Pow(10f, volDb / 20f);
    }
}
```

## Spatialization, Falloff, and Occlusion

- use `2D` playback for UI
- use `3D` playback for world events
- tune custom rolloff curves per category
- use LPF or similar filtering for off-screen / occluded content

For distant sectors or off-screen LOD:

- lower volume
- reduce bandwidth
- collapse many similar loops into fewer aggregated ambience loops

## Addressables and Memory

Suggested labels:

- `SFX_UI`
- `SFX_World`
- `SFX_Combat`
- `SFX_Voice`
- `SFX_Ambience`

Recommended rule:

- load only the banks needed by the current scene/state
- release handles when they are no longer needed

## Minimal First-Pass Category Coverage

At minimum, the project should have clean coverage for:

- UI click / hover / confirm / deny
- path blocked
- build place / start / complete
- convoy depart / arrive
- footsteps
- rifle fire
- heavy cannon
- impact dirt / concrete
- explosions
- infection tick / outbreak
- radio PTT
- city ambience / radio ambience

## Test and Calibration Checklist

- no clipping
- no obvious repetition in short sessions
- acceptable loudness balance vs music
- UI remains readable over combat
- loops are seamless
- pooled audio sources are reused correctly
- off-screen audio degrades gracefully

## Integration Order

1. Finalize folder naming and bank grouping.
2. Import and apply Unity presets.
3. Create mixer groups and snapshots.
4. Build `SfxManager`.
5. Connect gameplay events.
6. Add randomization and pooling.
7. Add Addressables labels.
8. Profile memory and runtime voice count.

## Generation Prompt Presets

If more SFX are generated later, keep the output aligned with the existing library:

- short, trimmed UI cues
- multi-variant combat events
- loop-friendly ambience segments
- clean naming and category assignment
