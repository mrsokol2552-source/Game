# States, Diplomacy, Research Rarity, Anomalies, and Automation Plan

Version: `1.0`  
Target stack: `Unity (C#)`  
Architectural fit: `Domain / Data / Application / AI / Presentation / Infrastructure`

This document is a working design plan for a future expansion layer:

- multiple states / factions with diplomacy
- research rarity and hidden research quality
- anomalous research tied to infected capture loops
- automation through compute resources and automation cores

Use this document together with:

- [../supplements_index.json](../supplements_index.json)
- [README.md](./README.md)
- [../../docs/gameplay_current_state.md](../../docs/gameplay_current_state.md)
- [../../docs/code_map.md](../../docs/code_map.md)
- [../../docs/deep-research-report.md](../../docs/deep-research-report.md)

Supporting source file:

- [project_addendum_states_diplomacy_research_anomalies_automation_economy.docx](./project_addendum_states_diplomacy_research_anomalies_automation_economy.docx)

## TL;DR

- The game starts with a selected `State`.
- States can progress through diplomatic stages:
  - `No Contact`
  - `Embassy`
  - `Alliance`
  - optional `Merge`
- Intel visibility depends on relation level.
- Research is reorganized into:
  - `Common`
  - `Rare`
  - `Anomalous`
- Rare research chance depends on hidden research quality, not just output quantity.
- Anomalous research requires infected samples and a full logistics loop.
- Automation is powered by server capacity and dedicated automation cores.

## 1. Domain Model

### Core enums

```csharp
public enum IntelAccess { None, UnitsAndBuildings, Full }
public enum RelationKind { None, Hostile, Embassy, Alliance }
public enum ResearchRarity { Common, Rare, Anomalous }
```

### Core entities

```csharp
public sealed record StateId(string Value);
public sealed record SectorId(int X, int Y);

public sealed class State
{
    public StateId Id;
    public string Title;
    public Dictionary<StateId, Relation> Relations = new();
    public IntelLayer Intel;
    public EconomySnapshot Economy;
    public ResearchProgress Research;
    public AutomationNetwork Automation;
}

public sealed class Relation
{
    public RelationKind Kind;
    public float DaysInAlliance;
    public float Stability;
}

public sealed class IntelLayer
{
    public IntelAccess AccessTo(StateId other);
    public KnownUnits GetKnownUnits(SectorId s, StateId other);
    public KnownBuildings GetKnownBuildings(SectorId s, StateId other);
    public KnownEconomy GetKnownEconomy(StateId other);
}

public sealed class ResearchProgress
{
    public Dictionary<string, Branch> Branches;
    public int TotalSteps;
    public float CostMultiplier;
    public float QualityScore;
}

public sealed class Branch
{
    public string Id;
    public int Completed;
    public ResearchDef Current;
}

public sealed class ResearchDef
{
    public string Id;
    public string BranchId;
    public ResearchRarity Rarity;
    public float BaseCostRP;
    public string[] PrereqIds;
    public string[] RequiresAssets;
}
```

### Automation entities

```csharp
public sealed class AutomationNetwork
{
    public int ComputeUnits;
    public int FreeComputeUnits;
    public List<AutomationCore> Cores;
}

public sealed class AutomationCore
{
    public string Id;
    public int ConsumeCU;
    public float PowerMW;
    public IAutomatable Target;
    public AutomationRecipe Recipe;
}

public interface IAutomatable
{
    Guid InstanceId { get; }
}

public enum RecipeKind
{
    CaptureLoop,
    ResearchLoop,
    ProductionLoop,
    Convoy,
    Patrol
}
```

## 2. Rules and Formulas

### Intel access levels

- `None` -> no usable external information
- `Embassy` -> visible units and buildings, but no economy or task visibility
- `Alliance` -> full visibility, including economy and queues

Visibility affects knowledge only, not direct unit control.

### Merge chance

Suggested daily merge probability while in `Alliance`:

```text
p_merge(day) =
clamp(p0 + a * ln(1 + DaysInAlliance / 7) + b * Stability - c * Strain, 0, p_max)
```

Suggested defaults:

- `p0 = 0.0015`
- `a = 0.002`
- `b = 0.005`
- `c = 0.003`
- `p_max = 0.12`

### Research cost inflation

For the `i`-th completed research step:

```text
Cost(i) = BaseCost * (1 + r)^i
```

