using System.Collections;
using System.Collections.Generic;
using Game.Domain.Units;
using Game.Presentation.Bootstrap;
using Game.Presentation.Input;
using Game.Presentation.View;
using UnityEngine;

/*
@file: My project/Assets/Scripts/Presentation/UI/ActionsPanel.SelfTest.cs
@module: presentation.ui.actions
@purpose: Deterministic save/load self-test helpers extracted from ActionsPanel.
@entry: SCRIPTS-PRESENTATION-UI-ACTIONSPANEL-SELFTEST
@api: ActionsPanel partial self-test coroutine and viewport helpers
@deps: CompositionRoot, UnitSpawnerCommander, UnitView, UnitCombat, UnitHpOverlay
@data: expected unit snapshot records for validation
@perf: coroutine/debug only; no gameplay hot-path
@thread: main thread only
@tests: manual Self-Test Save/Load button flow
@config: uses current scene camera and CompositionRoot test research
@assets: UnitPrefab / DefaultUnitPrefab and faction sprites
@notes: kept separate from live dev actions to isolate destructive test logic
*/

// [CODE-ID: SCRIPTS-PRESENTATION-UI-ACTIONSPANEL-SELFTEST]
// Logical block: Scripts/Presentation/UI/ActionsPanel.SelfTest.

namespace Game.Presentation.UI
{
    public partial class ActionsPanel
    {
        private struct ExpectedUnit
        {
            public Vector3 Pos;
            public bool HasDest;
            public Vector3 Dest;
            public Faction Faction;
            public int Health;
        }

        private IEnumerator SelfTestRoutine()
        {
            _selfTestRunning = true;
            var cam = Camera.main;
            if (cam == null)
            {
                root?.SetStatus("SelfTest: No Camera");
                _selfTestRunning = false;
                yield break;
            }

            var spawner = UnityEngine.Object.FindAnyObjectByType<UnitSpawnerCommander>();
            UnitView prefab = spawner != null ? spawner.UnitPrefab : null;
            if (prefab == null && root != null) prefab = root.DefaultUnitPrefab;
            if (prefab == null)
            {
                root?.SetStatus("SelfTest: No Unit prefab");
                _selfTestRunning = false;
                yield break;
            }

            var prevFreeze = UnitCombat.DisableCombat;
            UnitCombat.DisableCombat = true;

            var existing = UnityEngine.Object.FindObjectsByType<UnitView>();
            foreach (var u in existing)
            {
                if (u != null) Destroy(u.gameObject);
            }
            yield return null;

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

            var expected = new List<ExpectedUnit>();
            for (int i = 0; i < vp.Length; i++)
            {
                var pos = ViewportToWorldOnPlane(cam, vp[i]);
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = $"SelfTest Unit {i}";

                var combat = go.GetComponent<UnitCombat>();
                if (combat == null) combat = go.gameObject.AddComponent<UnitCombat>();
                combat.Faction = factions[i];

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = combat.Faction == Faction.Enemy ? Color.red : Color.white;
                    if (root != null)
                    {
                        if (combat.Faction == Faction.Enemy && root.EnemySprite != null)
                            sr.sprite = root.EnemySprite;
                        else if (combat.Faction == Faction.Player && root.PlayerSprite != null)
                            sr.sprite = root.PlayerSprite;
                    }
                }

                if (go.GetComponent<UnitHpOverlay>() == null)
                    go.gameObject.AddComponent<UnitHpOverlay>();

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
                    case 1: hp = (int)(maxHp * 0.5f); break;
                    case 3: hp = (int)(maxHp * 0.3f); break;
                    default: hp = (int)(maxHp * 0.9f); break;
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

            string researchId = null;
            Game.Infrastructure.Configs.ResearchConfig rc = root != null ? root.TestResearch : null;
            if (rc != null && rc.Items != null && rc.Items.Length > 0)
            {
                researchId = rc.Items[0].Id;
                root.AttemptStartTestResearch();
            }

            root?.Save();
            root?.Load();
            yield return null;

            var after = UnityEngine.Object.FindObjectsByType<UnitView>();
            bool pass = true;
            var used = new HashSet<int>();
            int matched = 0;
            const float EPS = 0.02f;

            for (int i = 0; i < expected.Count; i++)
            {
                int bestJ = -1;
                float bestD2 = float.MaxValue;
                for (int j = 0; j < after.Length; j++)
                {
                    if (used.Contains(j)) continue;
                    float d2 = (after[j].transform.position - expected[i].Pos).sqrMagnitude;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        bestJ = j;
                    }
                }

                if (bestJ < 0 || bestD2 > EPS * EPS)
                {
                    pass = false;
                    continue;
                }

                used.Add(bestJ);
                matched++;
                var u = after[bestJ];
                var uc = u.GetComponent<UnitCombat>();
                bool hasDestAfter = u.TryGetDestination(out var d);
                if (hasDestAfter != expected[i].HasDest) pass = false;
                if (expected[i].HasDest && (d - expected[i].Dest).sqrMagnitude > EPS * EPS) pass = false;
                if (uc == null || (int)uc.Faction != (int)expected[i].Faction) pass = false;
                if (uc == null || uc.CurrentHealth != expected[i].Health) pass = false;
                if (u.GetComponent<UnitHpOverlay>() == null) pass = false;
            }

            if (!string.IsNullOrEmpty(researchId) && root != null && CompositionRoot.Game != null)
                _ = CompositionRoot.Game.Research.GetStatus(researchId);

            string msg = pass
                ? $"SelfTest: PASS (units: {expected.Count}/{after.Length} matched)"
                : $"SelfTest: FAIL (matched {matched}/{expected.Count}; total after load: {after.Length})";
            Debug.Log("[SelfTest] " + msg);
            root?.SetStatus(msg);

            const bool keepAfterTest = false;
            if (!keepAfterTest)
            {
                var toRemove = UnityEngine.Object.FindObjectsByType<UnitView>();
                foreach (var u in toRemove)
                {
                    if (u != null) Destroy(u.gameObject);
                }
                yield return null;
            }

            UnitCombat.DisableCombat = prevFreeze;
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
