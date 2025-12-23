using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Presentation.Pathfinding;
using Game.Presentation.View;
using Game.Domain.Units;

namespace Tests.PlayMode
{
    public class CombatPathResetTests
    {
        [UnityTest]
        public IEnumerator CombatPathResetsStayLowDuringChase()
        {
            PathProfiler.ResetTotals();
            UnitCombat.DisableCombat = false;

            // Bootstrap minimal hex grid and path systems
            var hexGo = new GameObject("HexBootstrap");
            var hex = hexGo.AddComponent<Game.Presentation.Pathfinding.HexPathfindingBootstrap>();
            hex.Width = 64;
            hex.Height = 64;
            hex.HexSize = 0.4f;
            hex.AutoFitToCamera = false;
            hex.AutoBakeColliders = false;
            PathManager.Ensure();
            PathRequestQueue.Ensure();

            // Spawn attacker and target far enough to require a path
            var attacker = SpawnUnit("attacker", Vector3.zero, Faction.Player);
            var target = SpawnUnit("target", new Vector3(10f, 0f, 0f), Faction.Enemy);
            target.SetHealth(50);

            // Let them chase and fight for a few seconds while collecting diagnostics
            int chaseFrames = 0;
            int destChanges = 0;
            int destChangesDuringChase = 0;
            int centerDestChanges = 0;
            int destCellBacktracks = 0;
            Vector2Int? prevPrevDestCell = null;
            Vector2Int? prevDestCell = null;
            Vector3 lastDest = default;
            bool hadDest = false;
            var attackerView = attacker.GetComponent<UnitView>();
            var attackerCombat = attacker.GetComponent<UnitCombat>();
            float endTime = Time.time + 4.0f;
            while (Time.time < endTime)
            {
                var attackerPos = attacker.transform.position;
                float distToTarget = Vector3.Distance(attackerPos, target.transform.position);
                bool chasing = attackerCombat != null && distToTarget > attackerCombat.AttackRange * 1.1f;
                if (chasing) chaseFrames++;
                if (attackerView != null && attackerView.TryGetDestination(out var dest))
                {
                    bool destChanged = !hadDest || (dest - lastDest).sqrMagnitude > 0.0001f;
                    if (destChanged)
                    {
                        destChanges++;
                        if (chasing)
                        {
                            destChangesDuringChase++;
                            var currentCell = hex.WorldToGrid(attackerPos);
                            var cellCenter = hex.GridToWorld(currentCell.x, currentCell.y);
                            if ((dest - cellCenter).sqrMagnitude <= 0.0004f)
                                centerDestChanges++;
                        }
                        var destCell = hex.WorldToGrid(dest);
                        if (prevPrevDestCell.HasValue && prevDestCell.HasValue &&
                            destCell == prevPrevDestCell.Value && destCell != prevDestCell.Value)
                        {
                            destCellBacktracks++;
                        }
                        prevPrevDestCell = prevDestCell;
                        prevDestCell = destCell;
                        lastDest = dest;
                        hadDest = true;
                    }
                }
                else
                {
                    hadDest = false;
                }
                yield return null;
            }

            // Expect few path resets overall (no constant jitter)
            var reasons = PathProfiler.GetResetReasonTotals();
            float centerChangeRatio = destChangesDuringChase > 0 ? (float)centerDestChanges / destChangesDuringChase : 0f;
            TestContext.Out.WriteLine($"[CombatChaseMetrics] chaseFrames={chaseFrames} destChanges={destChanges} destChangesDuringChase={destChangesDuringChase} centerDestChanges={centerDestChanges} centerDestChangeRatio={centerChangeRatio:F3} destCellBacktracks={destCellBacktracks} resets={PathProfiler.TotalPathResets}");
            Assert.Less(PathProfiler.TotalPathResets, 15, $"Too many path/destination resets during combat chase (TotalPathResets={PathProfiler.TotalPathResets}). Reasons: {FormatReasons(reasons)}; chaseFrames={chaseFrames} destChanges={destChanges} destChangesDuringChase={destChangesDuringChase} centerDestChanges={centerDestChanges} centerDestChangeRatio={centerChangeRatio:F3} destCellBacktracks={destCellBacktracks}");
            Assert.Less(centerDestChanges, 3, $"Too many destination assignments to current hex center during chase (centerDestChanges={centerDestChanges}, destChangesDuringChase={destChangesDuringChase}, ratio={centerChangeRatio:F3}).");
            Assert.Less(destCellBacktracks, 3, $"Destination cell backtracks suggest hex-center oscillation (backtracks={destCellBacktracks}).");

            Object.Destroy(attacker.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(hexGo);
            PathProfiler.ResetTotals();
        }

        private static UnitCombat SpawnUnit(string name, Vector3 pos, Faction faction)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<UnitView>();
            view.Stats = new UnitStats { MaxHealth = 100, Speed = 3.5f };
            var combat = go.AddComponent<UnitCombat>();
            combat.Faction = faction;
            combat.AttackDamage = 5;
            combat.AttackCooldown = 0.4f;
            combat.RepathInterval = 0.15f;
            combat.RepathIntervalFar = 0.3f;
            combat.RepathIntervalVeryFar = 0.6f;
            combat.StallRepathSeconds = 0.2f;
            return combat;
        }

        private static string FormatReasons(System.Collections.Generic.Dictionary<string, int> reasons)
        {
            if (reasons == null || reasons.Count == 0) return "none";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in reasons)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append("=").Append(kv.Value);
            }
            return sb.ToString();
        }
    }
}
