using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Presentation.View;
using Game.Domain.Units;
using Game.Presentation.Pathfinding;
using Game.Presentation.Performance;

// [CODE-ID: TESTS-PLAYMODE-FPSSTRESSTESTS]
// Logical block: Tests/PlayMode/FpsStressTests.

namespace Tests.PlayMode
{
    [Category("Diagnostic")]
    [Explicit("Diagnostic benchmark only: logs FPS metrics but has no project performance budget assertions yet.")]
    public class FpsStressTests
    {
        private const int Allies = 20;
        private const int Enemies = 20;
        private const int OwnerTargetAllies = 100;
        private const int OwnerTargetEnemies = 100;
        private const float OwnerTargetDistance = 200f;
        private const float OwnerTargetPathPressureDistance = 12f;
        private const int OwnerTargetMeasurementFrames = 90;
        private const float OwnerTargetMeasurementSeconds = 10f;
        private const float OwnerTargetClusterRadius = 2.5f;
        private const float AllyClusterRadius = 5f;
        private const float EnemyClusterRadius = 15f;
        private const int WarmupFrames = 12;
        private const int MaxMeasurementFrames = 20;
        private const float MaxMeasurementSeconds = 20f;
        private const int RandomSeed = 13791;
        private const string FpsStressPrefix = "[FpsStress]";
        private const string CombatPressureProbePrefix = "[CombatPressureProbe]";
        private static readonly int[] SweepCounts = new[] { 10, 12, 14, 16, 18, 20 };

        [UnityTest]
        public IEnumerator AlliesVs20Enemies_Dist50()
        {
            yield return RunScenario(50f);
        }

        [UnityTest]
        public IEnumerator AlliesVs20Enemies_Dist100()
        {
            yield return RunScenario(100f);
        }

        [UnityTest]
        public IEnumerator AlliesVsEnemiesSweep_Dist200()
        {
            yield return RunSweep(SweepCounts, 200f);
        }

        [UnityTest]
        public IEnumerator OwnerTarget100v100_Dist200_LogsCombatPressureProbe()
        {
            yield return RunSingle(
                OwnerTargetAllies,
                OwnerTargetEnemies,
                OwnerTargetDistance,
                logCombatPressureProbe: true);
        }

        [UnityTest]
        public IEnumerator OwnerTarget100v100_PathPressure_LogsCombatPressureProbe()
        {
            yield return RunSingle(
                OwnerTargetAllies,
                OwnerTargetEnemies,
                OwnerTargetPathPressureDistance,
                logCombatPressureProbe: true,
                forcePathPressure: true,
                maxMeasurementFrames: OwnerTargetMeasurementFrames,
                maxMeasurementSeconds: OwnerTargetMeasurementSeconds,
                allyClusterRadius: OwnerTargetClusterRadius,
                enemyClusterRadius: OwnerTargetClusterRadius,
                allyCenter: new Vector3(48f, 48f, 0f));
        }

        private IEnumerator RunScenario(float distance)
        {
            yield return RunSingle(Allies, Enemies, distance);
        }

