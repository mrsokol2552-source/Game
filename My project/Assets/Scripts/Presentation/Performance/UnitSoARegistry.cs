/*
@file: My project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.cs
@module: presentation.performance.unitsoa
@purpose: Builds and exposes a shared structure-of-arrays snapshot for hot runtime systems such as ORCA and combat targeting jobs.
@entry: UnitSoARegistry.Update, USOA-02, USOA-03
@api: shared MonoBehaviour singleton queried by ORCA and UnitCombatJobScheduler
@deps: UnitView, UnitCombat, Unity.Collections, Unity.Mathematics
@data: active unit lists plus NativeArray snapshots for movement, combat, and ORCA inputs
@perf: hotpath snapshot builder; per-frame gather cost scales with active units
@thread: main thread only; Native data is read later by jobs
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual movement/combat verification
@config: Enabled, ExternalUpdate, OrcaCellSize, OrcaMinResponsibility
@assets: none
@notes: keep this layer focused on data extraction; gameplay decisions should stay in owning systems
*/

using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY]
// Logical block: Scripts/Presentation/Performance/UnitSoARegistry.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Builds a data-oriented snapshot (SoA) for hot systems like ORCA.
    /// </summary>
    public partial class UnitSoARegistry : MonoBehaviour
    {
        // [USOA-01]
        // Singleton state, inspector toggles, SoA buffers, and snapshot payload structs.
        public static UnitSoARegistry Instance { get; private set; }

        [Tooltip("Enable SoA snapshot generation.")]
        public bool Enabled = true;
        [Tooltip("If true, updates are driven externally.")]
        public bool ExternalUpdate = false;
        [Tooltip("Cell size used to compute ORCA spatial hash cells.")]
        public float OrcaCellSize = 1.5f;
        [Tooltip("Minimum responsibility weight for ORCA (prevents zero-weight agents).")]
        public float OrcaMinResponsibility = 0.05f;

        private readonly List<UnitView> _units = new List<UnitView>(512);
        private readonly List<UnitCombat> _combats = new List<UnitCombat>(512);
        private int _capacity;
        private int _count;
        private int _lastFrame = -1;

        private NativeArray<float2> _positions;
        private NativeArray<float2> _velocities;
        private NativeArray<float2> _preferred;
        private NativeArray<float> _maxSpeed;
        private NativeArray<byte> _hasDestination;
        private NativeArray<byte> _useOrca;
        private NativeArray<float> _responsibility;
        private NativeArray<int> _factions;
        private NativeArray<int2> _cells;
        private NativeArray<byte> _hasCombat;
        private NativeArray<byte> _isInSquad;

        public struct OrcaSnapshot
        {
            public List<UnitView> Units;
            public int Count;
            public NativeArray<float2> Positions;
            public NativeArray<float2> Velocities;
            public NativeArray<float2> Preferred;
            public NativeArray<float> MaxSpeed;
            public NativeArray<byte> HasDestination;
            public NativeArray<byte> UseOrca;
            public NativeArray<float> Responsibility;
            public NativeArray<int> Factions;
            public NativeArray<int2> Cells;
        }

        public struct CombatSnapshot
        {
            public List<UnitCombat> Units;
            public int Count;
            public NativeArray<float2> Positions;
            public NativeArray<int> Factions;
            public NativeArray<byte> HasCombat;
            public NativeArray<byte> IsInSquad;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("UnitSoARegistry");
            Instance = go.AddComponent<UnitSoARegistry>();
            DontDestroyOnLoad(go);
        }

        // [USOA-02]
        // Lifecycle setup, external tick control, and snapshot accessors.
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            DisposeArrays();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (ExternalUpdate) return;
            Tick();
        }

        public void Tick()
        {
            if (!Enabled) return;
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            BuildSnapshot();
        }

        public bool TryGetOrcaSnapshot(out OrcaSnapshot snapshot)
        {
            if (!Enabled || _count <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new OrcaSnapshot
            {
                Units = _units,
                Count = _count,
                Positions = _positions,
                Velocities = _velocities,
                Preferred = _preferred,
                MaxSpeed = _maxSpeed,
                HasDestination = _hasDestination,
                UseOrca = _useOrca,
                Responsibility = _responsibility,
                Factions = _factions,
                Cells = _cells
            };
            return true;
        }

        public bool TryGetCombatSnapshot(out CombatSnapshot snapshot)
        {
            if (!Enabled || _count <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new CombatSnapshot
            {
                Units = _combats,
                Count = _count,
                Positions = _positions,
                Factions = _factions,
                HasCombat = _hasCombat,
                IsInSquad = _isInSquad
            };
            return true;
        }
    }
}
