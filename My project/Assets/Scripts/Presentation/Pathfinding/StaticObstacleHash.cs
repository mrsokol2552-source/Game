using Unity.Collections;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Caches blocked hex cells into a static hash for fast queries.
    /// </summary>
    public class StaticObstacleHash : MonoBehaviour
    {
        public static StaticObstacleHash Instance { get; private set; }

        [Tooltip("Enable static obstacle hashing for blocked cells.")]
        public bool Enabled = true;

        private HexPathfindingBootstrap _hex;
        private NativeHashMap<int, byte> _blocked;
        private int _capacity;
        private int _walkableVersion = -1;
        private int _width = -1;
        private int _height = -1;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("StaticObstacleHash");
            Instance = go.AddComponent<StaticObstacleHash>();
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
            if (_blocked.IsCreated) _blocked.Dispose();
            if (Instance == this) Instance = null;
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

        public bool IsBlockedWorld(Vector3 world)
        {
            if (_hex == null) return false;
            var cell = _hex.WorldToGrid(world);
            return IsBlockedCell(cell);
        }

        public bool IsBlockedCell(Vector2Int cell)
        {
            if (_hex != null)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= _hex.Width || cell.y >= _hex.Height)
                    return true;
            }
            if (!_blocked.IsCreated) return false;
            int key = Key(cell.x, cell.y);
            return _blocked.ContainsKey(key);
        }

        private void Rebuild()
        {
            if (_hex == null) return;
            var walkable = _hex.GetWalkableNative();
            int width = _hex.Width;
            int height = _hex.Height;
            int size = width * height;
            if (!walkable.IsCreated || walkable.Length != size) return;

            int needed = Mathf.Max(1, size);
            if (!_blocked.IsCreated || needed != _capacity)
            {
                if (_blocked.IsCreated) _blocked.Dispose();
                _capacity = needed;
                _blocked = new NativeHashMap<int, byte>(_capacity, Allocator.Persistent);
            }
            else
            {
                _blocked.Clear();
            }

            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * width;
                for (int col = 0; col < width; col++)
                {
                    int idx = rowOffset + col;
                    if (walkable[idx] == 0)
                    {
                        _blocked.TryAdd(Key(col, row), 1);
                    }
                }
            }

            _walkableVersion = _hex.WalkableVersion;
            _width = width;
            _height = height;
        }

        private static int Key(int col, int row) => (row << 16) ^ (col & 0xFFFF);
    }
}
