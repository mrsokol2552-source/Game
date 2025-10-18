using System.Collections.Generic;
using Game.Domain.Units;
using UnityEngine;

namespace Game.Presentation.View
{
    [RequireComponent(typeof(UnitView))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class UnitCombat : MonoBehaviour
    {
        public static readonly HashSet<UnitCombat> All = new HashSet<UnitCombat>();

        [Header("Combat")]
        public Faction Faction = Faction.Player;
        public float AttackRange = 1.5f;
        public int AttackDamage = 10;
        public float AttackCooldown = 0.75f;

        private float _cooldown;
        private int _currentHealth;
        private UnitView _view;

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
            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            var target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 tp = target.transform.position;
                Vector3 mp = transform.position;
                float dist = (tp - mp).magnitude;
                float stopDist = Mathf.Max(AttackRange * 0.9f, 0.1f);

                if (dist > AttackRange)
                {
                    // Move to a point at stopDist from the target, to avoid overshooting and passing through.
                    Vector3 dir = (mp - tp);
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
                    Vector3 desired = tp + dir.normalized * stopDist;
                    _view.SetDestination(desired);
                }
                else
                {
                    // In range: hold position and attack on cooldown
                    _view.ClearDestination();
                    if (_cooldown <= 0f)
                    {
                        target.ApplyDamage(AttackDamage);
                        _cooldown = AttackCooldown;
                    }
                }
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
