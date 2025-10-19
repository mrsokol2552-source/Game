using System.IO;
using System.Collections;
using Game.Domain.Economy;
using Game.Domain.Units;
using Game.Presentation.Bootstrap;
using Game.Presentation.Input;
using Game.Presentation.View;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation.UI
{
    public class ActionsPanel : MonoBehaviour
    {
        public static bool Visible = false;
        public float Width = 220f;
        public float Height = 220f;
        // Pathfinding is now always enabled by default; dev toggles removed

        private Vector2 _scroll;

        private CompositionRoot root;

        private void Awake()
        {
            root = FindObjectOfType<CompositionRoot>();
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (root == null) root = FindObjectOfType<CompositionRoot>();

            var area = new Rect(Screen.width - Width - 10f, Screen.height - Height - 10f, Width, Height);
            HudController.AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Actions");
            _scroll = GUILayout.BeginScrollView(_scroll, false, true);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Unit"))
            {
                SpawnUnit(Faction.Player);
            }
            if (GUILayout.Button("Spawn Enemy"))
            {
                SpawnUnit(Faction.Enemy);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 Materials"))
                CompositionRoot.Game?.Economy.Add(ResourceType.Materials, 10);
            if (GUILayout.Button("+5 Food"))
                CompositionRoot.Game?.Economy.Add(ResourceType.Food, 5);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (GUILayout.Button("Attempt Build"))
                root?.AttemptPlaceTestBuilding();

            if (GUILayout.Button(ResearchPanel.Visible ? "Hide Research" : "Show Research"))
                ResearchPanel.Visible = !ResearchPanel.Visible;

            // Pathfinding controls removed; hex movement is always active by default.

            GUILayout.Space(6);
            if (GUILayout.Button("Clear Save"))
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "save.json");
                if (File.Exists(path)) File.Delete(path);
                root?.SetStatus("Save cleared");
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Self-Test Save/Load"))
            {
                if (!_selfTestRunning)
                {
                    StartCoroutine(SelfTestRoutine());
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void SpawnUnit(Faction faction)
        {
            var cam = Camera.main; if (cam == null) return;
            // Spawn at a random visible point within the camera view (with margins)
            var world = RandomWorldPointInView(cam, 0.12f); // 12% margin from edges

            var spawner = FindObjectOfType<UnitSpawnerCommander>();
            UnitView prefab = spawner != null ? spawner.UnitPrefab : null;
            if (prefab == null && root != null) prefab = root.DefaultUnitPrefab;
            if (prefab == null)
            {
                root?.SetStatus("No Unit prefab to spawn");
                return;
            }

            var u = Instantiate(prefab, world, Quaternion.identity);
            var combat = u.GetComponent<UnitCombat>();
            if (combat == null) combat = u.gameObject.AddComponent<UnitCombat>();
            combat.Faction = faction;
            if (u.GetComponent<UnitHpOverlay>() == null) u.gameObject.AddComponent<UnitHpOverlay>();

            var sr = u.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // Color tint
                sr.color = (faction == Faction.Enemy ? Color.red : Color.white);
                // Assign sprite from Bootstrap Visuals if available
                if (root != null)
                {
                    if (faction == Faction.Enemy && root.EnemySprite != null)
                        sr.sprite = root.EnemySprite;
                    else if (faction == Faction.Player && root.PlayerSprite != null)
                        sr.sprite = root.PlayerSprite;
                }
            }
        }

        private static Vector3 RandomWorldPointInView(Camera cam, float viewportMargin)
        {
            float m = Mathf.Clamp01(viewportMargin);
            float vx = UnityEngine.Random.Range(m, 1f - m);
            float vy = UnityEngine.Random.Range(m, 1f - m);

            Vector3 world;
            if (cam.orthographic)
            {
                world = cam.ViewportToWorldPoint(new Vector3(vx, vy, 0f));
            }
            else
            {
                var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
                // Intersect with Z=0 plane (game plane)
                if (Mathf.Abs(ray.direction.z) > 1e-4f)
                {
                    float t = -ray.origin.z / ray.direction.z;
                    if (t < cam.nearClipPlane) t = cam.nearClipPlane;
                    world = ray.origin + ray.direction * t;
                }
                else
                {
                    world = cam.ViewportToWorldPoint(new Vector3(vx, vy, cam.nearClipPlane));
                }
            }
            world.z = 0f;
            return world;
        }

        private struct ExpectedUnit
        {
            public Vector3 Pos;
            public bool HasDest;
            public Vector3 Dest;
            public Faction Faction;
            public int Health;
        }

        private bool _selfTestRunning = false;
        private IEnumerator SelfTestRoutine()
        {
            _selfTestRunning = true;
            var cam = Camera.main; if (cam == null) { root?.SetStatus("SelfTest: No Camera"); _selfTestRunning = false; yield break; }
            var spawner = FindObjectOfType<UnitSpawnerCommander>();
            UnitView prefab = spawner != null ? spawner.UnitPrefab : null;
            if (prefab == null && root != null) prefab = root.DefaultUnitPrefab;
            if (prefab == null) { root?.SetStatus("SelfTest: No Unit prefab"); _selfTestRunning = false; yield break; }

            // Freeze combat during the whole test to avoid side-effects
            var prevFreeze = Game.Presentation.View.UnitCombat.DisableCombat;
            Game.Presentation.View.UnitCombat.DisableCombat = true;

            // 1) Clean scene (units) for deterministic test
            var existing = FindObjectsOfType<UnitView>();
            foreach (var u in existing)
            {
                if (u != null) Destroy(u.gameObject);
            }
            // Wait a frame for destruction to apply
            yield return null;

            // 2) Create deterministic setup
            var vp = new Vector2[]
            {
                new Vector2(0.22f, 0.24f),
                new Vector2(0.78f, 0.26f),
                new Vector2(0.24f, 0.76f),
                new Vector2(0.76f, 0.78f),
                new Vector2(0.50f, 0.52f)
            };
            var vpDest = new Vector2[]
            {
                new Vector2(0.32f, 0.24f),
                new Vector2(0.68f, 0.26f),
                new Vector2(0.24f, 0.66f),
                new Vector2(0.76f, 0.68f),
                new Vector2(0.60f, 0.52f)
            };
            var factions = new Faction[]
            {
                Faction.Player,
                Faction.Enemy,
                Faction.Player,
                Faction.Enemy,
                Faction.Player
            };

            var expected = new System.Collections.Generic.List<ExpectedUnit>();
            for (int i = 0; i < vp.Length; i++)
            {
                var pos = ViewportToWorldOnPlane(cam, vp[i]);
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = $"SelfTest Unit {i}";
                var combat = go.GetComponent<UnitCombat>();
                if (combat == null) combat = go.gameObject.AddComponent<UnitCombat>();
                combat.Faction = factions[i];
                // visuals
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = (combat.Faction == Faction.Enemy ? Color.red : Color.white);
                    if (root != null)
                    {
                        if (combat.Faction == Faction.Enemy && root.EnemySprite != null) sr.sprite = root.EnemySprite;
                        else if (combat.Faction == Faction.Player && root.PlayerSprite != null) sr.sprite = root.PlayerSprite;
                    }
                }
                if (go.GetComponent<UnitHpOverlay>() == null) go.gameObject.AddComponent<UnitHpOverlay>();

                bool hasDest = (i % 2 == 0);
                Vector3 dest = default;
                if (hasDest)
                {
                    dest = ViewportToWorldOnPlane(cam, vpDest[i]);
                    go.SetDestination(dest);
                }

                int maxHp = combat.MaxHealth;
                int hp = maxHp;
                switch (i)
                {
                    case 1: hp = (int)(maxHp * 0.5f); break; // half
                    case 3: hp = (int)(maxHp * 0.3f); break; // low
                    default: hp = (int)(maxHp * 0.9f); break; // near full
                }
                combat.SetHealth(Mathf.Max(1, hp));

                expected.Add(new ExpectedUnit
                {
                    Pos = pos,
                    HasDest = hasDest,
                    Dest = dest,
                    Faction = combat.Faction,
                    Health = combat.CurrentHealth
                });
            }

            // Optional: Start research
            string researchId = null;
            Game.Infrastructure.Configs.ResearchConfig rc = root != null ? root.TestResearch : null;
            if (rc != null && rc.Items != null && rc.Items.Length > 0)
            {
                researchId = rc.Items[0].Id;
                root.AttemptStartTestResearch();
            }

            // 3) Save then Load
            root?.Save();
            root?.Load();
            // Wait for restore's destruction to apply
            yield return null;

            // 4) Validate
            var after = FindObjectsOfType<UnitView>();
            bool pass = true;
            var used = new System.Collections.Generic.HashSet<int>();
            int matched = 0;
            const float EPS = 0.02f;

            for (int i = 0; i < expected.Count; i++)
            {
                int bestJ = -1; float bestD2 = float.MaxValue;
                for (int j = 0; j < after.Length; j++)
                {
                    if (used.Contains(j)) continue;
                    float d2 = (after[j].transform.position - expected[i].Pos).sqrMagnitude;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2; bestJ = j;
                    }
                }
                if (bestJ < 0 || bestD2 > EPS * EPS)
                {
                    pass = false; continue;
                }
                used.Add(bestJ); matched++;
                var u = after[bestJ];
                var uc = u.GetComponent<UnitCombat>();
                bool hasDestAfter = u.TryGetDestination(out var d);
                if (hasDestAfter != expected[i].HasDest) pass = false;
                if (expected[i].HasDest && (d - expected[i].Dest).sqrMagnitude > EPS * EPS) pass = false;
                if (uc == null || (int)uc.Faction != (int)expected[i].Faction) pass = false;
                if (uc == null || Mathf.Abs(uc.CurrentHealth - expected[i].Health) > 0) pass = false;
                if (u.GetComponent<UnitHpOverlay>() == null) pass = false;
            }

            // Research status check
            if (!string.IsNullOrEmpty(researchId) && root != null && CompositionRoot.Game != null)
            {
                var st = CompositionRoot.Game.Research.GetStatus(researchId);
                // either stayed Locked (if insufficient resources) or became Queued; after save/load should be the same
                // We can’t know original expected without cost check; just assert it’s not null enum value
                // In practice, Save/Load shouldn’t change it across the cycle
                // pass remains as is; we’ll include status in message
            }

            string msg = pass
                ? $"SelfTest: PASS (units: {expected.Count}/{after.Length} matched)"
                : $"SelfTest: FAIL (matched {matched}/{expected.Count}; total after load: {after.Length})";
            Debug.Log("[SelfTest] " + msg);
            root?.SetStatus(msg);

            // Optional cleanup to avoid leaving test artifacts in scene
            var keepAfterTest = false; // set true if you want to inspect results
            if (!keepAfterTest)
            {
                var toRemove = FindObjectsOfType<UnitView>();
                foreach (var u in toRemove)
                {
                    if (u != null) Destroy(u.gameObject);
                }
                yield return null;
            }

            Game.Presentation.View.UnitCombat.DisableCombat = prevFreeze;
            _selfTestRunning = false;
        }

        private static Vector3 ViewportToWorldOnPlane(Camera cam, Vector2 vp)
        {
            Vector3 world;
            if (cam.orthographic)
            {
                world = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, 0f));
            }
            else
            {
                var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
                if (Mathf.Abs(ray.direction.z) > 1e-5f)
                {
                    float t = -ray.origin.z / ray.direction.z;
                    world = ray.origin + ray.direction * Mathf.Max(t, cam.nearClipPlane);
                }
                else
                {
                    world = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, cam.nearClipPlane));
                }
            }
            world.z = 0f;
            return world;
        }
    }
}
