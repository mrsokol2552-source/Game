# Machine Layer Audit

This document treats the machine-readable navigation layer as its own subsystem.

Use it when the task is about:

- validating repository indexes;
- checking whether machine-readable routes still match the real file tree;
- auditing path conventions and ownership boundaries;
- deciding where a new machine-readable artifact belongs.

## Scope

The current machine layer consists of:

- [code_index.json](./code_index.json)
- [document_roles_index.json](./document_roles_index.json)
- [repo_map.json](../maps/repo_map.json)
- [supplements_index.json](../supplements/supplements_index.json)
- [runtime_config_export.json](./runtime_config_export.json)
- [runtime_config_manifest.json](../scripts/runtime_config_manifest.json)

These files are complementary:

- [code_index.json](./code_index.json) routes by system, task, and `CODE-ID`;
- [document_roles_index.json](./document_roles_index.json) routes by documentation ownership, role IDs, and required cross-links;
- [repo_map.json](../maps/repo_map.json) routes by top-level repository structure;
- [supplements_index.json](../supplements/supplements_index.json) routes by supplement domain and normalized supplement IDs;
- [runtime_config_export.json](./runtime_config_export.json) exposes the currently tracked scene/prefab serialized runtime values;
- [runtime_config_manifest.json](../scripts/runtime_config_manifest.json) defines which scenes, game objects, prefabs, and component types feed that export.

Canonical export policy:

- [runtime_config_export.json](./runtime_config_export.json) is the canonical strict snapshot used by the machine layer.
- [runtime_config_export_full.json](./runtime_config_export_full.json) is an optional deep-inspection artifact generated only when explicitly requested.

## Role Boundary

The machine layer does not replace human-facing docs.

It must stay aligned with:

- [document_roles.md](./document_roles.md)
- [code_map.md](./code_map.md)
- [code_id_index.md](./code_id_index.md)
- [repo_map.md](../maps/repo_map.md)
- [supplements/README.md](../supplements/README.md)

Human docs explain meaning and workflow.  
Machine docs provide stable routing and compact lookup.

## Audit Table

| Artifact | Owner Layer | Primary Scope | Canonical Human Pair | Validation Target | Status |
| --- | --- | --- | --- | --- | --- |
| [code_index.json](./code_index.json) | `docs/` | system/task routing, hot IDs, reading routes | [code_id_index.md](./code_id_index.md) | JSON validity, section ID alignment, route relevance | Pass |
| [document_roles_index.json](./document_roles_index.json) | `docs/` | governed-doc manifest, role IDs, required cross-links | [document_roles.md](./document_roles.md) | JSON validity, required link coverage, role/ownership uniqueness | Pass |
| [repo_map.json](../maps/repo_map.json) | `maps/` | top-level repository routing | [repo_map.md](../maps/repo_map.md) | JSON validity, root/doc coverage, path existence | Pass |
| [supplements_index.json](../supplements/supplements_index.json) | `supplements/` | supplement-layer routing by normalized IDs | [supplements/README.md](../supplements/README.md) | JSON validity, supplement coverage, naming policy consistency | Pass |
| [runtime_config_export.json](./runtime_config_export.json) | `docs/` | generated runtime snapshot of tracked scenes/prefabs | [runtime_switches.md](./runtime_switches.md) | JSON validity, export shape, manifest path consistency | Pass |
| [runtime_config_manifest.json](../scripts/runtime_config_manifest.json) | `scripts/` | declarative export target list for runtime config capture | [runtime_config_export.json](./runtime_config_export.json) | JSON validity, target list presence, output path consistency | Pass |

## Current Invariants

These rules must remain true:

