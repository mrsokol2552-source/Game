using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Builds a data-oriented snapshot (SoA) for hot systems like ORCA.
    /// </summary>
    public class UnitSoARegistry : MonoBehaviour
    {
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

        private void BuildSnapshot()
        {
            _units.Clear();
            _combats.Clear();
            int needed = UnitView.All.Count;
            EnsureCapacity(needed);
            if (_units.Capacity < needed) _units.Capacity = needed;
            if (_combats.Capacity < needed) _combats.Capacity = needed;

            float cellSize = OrcaCellSize;
            float minResp = Mathf.Max(0f, OrcaMinResponsibility);
            int count = 0;
            foreach (var uv in UnitView.All)
            {
                if (uv == null || !uv.isActiveAndEnabled) continue;
                _units.Add(uv);
                var combat = uv.GetComponent<UnitCombat>();
                _combats.Add(combat);

                var pos3 = uv.transform.position;
                _positions[count] = new float2(pos3.x, pos3.y);

                var m = uv.GetMovementSettings();
                _maxSpeed[count] = m.MaxSpeed;

                Vector3 lastDir3 = uv.GetLastDirection();
                float2 lastDir = new float2(lastDir3.x, lastDir3.y);
                float speed = uv.GetSpeed();
                _velocities[count] = lastDir * speed;

                if (uv.TryGetDestination(out var dest))
                {
                    _hasDestination[count] = 1;
                    float2 to = new float2(dest.x - pos3.x, dest.y - pos3.y);
                    float len = math.length(to);
                    float2 dir = len > 0.0001f ? (to / len) : new float2(1f, 0f);
                    _preferred[count] = dir * m.MaxSpeed;
                }
                else
                {
                    _hasDestination[count] = 0;
                    _preferred[count] = default;
                }

                _useOrca[count] = uv.UseOrcaVelocity ? (byte)1 : (byte)0;
                float priority = Mathf.Clamp01(uv.OrcaPriority);
                float resp = Mathf.Max(minResp, Mathf.Max(0f, 1f - priority));
                _responsibility[count] = resp;

                _factions[count] = combat != null ? (int)combat.Faction : 0;
                _hasCombat[count] = combat != null ? (byte)1 : (byte)0;
                _isInSquad[count] = (combat != null && combat.IsInSquad) ? (byte)1 : (byte)0;

                _cells[count] = ToCell(pos3, cellSize);
                count++;
            }

            _count = count;
        }

        private void EnsureCapacity(int needed)
        {
            if (needed <= _capacity) return;
            DisposeArrays();
            _capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, needed));
            _positions = new NativeArray<float2>(_capacity, Allocator.Persistent);
            _velocities = new NativeArray<float2>(_capacity, Allocator.Persistent);
            _preferred = new NativeArray<float2>(_capacity, Allocator.Persistent);
            _maxSpeed = new NativeArray<float>(_capacity, Allocator.Persistent);
            _hasDestination = new NativeArray<byte>(_capacity, Allocator.Persistent);
            _useOrca = new NativeArray<byte>(_capacity, Allocator.Persistent);
            _responsibility = new NativeArray<float>(_capacity, Allocator.Persistent);
            _factions = new NativeArray<int>(_capacity, Allocator.Persistent);
            _cells = new NativeArray<int2>(_capacity, Allocator.Persistent);
            _hasCombat = new NativeArray<byte>(_capacity, Allocator.Persistent);
            _isInSquad = new NativeArray<byte>(_capacity, Allocator.Persistent);
        }

        private void DisposeArrays()
        {
            if (_positions.IsCreated) { _positions.Dispose(); _positions = default; }
            if (_velocities.IsCreated) { _velocities.Dispose(); _velocities = default; }
            if (_preferred.IsCreated) { _preferred.Dispose(); _preferred = default; }
            if (_maxSpeed.IsCreated) { _maxSpeed.Dispose(); _maxSpeed = default; }
            if (_hasDestination.IsCreated) { _hasDestination.Dispose(); _hasDestination = default; }
            if (_useOrca.IsCreated) { _useOrca.Dispose(); _useOrca = default; }
            if (_responsibility.IsCreated) { _responsibility.Dispose(); _responsibility = default; }
            if (_factions.IsCreated) { _factions.Dispose(); _factions = default; }
            if (_cells.IsCreated) { _cells.Dispose(); _cells = default; }
            if (_hasCombat.IsCreated) { _hasCombat.Dispose(); _hasCombat = default; }
            if (_isInSquad.IsCreated) { _isInSquad.Dispose(); _isInSquad = default; }
            _capacity = 0;
            _count = 0;
        }

        private static int2 ToCell(Vector3 pos, float cellSize)
        {
            float inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            int x = Mathf.FloorToInt(pos.x * inv);
            int y = Mathf.FloorToInt(pos.y * inv);
            return new int2(x, y);
        }
    }
}