        private IEnumerator RunSingle(
            int allies,
            int enemies,
            float distance,
            bool logCombatPressureProbe = false,
            bool forcePathPressure = false,
            int maxMeasurementFrames = MaxMeasurementFrames,
            float maxMeasurementSeconds = MaxMeasurementSeconds,
            float allyClusterRadius = AllyClusterRadius,
            float enemyClusterRadius = EnemyClusterRadius,
            Vector3? allyCenter = null)
        {
            UnitCombat.DisableCombat = false;

            bool prevAnomalyLog = PathProfiler.EnableAnomalyLog;
            int previousTargetSearchBudget = UnitCombat.TargetSearchBudgetPerFrame;
            int previousRepathBudget = UnitCombat.RepathBudgetPerFrame;
            int previousCombatTickBudget = UnitCombat.CombatTickBudgetPerFrame;
            PathRequestQueue queue = null;
            bool previousQueueUseJobs = true;
            bool previousQueueProcessSynchronouslyIfIdle = false;
            int previousQueueMaxPerFrame = 32;
            int previousQueueMaxQueueSize = 512;
            PathProfiler.EnableAnomalyLog = false;
            var previousRandomState = Random.state;
            Random.InitState(RandomSeed + Mathf.RoundToInt(distance));
            HexPathfindingBootstrap hex = null;
            bool createdBootstrap = false;
            EnemySquadManager squadMgr = null;
            try
            {
                hex = EnsureBootstrap(ref createdBootstrap);
                ConfigureBootstrap(hex);
                squadMgr = forcePathPressure ? null : EnsureSquadManager();
                if (forcePathPressure)
                    ConfigurePressurePathing(
                        out queue,
                        out previousQueueUseJobs,
                        out previousQueueProcessSynchronouslyIfIdle,
                        out previousQueueMaxPerFrame,
                        out previousQueueMaxQueueSize);

                yield return MeasureScenario(
                    allies,
                    enemies,
                    distance,
                    logCombatPressureProbe,
                    forcePathPressure,
                    maxMeasurementFrames,
                    maxMeasurementSeconds,
                    allyClusterRadius,
                    enemyClusterRadius,
                    allyCenter ?? Vector3.zero);
            }
            finally
            {
                Random.state = previousRandomState;
                PathProfiler.EnableAnomalyLog = prevAnomalyLog;
                UnitCombat.TargetSearchBudgetPerFrame = previousTargetSearchBudget;
                UnitCombat.RepathBudgetPerFrame = previousRepathBudget;
                UnitCombat.CombatTickBudgetPerFrame = previousCombatTickBudget;
                RestoreQueueSettings(
                    queue,
                    previousQueueUseJobs,
                    previousQueueProcessSynchronouslyIfIdle,
                    previousQueueMaxPerFrame,
                    previousQueueMaxQueueSize);
                Cleanup(createdBootstrap ? hex?.gameObject : null, squadMgr);
            }
        }

