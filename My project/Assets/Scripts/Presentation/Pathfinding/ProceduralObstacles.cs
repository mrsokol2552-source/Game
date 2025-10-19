using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    public class ProceduralObstacles : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Absolute number of rocks (ignored if CoveragePercent > 0)")]
        public int Count = 50;
        [Tooltip("Minimum hex distance between rocks (in hex cells). Set 0 to disable spacing.")]
        public int MinHexDistance = 0;
        [Range(0f,1f)]
        [Tooltip("Percent of hex cells to fill with rocks (0..1). If > 0, overrides Count.")]
        public float CoveragePercent = 0.1f;
        public bool UseRandomSeed = true;
        public int Seed = 12345;

        [Header("Visuals/Physics")]
        public Sprite RockSprite;
        public Sprite[] RockSprites;
        public bool UseCircleCollider = true;
        public float ColliderRadiusScale = 0.45f; // fraction of hex size
        public string ObstacleLayerName = "Obstacles";

        private HexPathfindingBootstrap _hex;

        private void Awake()
        {
            _hex = FindObjectOfType<HexPathfindingBootstrap>();
            if (_hex == null) return;

            var rng = UseRandomSeed ? new System.Random() : new System.Random(Seed);
            int totalHex = _hex.Width * _hex.Height;
            int targetCount = CoveragePercent > 0f ? Mathf.RoundToInt(totalHex * Mathf.Clamp01(CoveragePercent)) : Count;
            var picked = new List<Vector2Int>(targetCount);

            int attempts = 0;
            // Resolve target obstacle layer: prefer explicit name, then hex mask, else Default
            int layer = LayerMask.NameToLayer(ObstacleLayerName);
            if (layer < 0)
                layer = FirstLayerFromMask(_hex.ObstacleMask);
            if (layer < 0) layer = 0; // Default
            // Ensure hex mask includes this layer so BakeFromPhysics picks up colliders
            var m = _hex.ObstacleMask; m.value |= (1 << layer); _hex.ObstacleMask = m;

            // Try to auto-assign a sprite in editor if not set
            if (RockSprite == null)
            {
                TryAutoAssignSpriteEditor();
            }

            while (picked.Count < targetCount && attempts < targetCount * 50)
            {
                attempts++;
                int q = rng.Next(0, _hex.Width);
                int r = rng.Next(0, _hex.Height);
                var cell = new Vector2Int(q, r);
                bool ok = true;
                if (MinHexDistance > 0)
                {
                    for (int i = 0; i < picked.Count; i++)
                    {
                        if (HexDistance(cell, picked[i]) < MinHexDistance)
                        {
                            ok = false; break;
                        }
                    }
                }
                if (!ok) continue;
                picked.Add(cell);

                // Spawn rock instance
                var pos = _hex.GridToWorld(q, r);
                var go = new GameObject($"Rock ({q},{r})");
                go.layer = layer;
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PickSprite(rng); // may be null; collider still blocks path
                if (UseCircleCollider)
                {
                    var cc = go.AddComponent<CircleCollider2D>();
                    cc.radius = (_hex.HexSize * ColliderRadiusScale);
                    cc.isTrigger = false;
                }
                else
                {
                    var pc = go.AddComponent<PolygonCollider2D>();
                    pc.isTrigger = false;
                }
            }

            // Re-bake walkable map from physics
            _hex.BakeFromPhysics();
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            // Convert odd-r offset to axial and compute cube distance
            Axial aa = OddRToAxial(a.x, a.y);
            Axial bb = OddRToAxial(b.x, b.y);
            int dx = aa.q - bb.q; if (dx < 0) dx = -dx;
            int dz = aa.r - bb.r; if (dz < 0) dz = -dz;
            int dy = -(aa.q + aa.r) - (-(bb.q + bb.r));
            if (dy < 0) dy = -dy;
            return (dx + dy + dz) / 2;
        }

        private struct Axial { public int q; public int r; }
        private static Axial OddRToAxial(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return new Axial { q = q, r = r };
        }

        private static int FirstLayerFromMask(LayerMask mask)
        {
            int m = mask.value;
            if (m == 0) return -1;
            for (int i = 0; i < 32; i++)
            {
                if (((m >> i) & 1) != 0) return i;
            }
            return -1;
        }

#if UNITY_EDITOR
        private void TryAutoAssignSpriteEditor()
        {
            try
            {
                // Attempt to find any sprite under common rocks folder
                var paths = new List<string>
                {
                    "Assets/Game/Sprites/craftpix-724167-tds-modern-tilesets-environment/PNG/Rocks",
                    "Assets/Game/Sprites",
                };
                foreach (var p in paths)
                {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { p });
                    foreach (var g in guids)
                    {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                        var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (s != null)
                        {
                            RockSprite = s;
                            return;
                        }
                    }
                }
            }
            catch { /* editor only; ignore in player */ }
        }
#endif

        private Sprite PickSprite(System.Random rng)
        {
            if (RockSprites != null && RockSprites.Length > 0)
            {
                return RockSprites[rng.Next(RockSprites.Length)];
            }
            return RockSprite;
        }
    }
}


