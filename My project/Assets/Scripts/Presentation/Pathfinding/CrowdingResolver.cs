/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs
@module: presentation.pathfinding.crowding
@purpose: Periodically resolves multi-unit stacks in the same hex by nudging excess units toward nearby free cells.
@entry: CrowdingResolver.LateUpdate, CROWD-02
@api: scene MonoBehaviour helper used by movement/combat systems
@deps: HexPathfindingBootstrap, PathManager, OccupancyHash, UnitCombat, UnitView
@data: per-cell grouping map, pooled unit lists, move cooldowns, adaptive-throttle state
@perf: medium hotpath under crowd pressure; work is throttled by frame time and population
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual overlap-stack verification
@config: Interval, SearchRadius, MaxGroupsPerTick, AdaptiveThrottling, MoveCooldown
@assets: none
@notes: this system should stay subordinate to ORCA/flow-field ownership and never fight squad-driven movement
*/

using System.Collections.Generic;
using System.Linq;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER]
// Logical block: Scripts/Presentation/Pathfinding/CrowdingResolver.

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Periodically looks for stacks of units in the same hex and nudges extra units to nearest free hex.
    /// Allows traversal through friendlies but never through enemies.
    /// Runs in LateUpdate to act after combat/path updates.
    /// </summary>
    public partial class CrowdingResolver : MonoBehaviour
    {
        // [CROWD-01]
        // Resolver config, cached dependencies, per-cell groups, and adaptive-throttle state.
        public float Interval = 0.12f;
        public int SearchRadius = 4;
        [Tooltip("Max stacks resolved per tick to avoid spikes with large crowds.")]
        public int MaxGroupsPerTick = 8;
        [Tooltip("Limit free slots considered per stack to avoid runaway lists.")]
        public int MaxSlotsPerGroup = 12;
        [Tooltip("If true, skip building new paths when path builder budget is exhausted; keep current path instead.")]
        public bool SkipWhenBuilderBusy = true;
        [Tooltip("How many units may remain in the source cell; only units beyond this index are nudged.")]
        public int AllowStayCountPerCell = 2;
        [Header("Population scaling")]
        [Tooltip("Auto-scale group processing based on unit count.")]
        public bool AutoScaleByPopulation = true;
        [Tooltip("Units per step to add another group to process.")]
        public int UnitsPerCrowdGroupStep = 30;
        [Tooltip("Units per step to reduce search radius by 1.")]
        public int UnitsPerRadiusStep = 50;
        [Header("Adaptive throttle")]
        [Tooltip("Adjust SearchRadius/MaxGroupsPerTick down when frame time spikes.")]
        public bool AdaptiveThrottling = true;
        [Tooltip("Frame time (seconds) above which we start to reduce work (~45 FPS).")]
        public float FrameTimeSoftLimit = 1f / 45f;
        [Tooltip("Frame time (seconds) above which we aggressively reduce work (~30 FPS).")]
        public float FrameTimeHardLimit = 1f / 30f;
        [Tooltip("Frame time (seconds) below which we allow slight boost of radius/groups (~50 FPS).")]
        public float FrameTimeBoostLimit = 1f / 50f;
        [Tooltip("Minimum search radius when throttled.")]
        public int MinSearchRadius = 2;
        [Tooltip("Minimum groups processed per tick when throttled.")]
        public int MinGroupsPerTick = 1;
        [Header("Debug")]
        [Tooltip("Print effective radius/groups occasionally to see how scaling behaves.")]
        public bool DebugLogEffective = false;
        public float DebugLogInterval = 1.5f;
        [Header("Move throttling")]
        [Tooltip("Cooldown between resolver moves for the same unit to avoid ping-pong.")]
        public float MoveCooldown = 2.0f;
        [Header("No-enemy mode")]
        [Tooltip("Allow a short window to resolve stacks when no enemies exist, then stay idle to avoid wandering.")]
        public bool ResolveWithoutEnemies = true;
        [Tooltip("How long after last enemy disappears we keep resolving stacks (seconds).")]
        public float NoEnemyResolveWindow = 1.5f;
        [Tooltip("Randomize sorting of groups each tick to avoid trains in one direction.")]
        public bool ShuffleGroups = true;
        [Header("Diagnostics")]
        [Tooltip("Log how many stacks and moves were processed per tick (throttled).")]
        public bool LogTickActivity = false;
        public int MaxLogsPerTick = 3;

        private float _timer;
        private HexPathfindingBootstrap _hex;
        private PathManager _pm;
        private readonly Dictionary<Vector2Int, List<UnitView>> _groups = new Dictionary<Vector2Int, List<UnitView>>(64);
        private readonly Stack<List<UnitView>> _listPool = new Stack<List<UnitView>>();
        private Game.Presentation.Performance.OccupancyHash _occ;
        private float _avgFrameTime;
        private float _lastLogTime;
        private int _lastLogRadius;
        private int _lastLogGroups;
        private readonly Dictionary<int, float> _lastMoveTimes = new Dictionary<int, float>(128);
        private readonly List<int> _tmpMoveKeys = new List<int>(32);
        private float _noEnemyTimer;
        private readonly List<KeyValuePair<Vector2Int, List<UnitView>>> _tmpGroupList = new List<KeyValuePair<Vector2Int, List<UnitView>>>(64);
        private bool _enemiesPresent;
        private int _logCountThisTick;

        // [CROWD-02]
        // LateUpdate crowd-resolution tick: group stacked units, throttle work, and issue short nudges.
        private void LateUpdate()
        {
            if (Game.Presentation.Performance.OrcaAvoidanceSystem.IsActive)
                return;
            var legacyAvoid = Game.Presentation.Performance.LocalAvoidanceSystem.Instance;
            if (legacyAvoid != null && legacyAvoid.isActiveAndEnabled && legacyAvoid.Enabled)
                return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;
            _logCountThisTick = 0;

            EnsureCaches();
            if (_hex == null || _pm == null) return;

            // Smooth frame time to drive adaptive throttling
            _avgFrameTime = Mathf.Lerp(_avgFrameTime <= 0f ? Time.deltaTime : _avgFrameTime, Time.deltaTime, 0.1f);
            int unitCount = UnitCombat.All.Count;
            int effectiveRadius = ComputeSearchRadius(unitCount);
            int effectiveMaxGroups = ComputeMaxGroups(unitCount);
            bool builderBusy = SkipWhenBuilderBusy && PathManager.BuildBudgetExhausted;

            ReleaseGroupLists();

            _enemiesPresent = UnitCombat.All.Any(uc => uc != null && uc.isActiveAndEnabled && UnitCombat.All.Any(other => other != null && other.isActiveAndEnabled && other.Faction != uc.Faction));
            if (!_enemiesPresent)
            {
                if (!ResolveWithoutEnemies)
                    return;
                if (_noEnemyTimer <= 0f)
                    _noEnemyTimer = NoEnemyResolveWindow;
            }
            else
            {
                _noEnemyTimer = 0f; // reset; normal mode
            }

            // group units by cell
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                if (uc.IsInSquad) continue;
                if (uc.IsUsingFlowField) continue;
                var view = uc.GetComponent<UnitView>();
                if (view == null) continue;
                var cell = _hex.WorldToGrid(uc.transform.position);
                if (!_groups.TryGetValue(cell, out var list))
                {
                    list = RentList();
                    _groups[cell] = list;
                }
                list.Add(view);
            }

            // bail out if no stacks beyond allowed stay count
            bool hasStack = false;
            foreach (var kv in _groups)
            {
                if (kv.Value != null && kv.Value.Count > AllowStayCountPerCell)
                {
                    hasStack = true;
                    break;
                }
            }
            if (!hasStack)
            {
                ReleaseGroupLists();
                return;
            }
            if (!_enemiesPresent)
            {
                _noEnemyTimer -= Interval;
                if (_noEnemyTimer < 0f)
                {
                    ReleaseGroupLists();
                    return;
                }
            }

            int handled = 0;
            int movedUnits = 0;
            _tmpGroupList.Clear();
            foreach (var kv in _groups) _tmpGroupList.Add(kv);
            if (ShuffleGroups && _tmpGroupList.Count > 1)
            {
                for (int i = 0; i < _tmpGroupList.Count; i++)
                {
                    int j = Random.Range(i, _tmpGroupList.Count);
                    (_tmpGroupList[i], _tmpGroupList[j]) = (_tmpGroupList[j], _tmpGroupList[i]);
                }
            }
            foreach (var kv in _tmpGroupList)
            {
                if (effectiveMaxGroups > 0 && handled >= effectiveMaxGroups) break;
                var list = kv.Value;
                if (list == null || list.Count <= AllowStayCountPerCell) continue;
                handled++;
                // keep some units in place; nudge only beyond AllowStayCountPerCell
                var ordered = list.Where(u => u != null).OrderBy(u => u.GetInstanceID()).ToList();
                if (ordered.Count <= AllowStayCountPerCell) continue;
                var reserved = new HashSet<int> { Key(kv.Key) };
                var freeCells = GatherFreeCells(_hex, kv.Key, effectiveRadius, reserved, ordered[0]);
                int freeIdx = 0;
                int startIdx = Mathf.Min(ordered.Count, Mathf.Max(1, AllowStayCountPerCell));
                for (int i = startIdx; i < ordered.Count; i++)
                {
                    var u = ordered[i];
                    if (u == null) continue;
                    int id = u.GetInstanceID();
                    if (_lastMoveTimes.TryGetValue(id, out var lastT) && Time.time - lastT < MoveCooldown)
                        continue; // recently moved, skip to avoid ping-pong
                    var follower = u.GetComponent<UnitPathFollower>();
                    var hasPath = follower != null && follower.HasPath;
                    var moving = u.TryGetDestination(out _);
                    if (hasPath || moving) continue; // let moving units finish
                    if (builderBusy) continue; // builder budget exhausted -> keep last path/destination

                    if (freeIdx >= freeCells.Count) break;
                    var targetCell = freeCells[freeIdx++];
                    var targetWorld = _hex.GridToWorld(targetCell.x, targetCell.y);
                    if (_pm.IsWorldOccupied(targetWorld, u, enemiesOnly: false))
                        continue; // avoid jumping onto friendlies
                    // Always use short nudge without invoking pathfinder to avoid path spam
                    u.SetDestination(targetWorld);
                    _lastMoveTimes[id] = Time.time;
                    // reserve the target cell so other units from same stack don't pick it this tick
                    reserved.Add(Key(targetCell));
                    movedUnits++;
                }
            }

            MaybeLog(effectiveRadius, effectiveMaxGroups, unitCount);
            if (LogTickActivity && MaxLogsPerTick != 0)
            {
                _logCountThisTick++;
                if (MaxLogsPerTick < 0 || _logCountThisTick <= MaxLogsPerTick)
                {
                    Debug.Log($"[CrowdingResolver] tick handledGroups={handled} movedUnits={movedUnits} radius={effectiveRadius} maxGroups={effectiveMaxGroups} units={unitCount} frameTime={_avgFrameTime:F3}s");
                }
            }
            else
            {
                _logCountThisTick = 0;
            }
            Game.Presentation.Pathfinding.PathProfiler.CountCrowdMoves(movedUnits);
        }

        // [CROWD-03]
        // Dependency cache setup, pooled-list cleanup, and ephemeral timestamp maintenance.
        private void EnsureCaches()
        {
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_pm == null)
                _pm = PathManager.Ensure();
            if (_occ == null)
                _occ = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Performance.OccupancyHash>();
        }

        private void ReleaseGroupLists()
        {
            foreach (var kv in _groups)
            {
                kv.Value.Clear();
                _listPool.Push(kv.Value);
            }
            _groups.Clear();
            // cleanup move timestamps occasionally
            if (_lastMoveTimes.Count > 0 && Time.frameCount % 60 == 0)
            {
                _tmpMoveKeys.Clear();
                foreach (var kv in _lastMoveTimes)
                {
                    if (kv.Value + MoveCooldown * 4f < Time.time)
                        _tmpMoveKeys.Add(kv.Key);
                }
                for (int i = 0; i < _tmpMoveKeys.Count; i++)
                    _lastMoveTimes.Remove(_tmpMoveKeys[i]);
                _tmpMoveKeys.Clear();
            }
        }

        private List<UnitView> RentList()
        {
            if (_listPool.Count > 0) return _listPool.Pop();
            return new List<UnitView>(4);
        }
    }
}
