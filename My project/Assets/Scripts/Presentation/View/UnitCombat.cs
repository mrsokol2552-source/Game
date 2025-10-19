using System.Collections.Generic;
using Game.Domain.Units;
using UnityEngine;
using Game.Presentation.Pathfinding;

namespace Game.Presentation.View
{
    [RequireComponent(typeof(UnitView))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class UnitCombat : MonoBehaviour
    {
        public static readonly HashSet<UnitCombat> All = new HashSet<UnitCombat>();
        public static bool DisableCombat = false; // Self-test or debug freeze

        [Header("Combat")]
        public Faction Faction = Faction.Player;
        public float AttackRange = 1.5f;
        public int AttackDamage = 10;
        public float AttackCooldown = 0.75f;

        private float _cooldown;
        private int _currentHealth;
        private UnitView _view;
        private float _repathTimer;
        private Vector3 _lastDesired;
        private bool _combatSteering;

        private void OnEnable()
        {
            All.Add(this);
            _view = GetComponent<UnitView>();
            if (_view == null) _view = gameObject.AddComponent<UnitView>();
            _currentHealth = Mathf.Max(1, _view.Stats.MaxHealth);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Update()
        {
            if (DisableCombat) return;
            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            var follower = GetComponent<UnitPathFollower>();
            bool pathActive = follower != null && follower.HasPath;

            var target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 tp = target.transform.position;
                Vector3 mp = transform.position;
                float dist = (tp - mp).magnitude;
                float stopDist = Mathf.Max(AttackRange * 0.9f, 0.1f);

                // If path follower is active, don't override its movement

                if (dist > AttackRange)
                {
                    // Move to a point at stopDist from the target
                    Vector3 dir = (mp - tp);
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
                    Vector3 desired = tp + dir.normalized * stopDist;

                    // Pathfinding is always enabled for combat steering
                    if (true)
                    {
                        if (_repathTimer > 0f) _repathTimer -= Time.deltaTime;
                        float delta2 = (desired - _lastDesired).sqrMagnitude;
                        if (_repathTimer <= 0f || delta2 > 0.05f * 0.05f || !pathActive)
                        {
                            var pm = PathManager.Ensure();
                            if (pm.BuildPath(_view, desired,
                                allowDiag: true,
                                smooth: true,
                                autoFit: false,
                                out var worldPath))
                            {
                                var follower2 = GetComponent<UnitPathFollower>();
                                if (follower2 == null) follower2 = gameObject.AddComponent<UnitPathFollower>();
                                else if (follower2.Source == UnitPathFollower.PathSource.Combat) follower2.Cancel();
                                follower2.SetWorldPath(worldPath, UnitPathFollower.PathSource.Combat);
                                _lastDesired = desired;
                                _repathTimer = 0.2f;
                                _combatSteering = true;
                            }
                            else if (!pathActive)
                            {
                                _view.SetDestination(desired);
                                _combatSteering = true;
                            }
                        }
                    }
                    else if (!pathActive)
                    {
                        _view.SetDestination(desired);
                        _combatSteering = true;
                    }
                }
                else
                {
                    // In range: attack on cooldown; avoid clearing destination if path is active
                    if (!pathActive && _combatSteering)
                        _view.ClearDestination();
                    if (_cooldown <= 0f)
                    {
                        target.ApplyDamage(AttackDamage);
                        _cooldown = AttackCooldown;
                    }
                }
            }
            else
            {
                // No targets left: only cancel combat-driven movement, keep player's commands/path
                if (pathActive && follower.Source == UnitPathFollower.PathSource.Combat) follower.Cancel();
                if (_combatSteering) _view.ClearDestination();
                _combatSteering = false;
            }
        }

        public void ApplyDamage(int dmg)
        {
            if (dmg <= 0) return;
            _currentHealth -= dmg;
            if (_currentHealth <= 0)
            {
                Destroy(gameObject);
            }
        }

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _view != null ? _view.Stats.MaxHealth : 0;

        public void SetHealth(int hp)
        {
            _currentHealth = Mathf.Clamp(hp, 1, Mathf.Max(1, MaxHealth));
        }

        // Called by player input when issuing manual movement/path commands
        public void NotifyManualMove()
        {
            _combatSteering = false;
        }

        private UnitCombat FindNearestEnemy()
        {
            UnitCombat best = null;
            float bestDist2 = float.MaxValue;
            Vector3 p = transform.position;
            foreach (var uc in All)
            {
                if (uc == null || uc == this) continue;
                if (uc.Faction == this.Faction) continue;
                float d2 = (uc.transform.position - p).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    best = uc;
                    bestDist2 = d2;
                }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, AttackRange);
        }
    }
}
