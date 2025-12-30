using UnityEngine;

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "UnitCombatProfile", menuName = "Configs/Unit Combat Profile")]
    public class UnitCombatProfile : ScriptableObject
    {
        [Header("Combat")]
        public float AttackRange = 1.5f;
        public int AttackDamage = 10;
        public float AttackCooldown = 0.75f;

        [Header("Pathfinding")]
        public float RepathInterval = 0.25f;
        public float RepathIntervalFar = 0.7f;
        public float RepathIntervalVeryFar = 1.5f;
        public float RepathFailCooldown = 0.6f;
        public int ClusterSizeForRepath = 96;
        public int FarClusterDistance = 2;
        public int ClusterSizeForRepath2 = 256;
        public int FarClusterDistance2 = 1;
        public bool UseClusterStepping = true;
        public float RepathJitter = 0.08f;
        public bool InstantRepathOnTargetCellChange = true;
        public float StallRepathSeconds = 0.15f;

        [Header("Flow Fields")]
        public bool UseFlowFields = true;
        public float FlowFieldMinDistance = 6f;
        public float FlowFieldStepInterval = 0.12f;
        public float FlowFieldStepJitter = 0.04f;

        [Header("Targeting")]
        public float TargetRefreshInterval = 0.1f;
        public float EngageStopMultiplier = 1.2f;
        public float JobTargetTtl = 0.6f;
        public float LostTargetGraceSeconds = 0.2f;
        public float LocalThreatOverrideMultiplier = 3f;

        [Header("Performance")]
        public float CombatTickInterval = 0.04f;
        public float CombatTickJitter = 0.02f;

        [Header("Avoidance")]
        public bool DisableOrcaWhenInRange = true;
        public float OrcaDisableRangeMultiplier = 1.1f;

        [Header("Formation Offsets")]
        public bool UseFormationOffsets = true;
        public float FormationOffsetStartDistance = 8f;
        public int FormationSpacingHex = 1;
        public int FormationMaxRadiusHex = 0;

        [Header("Diagnostics")]
        public bool LogCombatResets = false;
        public int MaxCombatResetLogsPerFrame = 5;
    }
}
