# Document Roles

This file defines the role boundary of the main documentation set so the docs do not drift into each other.

Use it as the source of truth for "what belongs where".

## Active Navigation Layer

- [code_map.md](./code_map.md)
  - architecture and code ownership
  - where major systems live
  - high-level runtime flow maps
  - not the place for bug triage checklists

- [code_id_index.md](./code_id_index.md)
  - direct jump table from task -> file -> section ID
  - reading routes
  - not the place for long architecture explanation

- [debug_playbooks.md](./debug_playbooks.md)
  - symptom-first debugging routes
  - what to open first
  - not the place for broad architecture or full parameter dumps

- [runtime_switches.md](./runtime_switches.md)
  - inspector and runtime parameters that change behavior
  - tuning risks and where they matter
  - not the place for algorithm explanations

- [gameplay_current_state.md](./gameplay_current_state.md)
  - current runtime behavior and gameplay-facing state
  - what the game currently does
  - not the place for deep file routing

- [unity_mcp_tools.md](./unity_mcp_tools.md)
  - available Unity MCP operations and usage policy
  - editor-side workflow guidance
  - not the place for gameplay or code architecture

- [agent_project_workflow_rules.md](./agent_project_workflow_rules.md)
  - consolidated project working rules for Codex/agent collaboration
  - documentation, planning, testing, handoff, and current provisional target rules
  - not the place for changing current runtime status or recording new baselines

## Reference Layer

- [unity_csharp_performance_optimization_reference.md](./unity_csharp_performance_optimization_reference.md)
  - generic performance decision support
  - code/runtime optimization heuristics
  - not the place for project-specific state tracking

- [deep-research-report.md](./deep-research-report.md)
  - rationale behind the repository navigation model
  - long-form principles for agent-friendly repository design
  - not the place for daily operational notes

- [navigation_optimization_ideas.md](./navigation_optimization_ideas.md)
  - backlog of navigation/movement system ideas
  - candidate improvements, not authoritative current state

- [translated_tiles_grid_theory.md](./translated_tiles_grid_theory.md)
  - external/reference theory for tile-grid handling
  - not authoritative runtime behavior

## Machine-Readable Layer

- [code_index.json](./code_index.json)
  - machine-readable lookup for systems and routes

- [document_roles_index.json](./document_roles_index.json)
  - machine-readable manifest of governed documentation, role IDs, ownership tags, and required links

- [../maps/repo_map.json](../maps/repo_map.json)
  - machine-readable top-level repository map

- [../supplements/supplements_index.json](../supplements/supplements_index.json)
  - machine-readable supplement routing index

- [machine_layer_audit.md](./machine_layer_audit.md)
  - machine-layer ownership, invariants, and validation status

- [runtime_config_export.json](./runtime_config_export.json)
  - generated scene/prefab runtime snapshot for inspector-visible serialized values
  - machine-readable view of the active runtime config surface

## Enforcement Tooling

- [scripts/audit_machine_layer.py](../scripts/audit_machine_layer.py)
  - validates machine-readable indexes, routing consistency, file-header sync, and markdown-link integrity

- [scripts/audit_docs_architecture.py](../scripts/audit_docs_architecture.py)
  - validates document ownership boundaries, required cross-links, and coverage of all governed markdown docs

- [scripts/run_all_repo_audits.py](../scripts/run_all_repo_audits.py)
  - runs the strict runtime export refresh, both audits, and the Python regression suite as one pipeline

## Top-Level Routing Layer

- [../maps/repo_map.md](../maps/repo_map.md)
  - fastest top-level route into the repository
  - where to start before opening large docs

## Supplement Layer

- [../supplements/README.md](../supplements/README.md)
  - add-on design material and external content references
  - not part of the main runtime/navigation layer unless explicitly promoted

## Practical Rule

When adding new documentation:

1. Decide whether it is navigation, runtime state, tuning, reference, machine-readable index, or supplement.
2. Put it in the matching layer.
3. Link to the owning document instead of copying the same explanation twice.

If two documents explain the same thing in the same level of detail, one of them should be simplified, linked, or removed.

## Mandatory Anti-Duplication Rule

These rules are mandatory:

1. One document must have one role.
2. Do not create new broad "general" docs that overlap existing ownership.
3. If the same explanation appears a second time, convert one copy into a link, an index entry, a generated snapshot, or a script output.
4. Prefer extending the existing navigation/machine layer over inventing a parallel document set.
