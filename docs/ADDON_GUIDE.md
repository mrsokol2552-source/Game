# Add‑On Guide: States, Diplomacy, Research Rarities, Anomalies, Automation, Eco

Purpose: quick, structured navigator over the two add‑on docs so we can jump to relevant parts fast and keep a shared mental model when implementing features.

## Contents
- [States](#states)
- [Diplomacy](#diplomacy)
- [Research Rarities](#research-rarities)
- [Anomalies](#anomalies)
- [Automation](#automation)
- [Eco](#eco)
- [Data/Configs](#dataconfigs)
- [Application/Events](#applicationevents)

---

## States
- Purpose: world factions/states, ownership, progression hooks.
- Data: `StateDef` (id, title), ownership per tile/region.
- APIs (planned): query owner, change owner, merge states.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (States section)
- Search tokens: `states`, `ownership`, `merge`.

## Diplomacy
- Purpose: relations between states (Hostile/Neutral/Ally), treaties.
- Data: `Relation(a,b,kind,stability)`.
- Events: `Diplomacy/RelationsChanged`, `Diplomacy/MergeHappened`.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (Diplomacy)
- Search tokens: `relations`, `treaty`, `stability`.

## Research Rarities
- Purpose: research tiers (Common/Rare/Epic/Anomalous) affecting cost/progression.
- Data: `ResearchDef(id, branch, rarity, baseCostRP, prereq[])`.
- UI: badge per rarity; gating by state/progression.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (Research Rarities)
- Search tokens: `rarity`, `branch`, `baseCostRP`.

## Anomalies
- Purpose: world events affecting economy/combat; time‑limited; capture/containment.
- Data: assets like `ContainmentLab`, `CaptureTeam`.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (Anomalies)
- Search tokens: `anomaly`, `containment`, `capture`.

## Automation
- Purpose: macro loops (build/harvest/research queue) using compute units/cores.
- Data: `ServerFarm(ComputeUnits)`, `AutomationCore(ConsumeCU, PowerMW, Target, Recipe)`.
- Interfaces: `IAutomatable`, `RecipeKind`.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md and docs/ADDON_STATES_RESEARCH_AUTOMATION_PLAN.md
- Search tokens: `ComputeUnits`, `AutomationCore`, `Recipe`.

## Eco
- Purpose: environmental modifiers (pollution/renewables) that alter costs/effects.
- Effects: cost multipliers, power constraints.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (Eco)
- Search tokens: `eco`, `pollution`, `renewables`.

## Data/Configs
- JSON examples in add‑on doc for `states/relations`, `research`, `buildings`.
- SOs (planned): `StateDef.asset`, `ResearchDef.asset`, `BuildingDef.asset`, `RecipeDef.asset`.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (Data)

## Application/Events
- EventBus channels (planned): `Diplomacy/RelationsChanged`, `Diplomacy/MergeHappened`, `Intel/AccessChanged`.
- Read: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md (Application)

