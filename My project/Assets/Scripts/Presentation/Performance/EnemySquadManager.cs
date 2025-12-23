using System.Collections.Generic;
using UnityEngine;
using Game.Presentation.View;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Groups nearby enemy units into squads and assigns them a shared target.
    /// Reduces random dispersion and encourages path reuse.
    /// </summary>
    public class EnemySquadManager : MonoBehaviour
    {
        public static EnemySquadManager Instance { get; private set; }

        [Tooltip("How often to regroup enemies into squads.")]
        public float Interval = 0.2f;
        [Tooltip("Max squad size (target concept: ~12).")]
        public int MaxSquadSize = 8;
        [Tooltip("Radius in world units to collect squad members.")]
        public float SquadRadius = 6f;
        [Tooltip("How long (seconds) squad target stays assigned before refresh.")]
        public float SquadTargetTTL = 1.0f;
        [Tooltip("Also assign squad targets for player units.")]
        public bool DrivePlayers = true;
        [Header("Shared Path")]
        [Tooltip("If enabled, build one path for the squad leader and reuse it for members with offsets.")]
        public bool ShareLeaderPath = false;
        [Tooltip("Allow player squads to reuse the leader path (ignored if DrivePlayers is false).")]
        public bool ShareLeaderPathForPlayers = false;
        [Tooltip("Offset radius (world units) for squad formation around leader when reusing path.")]
        public float FormationRadius = 0.6f;

        private float _timer;
        private readonly List<UnitCombat> _enemies = new List<UnitCombat>(128);
        private readonly List<UnitCombat> _players = new List<UnitCombat>(128);

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
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;

            GatherUnits();
            if (_enemies.Count == 0 || _players.Count == 0) return;

            ProcessSquads(_enemies, _players, ShareLeaderPath);
            if (DrivePlayers)
                ProcessSquads(_players, _enemies, ShareLeaderPathForPlayers);
        }

        private void GatherUnits()
        {
            _enemies.Clear();
            _players.Clear();
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                if (uc.Faction == Game.Domain.Units.Faction.Enemy)
                    _enemies.Add(uc);
                else if (uc.Faction == Game.Domain.Units.Faction.Player)
                    _players.Add(uc);
            }
        }

        private void ProcessSquads(List<UnitCombat> movers, List<UnitCombat> targets, bool allowSharedPath)
        {
            if (movers == null || targets == null || movers.Count == 0 || targets.Count == 0) return;

            float r2 = SquadRadius * SquadRadius;
            var used = new bool[movers.Count];
            for (int i = 0; i < movers.Count; i++)
            {
                if (used[i]) continue;
                var leader = movers[i];
                if (leader == null) continue;
                var squad = new List<UnitCombat>(MaxSquadSize) { leader };
                used[i] = true;

                var lp = leader.transform.position;
                for (int j = i + 1; j < movers.Count && squad.Count < MaxSquadSize; j++)
                {
                    if (used[j]) continue;
                    var cand = movers[j];
                    if (cand == null) continue;
                    var d2 = (cand.transform.position - lp).sqrMagnitude;
                    if (d2 <= r2)
                    {
                        squad.Add(cand);
                        used[j] = true;
                    }
                }

                var target = FindNearestTarget(lp, targets);
                if (target == null) continue;

                if (allowSharedPath && squad.Count > 1)
                    AssignSharedPath(squad, target);
                else
                    AssignTargetsOnly(squad, target);
            }
        }

        private UnitCombat FindNearestTarget(Vector3 from, List<UnitCombat> targets)
        {
            UnitCombat best = null;
            float best2 = float.MaxValue;
            foreach (var uc in targets)
            {
                if (uc == null) continue;
                float d2 = (uc.transform.position - from).sqrMagnitude;
                if (d2 < best2)
                {
                    best2 = d2;
                    best = uc;
                }
            }
            return best;
        }

        private void AssignTargetsOnly(List<UnitCombat> squad, UnitCombat target)
        {
            foreach (var uc in squad)
            {
                if (uc == null) continue;
                uc.AssignSquadTarget(target, SquadTargetTTL);
            }
        }

        private void AssignSharedPath(List<UnitCombat> squad, UnitCombat target)
        {
            if (squad == null || squad.Count == 0 || target == null) return;
            var leader = squad[0];
            if (leader == null) return;
            var leaderView = leader.GetComponent<UnitView>();
            if (leaderView == null) return;

            var pm = Game.Presentation.Pathfinding.PathManager.Ensure();
            var targetWorld = target.transform.position;
            if (!pm.BuildPath(leaderView, targetWorld, allowDiag: true, smooth: true, autoFit: false, out var path))
            {
                AssignTargetsOnly(squad, target);
                return;
            }
            int maxNodes = pm.MaxPathNodes > 0 ? pm.MaxPathNodes : 2048;
            if (path != null && path.Count > maxNodes)
            {
                Game.Presentation.Pathfinding.PathManager.ReturnWorldList(path);
                AssignTargetsOnly(squad, target);
                return;
            }

            // Assign leader path
            var leaderFollower = leaderView.GetComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
            if (leaderFollower == null) leaderFollower = leaderView.gameObject.AddComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
            leaderFollower.SetWorldPath(path, Game.Presentation.Pathfinding.UnitPathFollower.PathSource.Combat);
            leader.AssignSquadTarget(target, SquadTargetTTL);

            // Prepare offsets
            var offsets = BuildOffsets(squad.Count, FormationRadius);
            Vector3 leaderPos = leader.transform.position;
            Vector3 forward = (targetWorld - leaderPos);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.right;
            forward = forward.normalized;
            Vector3 right = new Vector3(forward.y, -forward.x, 0f);

            for (int i = 1; i < squad.Count && i < offsets.Count; i++)
            {
                var member = squad[i];
                if (member == null) continue;
                var view = member.GetComponent<UnitView>();
                if (view == null) continue;
                var follower = view.GetComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
                if (follower == null) follower = view.gameObject.AddComponent<Game.Presentation.Pathfinding.UnitPathFollower>();

                var offsetLocal = offsets[i];
                var offsetWorld = forward * offsetLocal.y + right * offsetLocal.x;
                follower.SetSharedPathWithOffset(path, offsetWorld, Game.Presentation.Pathfinding.UnitPathFollower.PathSource.Combat);
                member.AssignSquadTarget(target, SquadTargetTTL);
            }

            // Return base path to pool after followers consumed it
            Game.Presentation.Pathfinding.PathManager.ReturnWorldList(path);
        }

        private List<Vector2> BuildOffsets(int count, float radius)
        {
            var res = new List<Vector2>(count);
            res.Add(Vector2.zero); // leader
            if (count <= 1) return res;
            float r = Mathf.Max(0.1f, radius);
            int ring = 1;
            while (res.Count < count && ring < 4)
            {
                int slots = 6 * ring;
                for (int i = 0; i < slots && res.Count < count; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / slots;
                    res.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r * ring);
                }
                ring++;
            }
            return res;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("EnemySquadManager");
            go.AddComponent<EnemySquadManager>();
        }
    }
}