        private IEnumerator RunSweep(int[] counts, float distance)
        {
            UnitCombat.DisableCombat = false;

            bool prevAnomalyLog = PathProfiler.EnableAnomalyLog;
            PathProfiler.EnableAnomalyLog = false;
            var previousRandomState = Random.state;
            Random.InitState(RandomSeed + Mathf.RoundToInt(distance));
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
                    yield return MeasureScenario(
                        count,
                        count,
                        distance,
                        logCombatPressureProbe: false,
                        forcePathPressure: false,
                        maxMeasurementFrames: MaxMeasurementFrames,
                        maxMeasurementSeconds: MaxMeasurementSeconds,
                        allyClusterRadius: AllyClusterRadius,
                        enemyClusterRadius: EnemyClusterRadius,
                        allyCenter: Vector3.zero);
                }
            }
            finally
            {
                Random.state = previousRandomState;
                PathProfiler.EnableAnomalyLog = prevAnomalyLog;
                Cleanup(createdBootstrap ? hex?.gameObject : null, squadMgr);
            }
        }

        private IEnumerator MeasureScenario(
            int allies,
            int enemies,
            float distance,
            bool logCombatPressureProbe,
            bool forcePathPressure,
            int maxMeasurementFrames,
            float maxMeasurementSeconds,
            float allyClusterRadius,
            float enemyClusterRadius,
            Vector3 allyCenter)
        {
            Vector3 enemyCenter = allyCenter + new Vector3(distance, 0f, 0f);
            var allyUnits = SpawnUnits(allies, Faction.Player, allyCenter, allyClusterRadius);
            var enemyUnits = SpawnUnits(enemies, Faction.Enemy, enemyCenter, enemyClusterRadius);
            var playerAttackEvents = new Counter();
            var enemyAttackEvents = new Counter();
            if (forcePathPressure)
            {
                ConfigurePressureUnits(allyUnits, enemyUnits, playerAttackEvents, enemyAttackEvents);
                ForcePairedTargets(allyUnits, enemyUnits);
            }

            for (int i = 0; i < WarmupFrames; i++) { yield return null; }

            PathProfiler.CollectAndReset();
            PathProfiler.ResetTotals();
            PathRequestQueue.CollectJobStatsAndReset();

            int samples = 0;
            float totalSeconds = 0f;
            float maxFrameMs = 0f;
            int maxBuildsFrame = 0;
            int totalBuilds = 0;
            int totalAccepts = 0;
            int totalRejects = 0;
            int maxCommandsFrame = 0;
            int totalCommands = 0;
            int maxPathsFrame = 0;
            int totalPaths = 0;
            int totalPathLength = 0;
            int maxPathLengthFrame = 0;
            int maxJitterFrame = 0;
            int totalJitter = 0;
            int maxCrowdMovesFrame = 0;
            int totalCrowdMoves = 0;
            int maxPathResetsFrame = 0;
            int totalJobScheduled = 0;
            int totalJobCompleted = 0;
            int totalJobFallback = 0;
            int slowFrameSample = -1;
            int slowFrameCommands = 0;
            int slowFramePathResets = 0;
            int slowFrameBuilds = 0;
            int slowFramePaths = 0;
            int slowFrameJobScheduled = 0;
            int slowFrameJobCompleted = 0;
            int slowFrameJobFallback = 0;
            int slowFrameAttackEvents = 0;
            int noActivityFrames = 0;
            float noActivityFrameMs = 0f;
            float noActivityMaxFrameMs = 0f;
            int attackActivityFrames = 0;
            float attackActivityFrameMs = 0f;
            float attackActivityMaxFrameMs = 0f;
            int pathActivityFrames = 0;
            float pathActivityFrameMs = 0f;
            float pathActivityMaxFrameMs = 0f;
            int jobActivityFrames = 0;
            float jobActivityFrameMs = 0f;
            float jobActivityMaxFrameMs = 0f;
            int maxAttackEventsFrame = 0;
            int previousAttackEvents = playerAttackEvents.Value + enemyAttackEvents.Value;
            float measureStart = Time.realtimeSinceStartup;
            while (samples < maxMeasurementFrames && (Time.realtimeSinceStartup - measureStart) < maxMeasurementSeconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                var pathStats = PathProfiler.CollectAndReset();
                var jobStats = PathRequestQueue.CollectJobStatsAndReset();
                int attackEventsThisFrame = playerAttackEvents.Value + enemyAttackEvents.Value - previousAttackEvents;
                previousAttackEvents = playerAttackEvents.Value + enemyAttackEvents.Value;
                bool hasAttackActivity = attackEventsThisFrame > 0;
                bool hasPathActivity =
                    pathStats.CommandsThisFrame > 0
                    || pathStats.PathResetsThisFrame > 0
                    || pathStats.BuildsThisFrame > 0
                    || pathStats.PathsThisFrame > 0;
                bool hasJobActivity = jobStats.Scheduled > 0 || jobStats.Completed > 0 || jobStats.Fallback > 0;
                if (dt > 0.00001f)
                {
                    float frameMs = dt * 1000f;
                    totalSeconds += dt;
                    if (hasAttackActivity)
                    {
                        attackActivityFrames++;
                        attackActivityFrameMs += frameMs;
                        attackActivityMaxFrameMs = Mathf.Max(attackActivityMaxFrameMs, frameMs);
                    }
                    if (hasPathActivity)
                    {
                        pathActivityFrames++;
                        pathActivityFrameMs += frameMs;
                        pathActivityMaxFrameMs = Mathf.Max(pathActivityMaxFrameMs, frameMs);
                    }
                    if (hasJobActivity)
                    {
                        jobActivityFrames++;
                        jobActivityFrameMs += frameMs;
                        jobActivityMaxFrameMs = Mathf.Max(jobActivityMaxFrameMs, frameMs);
                    }
                    if (!hasAttackActivity && !hasPathActivity && !hasJobActivity)
                    {
                        noActivityFrames++;
                        noActivityFrameMs += frameMs;
                        noActivityMaxFrameMs = Mathf.Max(noActivityMaxFrameMs, frameMs);
                    }
                    if (frameMs > maxFrameMs)
                    {
                        maxFrameMs = frameMs;
                        slowFrameSample = samples;
                        slowFrameCommands = pathStats.CommandsThisFrame;
                        slowFramePathResets = pathStats.PathResetsThisFrame;
                        slowFrameBuilds = pathStats.BuildsThisFrame;
                        slowFramePaths = pathStats.PathsThisFrame;
                        slowFrameJobScheduled = jobStats.Scheduled;
                        slowFrameJobCompleted = jobStats.Completed;
                        slowFrameJobFallback = jobStats.Fallback;
                        slowFrameAttackEvents = attackEventsThisFrame;
                    }
                    maxAttackEventsFrame = Mathf.Max(maxAttackEventsFrame, attackEventsThisFrame);
                    maxBuildsFrame = Mathf.Max(maxBuildsFrame, pathStats.BuildsThisFrame);
                    totalBuilds += pathStats.BuildsThisFrame;
                    totalAccepts += pathStats.Accepts;
                    totalRejects += pathStats.Rejects;
                    maxCommandsFrame = Mathf.Max(maxCommandsFrame, pathStats.CommandsThisFrame);
                    totalCommands += pathStats.CommandsThisFrame;
                    maxPathsFrame = Mathf.Max(maxPathsFrame, pathStats.PathsThisFrame);
                    totalPaths += pathStats.PathsThisFrame;
                    totalPathLength += pathStats.TotalPathLengthThisFrame;
                    maxPathLengthFrame = Mathf.Max(maxPathLengthFrame, pathStats.MaxPathLengthThisFrame);
                    maxJitterFrame = Mathf.Max(maxJitterFrame, pathStats.JitterThisFrame);
                    totalJitter += pathStats.JitterThisFrame;
                    maxCrowdMovesFrame = Mathf.Max(maxCrowdMovesFrame, pathStats.CrowdMovesThisFrame);
                    totalCrowdMoves += pathStats.CrowdMovesThisFrame;
                    maxPathResetsFrame = Mathf.Max(maxPathResetsFrame, pathStats.PathResetsThisFrame);
                    totalJobScheduled += jobStats.Scheduled;
                    totalJobCompleted += jobStats.Completed;
                    totalJobFallback += jobStats.Fallback;
                    samples++;
                }
            }

            float avgFps = totalSeconds > 0f ? samples / totalSeconds : 0f;
            float avgFrameMs = samples > 0 ? (totalSeconds / samples) * 1000f : 0f;
            Debug.Log(
                $"{FpsStressPrefix} Allies={allies} Enemies={enemies} distance={F1(distance)} "
                + $"avgFps={F2(avgFps)} samples={samples} seconds={F2(totalSeconds)}");

            if (logCombatPressureProbe)
            {
                int totalUnits = allies + enemies;
                int attackEvents = playerAttackEvents.Value + enemyAttackEvents.Value;
                int playerAlive = CountAlive(allyUnits);
                int enemyAlive = CountAlive(enemyUnits);
                var resetReasons = PathProfiler.GetResetReasonTotals();
                Debug.Log(
                    $"{CombatPressureProbePrefix} measurement=ownerTarget100v100 "
                    + $"playerUnits={allies} enemyUnits={enemies} totalUnits={totalUnits} "
                    + $"distance={F3(distance)} pathPressure={forcePathPressure} warmupFrames={WarmupFrames} frames={samples} seconds={F3(totalSeconds)} "
                    + $"avgFps={F3(avgFps)} avgFrameMs={F3(avgFrameMs)} maxFrameMs={F3(maxFrameMs)} "
                    + $"attackEvents={attackEvents} playerAttackEvents={playerAttackEvents.Value} enemyAttackEvents={enemyAttackEvents.Value} "
                    + $"maxAttackEventsFrame={maxAttackEventsFrame} "
                    + $"playerAlive={playerAlive} enemyAlive={enemyAlive} "
                    + $"pathResets={PathProfiler.TotalPathResets} maxPathResetsFrame={maxPathResetsFrame} "
                    + $"resetCombatInRange={GetResetReasonCount(resetReasons, "combat-in-range")} "
                    + $"resetPathComplete={GetResetReasonCount(resetReasons, "path-complete")} "
                    + $"resetFollowerCancel={GetResetReasonCount(resetReasons, "follower-cancel")} "
                    + $"resetCombatNoEnemies={GetResetReasonCount(resetReasons, "combat-no-enemies")} "
                    + $"resetCombatLostTarget={GetResetReasonCount(resetReasons, "combat-lost-target")} "
                    + $"totalPathBuilds={totalBuilds} totalPathAccepts={totalAccepts} totalPathRejects={totalRejects} "
                    + $"maxPathBuildsFrame={maxBuildsFrame} totalPathCommands={totalCommands} maxPathCommandsFrame={maxCommandsFrame} "
                    + $"totalPaths={totalPaths} maxPathsFrame={maxPathsFrame} totalPathLength={totalPathLength} maxPathLengthFrame={maxPathLengthFrame} "
                    + $"totalJobScheduled={totalJobScheduled} totalJobCompleted={totalJobCompleted} totalJobFallback={totalJobFallback} "
                    + $"noActivityFrames={noActivityFrames} noActivityAvgFrameMs={F3(AverageFrameMs(noActivityFrameMs, noActivityFrames))} noActivityMaxFrameMs={F3(noActivityMaxFrameMs)} "
                    + $"attackActivityFrames={attackActivityFrames} attackActivityAvgFrameMs={F3(AverageFrameMs(attackActivityFrameMs, attackActivityFrames))} attackActivityMaxFrameMs={F3(attackActivityMaxFrameMs)} "
                    + $"pathActivityFrames={pathActivityFrames} pathActivityAvgFrameMs={F3(AverageFrameMs(pathActivityFrameMs, pathActivityFrames))} pathActivityMaxFrameMs={F3(pathActivityMaxFrameMs)} "
                    + $"jobActivityFrames={jobActivityFrames} jobActivityAvgFrameMs={F3(AverageFrameMs(jobActivityFrameMs, jobActivityFrames))} jobActivityMaxFrameMs={F3(jobActivityMaxFrameMs)} "
                    + $"slowFrameSample={slowFrameSample} slowFrameCommands={slowFrameCommands} slowFramePathResets={slowFramePathResets} "
                    + $"slowFrameBuilds={slowFrameBuilds} slowFramePaths={slowFramePaths} "
                    + $"slowFrameJobScheduled={slowFrameJobScheduled} slowFrameJobCompleted={slowFrameJobCompleted} slowFrameJobFallback={slowFrameJobFallback} "
                    + $"slowFrameAttackEvents={slowFrameAttackEvents} "
                    + $"totalJitter={totalJitter} maxJitterFrame={maxJitterFrame} totalCrowdMoves={totalCrowdMoves} maxCrowdMovesFrame={maxCrowdMovesFrame}");
            }

            CleanupUnits();
        }

        private static void ConfigurePressurePathing(
            out PathRequestQueue queue,
            out bool previousUseJobs,
            out bool previousProcessSynchronouslyIfIdle,
            out int previousMaxPerFrame,
            out int previousMaxQueueSize)
        {
            UnitCombat.TargetSearchBudgetPerFrame = 200;
            UnitCombat.RepathBudgetPerFrame = 200;
            UnitCombat.CombatTickBudgetPerFrame = 48;

            PathRequestQueue.Ensure();
            queue = PathRequestQueue.Instance;
            previousUseJobs = queue.UseJobs;
            previousProcessSynchronouslyIfIdle = queue.ProcessSynchronouslyIfIdle;
            previousMaxPerFrame = queue.MaxPerFrame;
            previousMaxQueueSize = queue.MaxQueueSize;

            queue.CompleteActiveJobAndClear();
            queue.UseJobs = true;
            queue.ProcessSynchronouslyIfIdle = false;
            queue.MaxPerFrame = 2;
            queue.MaxQueueSize = 512;

            var pm = PathManager.Ensure();
            pm.MaxBuildsPerFrame = 0;
            pm.EnableGroupPathReuse = false;
        }

        private static void RestoreQueueSettings(
            PathRequestQueue queue,
            bool previousUseJobs,
            bool previousProcessSynchronouslyIfIdle,
            int previousMaxPerFrame,
            int previousMaxQueueSize)
        {
            if (queue == null)
                return;

            queue.CompleteActiveJobAndClear();
            queue.UseJobs = previousUseJobs;
            queue.ProcessSynchronouslyIfIdle = previousProcessSynchronouslyIfIdle;
            queue.MaxPerFrame = previousMaxPerFrame;
            queue.MaxQueueSize = previousMaxQueueSize;
        }

        private static void ConfigurePressureUnits(
            List<UnitCombat> allies,
            List<UnitCombat> enemies,
            Counter playerAttackEvents,
            Counter enemyAttackEvents)
        {
            for (int i = 0; i < allies.Count; i++)
                ConfigurePressureUnit(allies[i], playerAttackEvents);
            for (int i = 0; i < enemies.Count; i++)
                ConfigurePressureUnit(enemies[i], enemyAttackEvents);
        }

        private static void ConfigurePressureUnit(UnitCombat combat, Counter attackEvents)
        {
            if (combat == null)
                return;

            combat.UseFactionOverrides = false;
            combat.UseFlowFields = false;
            combat.PreferForcedTarget = true;
            combat.AttackRange = 2.5f;
            combat.AttackDamage = 1;
            combat.AttackCooldown = 0.15f;
            combat.CombatTickInterval = 0.02f;
            combat.CombatTickJitter = 0.02f;
            combat.TargetRefreshInterval = 0.02f;
            combat.RepathInterval = 0.05f;
            combat.RepathIntervalFar = 0.08f;
            combat.RepathIntervalVeryFar = 0.12f;
            combat.RepathJitter = 0.03f;
            combat.ClusterSizeForRepath2 = 4096;
            combat.FarClusterDistance2 = 4096;

            combat.OnAttack += () => attackEvents.Value++;
        }

        private static void ForcePairedTargets(List<UnitCombat> allies, List<UnitCombat> enemies)
        {
            if (allies.Count == 0 || enemies.Count == 0)
                return;

            for (int i = 0; i < allies.Count; i++)
                allies[i].AssignSquadTarget(enemies[i % enemies.Count], ttl: OwnerTargetMeasurementSeconds + 5f);
            for (int i = 0; i < enemies.Count; i++)
                enemies[i].AssignSquadTarget(allies[i % allies.Count], ttl: OwnerTargetMeasurementSeconds + 5f);
        }

        private static int CountAlive(List<UnitCombat> units)
        {
            int alive = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].isActiveAndEnabled)
                    alive++;
            }

            return alive;
        }

        private static int GetResetReasonCount(Dictionary<string, int> reasons, string reason)
        {
            return reasons != null && reasons.TryGetValue(reason, out int count) ? count : 0;
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

        private static List<UnitCombat> SpawnUnits(int count, Faction faction, Vector3 center, float radius)
        {
            var units = new List<UnitCombat>(count);
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
                units.Add(combat);
            }

            return units;
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
            PathProfiler.ResetTotals();
        }

        private static string F1(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static string F3(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

        private static float AverageFrameMs(float totalFrameMs, int frames) => frames > 0 ? totalFrameMs / frames : 0f;

        private sealed class Counter
        {
            public int Value;
        }
    }
}