Suggested range:

- `r = 0.10 .. 0.15`

### Rare research chance

After finishing a branch step, assign the next visible step through:

```text
pRare = clamp(pRareBase + alpha * QualityScore, 0, pRareMax)
```

Then:

- roll `Rare`
- otherwise assign `Common`
- `Anomalous` is available only if required assets and sample conditions are met

### Hidden research quality

The player should not see the exact rare-research chance.

Internally, use a derived value such as:

```text
QualityScore = Sum(QP) / (Sum(RP) + epsilon)
```

Where:

- `RP` = research throughput
- `QP` = research quality

### Science building upgrade philosophy

Two upgrade directions:

- `Quantitative` -> increases RP
- `Qualitative` -> increases QP

Economic rule:

- one upgraded building should outperform two base buildings by roughly `10% .. 30%` in its specialization axis

### Anomalies

Anomalous research requires:

- infected capture teams
- transport / delivery flow
- containment lab capacity
- sample spending per research step

Containment failure can trigger risk events.

## 3. Data Layer

### Example relations JSON

```json
{
  "states": [
    { "id": "player", "title": "Highland Union" },
    { "id": "red",    "title": "Red Commune" },
    { "id": "blue",   "title": "Northern Consortium" }
  ],
  "relations": [
    { "a": "player", "b": "red",  "kind": "Hostile", "stability": 0.2 },
    { "a": "player", "b": "blue", "kind": "None",    "stability": 0.0 }
  ]
}
```

### Example research JSON

```json
{
  "research": [
    {
      "id": "eco.extraction.1",
      "branch": "economy.extraction",
      "rarity": "Common",
      "baseCostRP": 100,
      "prereq": []
    },
    {
      "id": "eco.extraction.R1",
      "branch": "economy.extraction",
      "rarity": "Rare",
      "baseCostRP": 160,
      "prereq": ["eco.extraction.1"]
    },
    {
      "id": "anom.capture.1",
      "branch": "anomaly.capture",
      "rarity": "Anomalous",
      "baseCostRP": 220,
      "requiresAssets": ["ContainmentLab", "CaptureTeam"]
    }
  ]
}
```

### Example buildings JSON

```json
{
  "buildings": [
    { "id": "Lab_T1",         "type": "Science",     "rp": 1.0, "qp": 0.1, "power": 0.5 },
    { "id": "Lab_T2",         "type": "Science",     "rp": 2.2, "qp": 0.2, "power": 0.8, "upgradeOf": "Lab_T1" },
    { "id": "Lab_Spec",       "type": "Science",     "rp": 1.0, "qp": 1.4, "power": 1.2 },
    { "id": "ContainmentLab", "type": "Containment", "capacity": 20, "power": 1.5 },
    { "id": "ServerFarm_T1",  "type": "Server",      "computeUnits": 20, "power": 3.0 },
    { "id": "AutomationCore", "type": "Core",        "consumeCU": 10, "power": 2.5 }
  ]
}
```

### Suggested ScriptableObjects

- `StateDef.asset`
- `ResearchDef.asset`
- `BuildingDef.asset`
- `RecipeDef.asset`

### Save compatibility

Increase `SaveVersion` when introducing these systems and use a migrator for default values in older saves.

## 4. Application Layer

### Key event bus events

```text
Diplomacy/RelationsChanged(stateA, stateB, kind)
Diplomacy/MergeHappened(newStateId, absorbedStateId)
Intel/AccessChanged(stateA, stateB, newAccess)
Research/StepCompleted(stateId, branchId, defId, rarity)
Research/NextStepAssigned(stateId, branchId, defId, rarity)
Anomaly/SamplesRequired(stateId, amount)
Anomaly/Breach(stateId, sectorId, severity)
Automation/CoreAttached(coreId, targetId)
Automation/CoreStalled(coreId, reason)
Power/GridOverload(stateId, deltaMW)
```

### Simulation loop order

Suggested order:

1. diplomacy
2. intel
3. research
4. automation
5. economy / production
6. combat / anomaly
7. event flush

Off-screen sectors should tick these systems at aggregated or slower cadence.

### Save payload additions

Add sections for:

- `states[]`
- `relations[]`
- `researchProgress{}`
- `automation{}`
- `containment{}`

Persist:

- `DaysInAlliance`
- `Stability`
- `QualityScore`
- `ComputeUnits`
- core attachments
- random seed state where deterministic replay matters

