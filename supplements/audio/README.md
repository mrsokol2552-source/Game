# Audio Supplements

This directory contains audio-side support material that is not yet wired directly into runtime code.

Main documents:

- [../supplements_index.json](../supplements_index.json)
- [music_integration.md](./music_integration.md)
- [sfx_integration.md](./sfx_integration.md)

Source libraries:

- [soundtrack_variants](./soundtrack_variants)
- [sound_effects](./sound_effects)

Recommended use:

- treat these files as implementation references, not as authoritative runtime documentation;
- use [../supplements_index.json](../supplements_index.json) for machine routing into soundtrack banks and SFX categories;
- when the audio runtime is actually added to the project, reflect the final decisions back into:
  - [../../docs/gameplay_current_state.md](../../docs/gameplay_current_state.md)
  - [../../docs/code_map.md](../../docs/code_map.md)
