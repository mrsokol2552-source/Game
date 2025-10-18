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
                float dist2 = (target.transform.position - transform.position).sqrMagnitude;
                float range2 = AttackRange * AttackRange;
                if (dist2 > range2)
                {
                    // Chase into range
                    _view.SetDestination(target.transform.position);
                }
                else if (_cooldown <= 0f)
                {
                    target.ApplyDamage(AttackDamage);
                    _cooldown = AttackCooldown;
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
