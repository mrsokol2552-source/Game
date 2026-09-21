# Supplements

This directory contains non-runtime support material that is still relevant to project planning and future implementation.

It is split by role:

- [supplements_index.json](./supplements_index.json) - machine-readable supplement index
- [audio](./audio/README.md) - soundtrack and SFX integration references, plus source-library folders
- [design](./design/README.md) - future gameplay expansion plans and supporting design documents

Role boundary:

- `docs/` contains active project navigation and runtime references
- `supplements/` contains add-on plans, external content references, and implementation-ready supporting material
- [supplements/supplements_index.json](./supplements_index.json) is the machine-readable entry point for this layer

Machine-layer rule:

- route by normalized supplement directory IDs, not by raw media filenames;
- source media filenames are preserved for provenance and import continuity.

If a supplement becomes part of the active implementation workflow, it should be linked from:

- [../docs/code_map.md](../docs/code_map.md)
- [../docs/gameplay_current_state.md](../docs/gameplay_current_state.md)
- [../maps/repo_map.md](../maps/repo_map.md)