1. All machine-layer paths are repo-relative.
2. All JSON files parse successfully.
3. Human-facing markdown links in `docs/`, `maps/`, and `supplements/` resolve to real files.
4. Supplement machine routing uses normalized directory IDs, not raw media filenames.
5. Documentation must not point to the obsolete legacy Cyrillic supplements path.
6. Documentation must not point to the obsolete editor path that used to live under `Assets/Scripts`.
7. `docs/`, `maps/`, and `supplements/` markdown files stay English-first and ASCII-path-friendly.
8. [runtime_config_export.json](./runtime_config_export.json) must be regenerated after tracked scene/prefab inspector changes.

## What Gets Checked During an Audit

### Structural checks

- JSON parse validity
- dead path detection
- markdown anchor resolution
- obsolete path detection
- top-level root coverage
- supplement root coverage

### Semantic checks

- [code_index.json](./code_index.json) still matches existing `CODE-ID` and section-ID conventions
- [repo_map.json](../maps/repo_map.json) still reflects the actual navigation roots
- [supplements_index.json](../supplements/supplements_index.json) still matches the current supplement domains and normalized naming policy
- [runtime_config_export.json](./runtime_config_export.json) still matches the current export manifest
- duplicate `CODE-ID` markers are not present in the code layer
- hot files listed in machine indexes still match real `@file` headers and section markers
- section-ID references used by docs still resolve to real code markers

### Drift checks

- a new human-facing doc was added but not linked from the routing layer
- a new supplement domain was added but not registered in `supplements_index.json`
- a hot code path moved but the machine layer still points to the old location

## Current Audit Result

Audit baseline date:

- `2026-03-16`

Validated in this pass:

- [code_index.json](./code_index.json) parses
- [repo_map.json](../maps/repo_map.json) parses
- [supplements_index.json](../supplements/supplements_index.json) parses
- [runtime_config_manifest.json](../scripts/runtime_config_manifest.json) parses
- [runtime_config_export.json](./runtime_config_export.json) parses
- [document_roles_index.json](./document_roles_index.json) parses
- markdown links in `docs/`, `maps/`, `supplements/` resolve
- markdown anchors in the same layer resolve
- obsolete legacy Cyrillic supplements path references not found
- obsolete script-editor path references not found

Known intentional exception:

- raw source audio file names under `supplements/audio/sound_effects` and `supplements/audio/soundtrack_variants` are preserved as imported reference assets;
- machine routing must use normalized directory IDs instead.

## Maintenance Rule

Primary audit command:

```bash
python ./scripts/audit_machine_layer.py
```

Documentation-governance audit command:

```bash
python ./scripts/audit_docs_architecture.py
```

Regression-test command:

```bash
python -m unittest discover ./scripts/tests -v
```

One-command full pipeline:

```bash
python ./scripts/run_all_repo_audits.py --with-full-export
```

When adding a new machine-readable file:

1. decide whether it belongs to `docs/`, `maps/`, or `supplements/`;
2. pair it with one human-facing routing document;
3. register it in the appropriate parent index;
4. update this audit file if it becomes part of the stable machine layer.

When scene/prefab serialized config changes:

1. run [scripts/export_runtime_config.py](../scripts/export_runtime_config.py);
2. regenerate [runtime_config_export.json](./runtime_config_export.json);
3. run [scripts/audit_machine_layer.py](../scripts/audit_machine_layer.py).

Agent rule:

- after any change to [README.md](../README.md), `docs/`, `maps/`, `supplements/`, [runtime_config_manifest.json](../scripts/runtime_config_manifest.json), or any machine-readable routing `.json`, run [scripts/audit_machine_layer.py](../scripts/audit_machine_layer.py), [scripts/audit_docs_architecture.py](../scripts/audit_docs_architecture.py), and `python -m unittest discover ./scripts/tests -v` before closing the task.

## Practical Entry Rule

Start here only when the task is specifically about:

- machine-layer consistency;
- index drift;
- repo navigation infrastructure;
- formal documentation hygiene of routing artifacts.

For normal gameplay/runtime work, start from:

- [debug_playbooks.md](./debug_playbooks.md)
- [code_id_index.md](./code_id_index.md)
- [runtime_switches.md](./runtime_switches.md)