## 5. AI Layer

### Diplomacy decisions

AI should evaluate:

- survival pressure
- resource needs
- mutual threats
- historical losses
- alliance stability
- merge desirability

### Intel and FoW

Each state should maintain its own intel layer.

This affects:

- target selection
- build planning
- diplomacy
- anomaly routing

### Merge behavior

On merge:

- one AI takes macro control for the merged bloc
- the player remains the leader of their own side if the player state is involved
- economy, intel, and diplomacy graphs must be consolidated carefully

## 6. Presentation Layer

### UI / UX requirements

Needed views:

- selected state identity
- diplomacy panel
- intel access state per faction
- alliance progression / stability
- research branch progression with rarity
- automation capacity and assignments
- anomaly sample stock and containment status

Important UX rule:

- rare research probability should stay hidden from the player
- the player should feel the effect of quality investment without seeing the exact formula

## 7. Balance Seed Values

Initial balance targets:

- research cost growth: `10% .. 15%`
- merge chance starts very low and ramps with time and stability
- automation cores are expensive in both power and compute
- qualitative science upgrades are more expensive than throughput upgrades

## 8. Suggested Rollout Plan

### Sprint 1: States and visibility

- add `State`
- add relation graph
- add per-state intel layer
- wire relation-driven visibility

### Sprint 2: Alliance and merge logic

- add alliance timers and stability
- implement merge probability
- implement merge resolution and persistence

### Sprint 3: Research v2

- add rarity tiers
- add hidden quality score
- add next-step reassignment logic

### Sprint 4: Anomalies and automation

- add infected sample logistics
- add containment
- add automation network and cores
- add automation recipes

## 9. Test Plan and Invariants

Core invariants:

- visibility level must never grant control
- alliance info sharing must follow relation level exactly
- merge must not duplicate state ownership or economy entries
- anomalous research must fail cleanly without required assets
- automation must pause on compute or power shortage
- save/load must preserve relation and automation state

Recommended test groups:

- diplomacy progression
- intel gating
- merge simulation
- research rarity assignment
- containment consumption
- automation stall / recovery
- off-screen aggregated simulation

## 10. Risks

Main risks:

- relation graph complexity spills into too many systems at once
- merge logic corrupts ownership and save state
- hidden quality becomes impossible to balance
- anomaly capture loop becomes too content-heavy before the base game is stable
- automation can trivialize gameplay if compute costs are too low

Mitigation:

- add systems incrementally
- keep state ownership explicit
- write save migration early
- test off-screen behavior before full rollout

## 11. Integration Hooks

Likely integration targets in the current project:

- [CompositionRoot.cs](../../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs)
- [GameStateService.cs](../../My%20project/Assets/Scripts/Application/Services/GameStateService.cs)
- [SaveSystem.cs](../../My%20project/Assets/Scripts/Infrastructure/Persistence/SaveSystem.cs)
- [EnemySquadManager.cs](../../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
- [UnitCombat.cs](../../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
- [ProceduralEnvironment.cs](../../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)

Potential new module roots:

- `Domain/States`
- `Domain/Diplomacy`
- `Domain/ResearchV2`
- `Domain/Automation`
- `Application/UseCases/Diplomacy`
- `Application/UseCases/Automation`
- `Infrastructure/Configs/States`
- `Presentation/UI/Diplomacy`

## Appendix A: Next Research Selection Pseudocode

```csharp
ResearchDef PickNextResearch(State state, Branch branch, Random rng)
{
    bool canRollAnomalous = HasRequiredAssets(state, "ContainmentLab", "CaptureTeam");

    if (canRollAnomalous && RollAnomalous(state.Research.QualityScore, rng))
        return PickAnomalous(branch, rng);

    if (RollRare(state.Research.QualityScore, rng))
        return PickRare(branch, rng);

    return PickCommon(branch, rng);
}
```

## Appendix B: Example Automation Events

```text
Automation/CoreAttached(coreId, targetId)
Automation/CoreDetached(coreId, targetId)
Automation/CorePaused(coreId, reason)
Automation/CoreResumed(coreId)
Automation/RecipeStageChanged(coreId, recipeKind, stageId)
Automation/CaptureLoopCompleted(coreId, deliveredSamples)
Automation/ResearchLoopBlocked(coreId, reason)
```
