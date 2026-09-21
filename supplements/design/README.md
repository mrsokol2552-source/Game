# Design Supplements

This directory contains future-facing gameplay expansion material.

Main documents:

- [../supplements_index.json](../supplements_index.json)
- [states_diplomacy_research_automation_plan.md](./states_diplomacy_research_automation_plan.md)
- [project_addendum_states_diplomacy_research_anomalies_automation_economy.docx](./project_addendum_states_diplomacy_research_anomalies_automation_economy.docx)

Recommended use:

- use this directory for major feature concepts that are not yet fully integrated into runtime code;
- use [../supplements_index.json](../supplements_index.json) for machine routing into this layer;
- once a concept becomes active implementation work, move the implementation-facing decisions into:
  - [../../docs/gameplay_current_state.md](../../docs/gameplay_current_state.md)
  - [../../docs/code_map.md](../../docs/code_map.md)
  - [../../docs/debug_playbooks.md](../../docs/debug_playbooks.md) or [../../docs/runtime_switches.md](../../docs/runtime_switches.md) where appropriate
