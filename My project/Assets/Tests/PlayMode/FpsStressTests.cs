using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Presentation.View;
using Game.Domain.Units;
using Game.Presentation.Pathfinding;
using Game.Presentation.Performance;

namespace Tests.PlayMode
{
    public class FpsStressTests
    {
        private const int Allies = 20;
        private const int Enemies = 20;
        private const float AllyClusterRadius = 5f;
        private const float EnemyClusterRadius = 15f;
        private const int WarmupFrames = 3;
        private const int MaxMeasurementFrames = 20;
        private const float MaxMeasurementSeconds = 20f;
        private static readonly int[] SweepCounts = new[] { 10, 12, 14, 16, 18, 20 };

        [UnityTest]
        public IEnumerator AlliesVs200_Dist50()
        {
            yield return RunScenario(50f);
        }

        [UnityTest]
        public IEnumerator AlliesVs200_Dist100()
        {
            yield return RunScenario(100f);
        }

        [UnityTest]
        public IEnumerator AlliesVs200_Dist200()
        {
            yield return RunSweep(SweepCounts, 200f);
        }

        private IEnumerator RunScenario(float distance)
        {
            yield return RunSingle(Allies, Enemies, distance);
        }

        private IEnumerator RunSingle(int allies, int enemies, float distance)
        {
            UnitCombat.DisableCombat = false;

            bool prevAnomalyLog = PathProfiler.EnableAnomalyLog;
            PathProfiler.EnableAnomalyLog = false;
            HexPathfindingBootstrap hex = null;
            bool createdBootstrap = false;
            EnemySquadManager squadMgr = null;
            try
            {
                hex = EnsureBootstrap(ref createdBootstrap);
                ConfigureBootstrap(hex);
                squadMgr = EnsureSquadManager();
                yield return MeasureScenario(allies, enemies, distance);
            }
            finally
            {
                PathProfiler.EnableAnomalyLog = prevAnomalyLog;
                Cleanup(createdBootstrap ? hex?.gameObject : null, squadMgr);
            }
        }

        private IEnumerator RunSweep(int[] counts, float distance)
        {
            UnitCombat.DisableCombat = false;

            bool prevAnomalyLog = PathProfiler.EnableAnomalyLog;
            PathProfiler.EnableAnomalyLog = false;
            HexPathfindingBootstrap hex = null;
            bool createdBootstrap = false;
            EnemySquadManager squadMgr = null;
            try
            {
                hex = EnsureBootstrap(ref createdBootstrap);
                ConfigureBootstrap(hex);
                squadMgr = EnsureSquadManager();
                for (int i = 0; i < counts.Length; i++)
                {
                    int count = counts[i];
                    yield return MeasureScenario(count, count, distance);
                }
            }
            finally
            {
                PathProfiler.EnableAnomalyLog = prevAnomalyLog;
                Cleanup(createdBootstrap ? hex?.gameObject : null, squadMgr);
            }
        }

        private IEnumerator MeasureScenario(int allies, int enemies, float distance)
        {
            SpawnUnits(allies, Faction.Player, Vector3.zero, AllyClusterRadius);
            SpawnUnits(enemies, Faction.Enemy, new Vector3(distance, 0f, 0f), EnemyClusterRadius);

            for (int i = 0; i < WarmupFrames; i++) { yield return null; }

            int samples = 0;
            float totalSeconds = 0f;
            float measureStart = Time.realtimeSinceStartup;
            while (samples < MaxMeasurementFrames && (Time.realtimeSinceStartup - measureStart) < MaxMeasurementSeconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.00001f)
                {
                    totalSeconds += dt;
                    samples++;
                }
            }

            float avgFps = totalSeconds > 0f ? samples / totalSeconds : 0f;
            Debug.Log($"[FpsStress] Allies={allies} Enemies={enemies} distance={distance} avgFps={avgFps:F2} samples={samples} seconds={totalSeconds:F2}");
            CleanupUnits();
        }

        private static HexPathfindingBootstrap EnsureBootstrap(ref bool createdBootstrap)
        {
            var hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (hex == null)
            {
                var go = new GameObject("HexPathfindingBootstrap (Test)");
                hex = go.AddComponent<HexPathfindingBootstrap>();
                hex.Width = 1024;
                hex.Height = 1024;
                hex.HexSize = 0.4f;
                hex.Origin = Vector2.zero;
                hex.AutoFitToCamera = false;
                hex.AutoBakeColliders = false;
                createdBootstrap = true;
            }
            return hex;
        }

        private static EnemySquadManager EnsureSquadManager()
        {
            var mgr = UnityEngine.Object.FindAnyObjectByType<EnemySquadManager>();
            if (mgr == null)
            {
                var go = new GameObject("EnemySquadManager (Test)");
                mgr = go.AddComponent<EnemySquadManager>();
            }
            return mgr;
        }

        private static void ConfigureBootstrap(HexPathfindingBootstrap hex)
        {
            if (hex == null) return;
            hex.DrawGrid = false;
            hex.DrawOnlyVisible = true;
            hex.LogBake = false;
        }

        private static void SpawnUnits(int count, Faction faction, Vector3 center, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"{faction}_unit_{i}");
                go.transform.position = center + Random.insideUnitSphere * radius;
                go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, 0f);
                go.AddComponent<SpriteRenderer>();
                var view = go.AddComponent<UnitView>();
                view.Stats = new UnitStats { MaxHealth = 50, Speed = 2.5f };
                var combat = go.AddComponent<UnitCombat>();
                combat.Faction = faction;
            }
        }

        private static void Cleanup(GameObject bootstrap, EnemySquadManager squadMgr)
        {
            CleanupUnits();
            if (bootstrap != null)
                Object.DestroyImmediate(bootstrap);
            if (squadMgr != null)
                Object.DestroyImmediate(squadMgr.gameObject);
        }

        private static void CleanupUnits()
        {
            Game.Presentation.Pathfinding.PathRequestQueue.Instance?.CompleteActiveJobAndClear();
            foreach (var uc in new System.Collections.Generic.List<UnitCombat>(UnitCombat.All))
            {
                if (uc != null)
                    Object.DestroyImmediate(uc.gameObject);
            }
            UnitCombat.All.Clear();
        }
    }
}
