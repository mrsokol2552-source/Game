using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Game.Presentation.View;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Maintains a spatial hash of unit occupancy per frame for fast queries.
    /// Rebuilt each Update; data kept in NativeHashMap for job-friendly reads and a managed bucket map for targeting.
    /// </summary>
    public class OccupancyHash : MonoBehaviour
    {
        public static OccupancyHash Instance { get; private set; }

        [Tooltip("World cell size for occupancy hash.")]
        public float CellSize = 1.5f;
        [Header("Targeting")]
        [Tooltip("How many bucket rings to scan when searching nearest enemy.")]
        public int TargetRings = 1;
        [Tooltip("Optional max squared distance for nearest search (0 = no limit).")]
        public float TargetMaxDistanceSq = 0f;

        private NativeHashMap<int, int> _map;
        private int _capacity = 256;

        // Managed bucket map for nearest-enemy queries (per-frame, cleared and reused).
        private readonly Dictionary<int, List<UnitCombat>> _buckets = new Dictionary<int, List<UnitCombat>>(256);
        private readonly Stack<List<UnitCombat>> _listPool = new Stack<List<UnitCombat>>();

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("OccupancyHash");
            Instance = go.AddComponent<OccupancyHash>();
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
            _map = new NativeHashMap<int, int>(_capacity, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            if (_map.IsCreated) _map.Dispose();
            if (Instance == this) Instance = null;
            // No need to dispose managed lists; they are pooled and cleared each frame.
        }

        private void Update()
        {
            Rebuild();
        }

        public bool IsOccupied(Vector3 world, UnitView self = null, bool enemiesOnly = false)
        {
            if (!_map.IsCreated) return false;
            int hash = Hash(world, CellSize);
            if (_map.TryGetValue(hash, out var count))
            {
                return count > 0;
            }
            return false;
        }

        /// <summary>
        /// Find nearest enemy using the managed bucket map. Returns false if none found.
        /// </summary>
        public bool TryGetNearestEnemy(Vector3 world, Game.Domain.Units.Faction selfFaction, out UnitCombat enemy)
        {
            enemy = null;
            if (_buckets.Count == 0) return false;
            int rings = Mathf.Max(0, TargetRings);
            var cell = WorldToCell(world, CellSize);

            float best = float.MaxValue;
            UnitCombat bestUc = null;
            for (int dy = -rings; dy <= rings; dy++)
            {
                for (int dx = -rings; dx <= rings; dx++)
                {
                    int key = Key(cell.x + dx, cell.y + dy);
                    if (!_buckets.TryGetValue(key, out var list) || list == null || list.Count == 0)
                        continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var uc = list[i];
                        if (uc == null || !uc.isActiveAndEnabled) continue;
                        if (uc.Faction == selfFaction) continue;
                        float d2 = (uc.transform.position - world).sqrMagnitude;
                        if (TargetMaxDistanceSq > 0f && d2 > TargetMaxDistanceSq) continue;
                        if (d2 < best)
                        {
                            best = d2;
                            bestUc = uc;
                        }
                    }
                }
            }

            enemy = bestUc;
            return bestUc != null;
        }

        private void Rebuild()
        {
            if (!_map.IsCreated) return;
            _map.Clear();
            ClearBuckets();

            var all = Game.Presentation.View.UnitCombat.All;
            int needed = Mathf.Max(_capacity, all.Count * 2 + 32);
            if (needed > _capacity)
            {
                _map.Dispose();
                _capacity = Mathf.NextPowerOfTwo(needed);
                _map = new NativeHashMap<int, int>(_capacity, Allocator.Persistent);
            }

            foreach (var uc in all)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                var pos = uc.transform.position;
                // Native count map for occupancy queries
                int h = Hash(pos, CellSize);
                if (_map.TryGetValue(h, out var count))
                    _map[h] = count + 1;
                else
                    _map.TryAdd(h, 1);

                // Managed buckets for targeting
                var cell = WorldToCell(pos, CellSize);
                int key = Key(cell.x, cell.y);
                if (!_buckets.TryGetValue(key, out var list) || list == null)
                {
                    list = _listPool.Count > 0 ? _listPool.Pop() : new List<UnitCombat>(4);
                    if (list == null) list = new List<UnitCombat>(4);
                    _buckets[key] = list;
                }
                list.Add(uc);
            }
        }

        private void ClearBuckets()
        {
            foreach (var kv in _buckets)
            {
                if (kv.Value == null) continue;
                kv.Value.Clear();
                _listPool.Push(kv.Value);
            }
            _buckets.Clear();
        }

        private static Vector2Int WorldToCell(Vector3 world, float cellSize)
        {
            float inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            int x = Mathf.FloorToInt(world.x * inv);
            int y = Mathf.FloorToInt(world.y * inv);
            return new Vector2Int(x, y);
        }

        private static int Key(int col, int row) => (row << 16) ^ (col & 0xFFFF);

        private static int Hash(Vector3 world, float cellSize)
        {
            float inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            int x = Mathf.FloorToInt(world.x * inv);
            int y = Mathf.FloorToInt(world.y * inv);
            unchecked
            {
                int h = 73856093 ^ x;
                h = (h * 19349663) ^ y;
                return h;
            }
        }
    }
}
