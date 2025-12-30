using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Pre-bakes cover slots around static obstacles and keeps a spatial hash for fast queries.
    /// </summary>
    public class CoverSlotHash : MonoBehaviour
    {
        public static CoverSlotHash Instance { get; private set; }

        [Tooltip("Enable cover slot baking and hashing.")]
        public bool Enabled = true;
        [Tooltip("World cell size for cover spatial hash.")]
        public float BucketCellSize = 1.5f;
        [Tooltip("How many bucket rings to scan for nearest cover.")]
        public int SearchRings = 2;

        private HexPathfindingBootstrap _hex;
        private int _walkableVersion = -1;
        private int _width = -1;
        private int _height = -1;

        private readonly Dictionary<int, List<CoverSlot>> _buckets = new Dictionary<int, List<CoverSlot>>(256);
        private readonly Stack<List<CoverSlot>> _listPool = new Stack<List<CoverSlot>>();

        private static readonly Vector2Int[] EvenOffsets =
        {
            new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, -1),
            new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
        };

        private static readonly Vector2Int[] OddOffsets =
        {
            new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };

        public struct CoverSlot
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("CoverSlotHash");
            Instance = go.AddComponent<CoverSlotHash>();
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
            if (Instance == this) Instance = null;
            ClearBuckets();
        }

        private void Update()
        {
            if (!Enabled) return;
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_hex == null) return;
            if (_walkableVersion != _hex.WalkableVersion || _width != _hex.Width || _height != _hex.Height)
            {
                Rebuild();
            }
        }

        public bool TryGetNearestCover(Vector3 world, float maxDistance, out CoverSlot slot)
        {
            slot = default;
            if (_buckets.Count == 0) return false;
            int rings = Mathf.Max(0, SearchRings);
            var cell = WorldToCell(world, BucketCellSize);
            float maxDistSq = maxDistance > 0f ? maxDistance * maxDistance : float.MaxValue;
            float best = float.MaxValue;
            CoverSlot bestSlot = default;
            bool found = false;

            for (int dy = -rings; dy <= rings; dy++)
            {
                for (int dx = -rings; dx <= rings; dx++)
                {
                    int key = Key(cell.x + dx, cell.y + dy);
                    if (!_buckets.TryGetValue(key, out var list) || list == null || list.Count == 0)
                        continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var c = list[i];
                        float d2 = (c.Position - world).sqrMagnitude;
                        if (d2 > maxDistSq) continue;
                        if (d2 < best)
                        {
                            best = d2;
                            bestSlot = c;
                            found = true;
                        }
                    }
                }
            }

            slot = bestSlot;
            return found;
        }

        private void Rebuild()
        {
            if (_hex == null) return;
            var walkable = _hex.GetWalkableNative();
            int width = _hex.Width;
            int height = _hex.Height;
            int size = width * height;
            if (!walkable.IsCreated || walkable.Length != size) return;

            ClearBuckets();

            for (int row = 0; row < height; row++)
            {
                var offs = (row & 1) == 0 ? EvenOffsets : OddOffsets;
                int rowOffset = row * width;
                for (int col = 0; col < width; col++)
                {
                    int idx = rowOffset + col;
                    if (walkable[idx] != 0) continue;
                    var obstacleWorld = _hex.GridToWorld(col, row);
                    for (int i = 0; i < 6; i++)
                    {
                        int nc = col + offs[i].x;
                        int nr = row + offs[i].y;
                        if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                        int nIdx = nr * width + nc;
                        if (walkable[nIdx] == 0) continue;
                        var slotWorld = _hex.GridToWorld(nc, nr);
                        var normal = (slotWorld - obstacleWorld);
                        if (normal.sqrMagnitude > 0.0001f)
                            normal.Normalize();
                        AddSlot(slotWorld, normal);
                    }
                }
            }

            _walkableVersion = _hex.WalkableVersion;
            _width = width;
            _height = height;
        }

        private void AddSlot(Vector3 pos, Vector3 normal)
        {
            var cell = WorldToCell(pos, BucketCellSize);
            int key = Key(cell.x, cell.y);
            if (!_buckets.TryGetValue(key, out var list) || list == null)
            {
                list = _listPool.Count > 0 ? _listPool.Pop() : new List<CoverSlot>(4);
                if (list == null) list = new List<CoverSlot>(4);
                _buckets[key] = list;
            }
            list.Add(new CoverSlot { Position = pos, Normal = normal });
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
    }
}
