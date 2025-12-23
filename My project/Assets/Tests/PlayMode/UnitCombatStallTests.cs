using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Presentation.View;
using Game.Domain.Units;

namespace Tests.PlayMode
{
    public class UnitCombatStallTests
    {
        [SetUp]
        public void SetUp()
        {
            UnitCombat.DisableCombat = false;
            UnitCombat.All.Clear();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            UnitCombat.DisableCombat = false;
            UnitCombat.All.Clear();
        }

        [UnityTest]
        public IEnumerator RepeatsAttacksWhenAlreadyInRange()
        {
            var attacker = SpawnUnit("attacker", Vector3.zero, attackCooldown: 0.2f, attackDamage: 1, Game.Domain.Units.Faction.Player);
            var target = SpawnUnit("target", new Vector3(0.5f, 0f, 0f), attackCooldown: 1f, attackDamage: 0, Game.Domain.Units.Faction.Enemy);
            target.SetHealth(20);
            int initial = target.CurrentHealth;

            // Let combat tick
            yield return new WaitForSeconds(1.0f);

            Assert.Less(target.CurrentHealth, initial - 1, "Target should have taken multiple hits when in range.");

            Cleanup(attacker, target);
        }

        [UnityTest]
        public IEnumerator ClosesDistanceAndAttacks()
        {
            var attacker = SpawnUnit("attacker", Vector3.zero, attackCooldown: 0.2f, attackDamage: 1, Game.Domain.Units.Faction.Player);
            var target = SpawnUnit("target", new Vector3(2.0f, 0f, 0f), attackCooldown: 1f, attackDamage: 0, Game.Domain.Units.Faction.Enemy);
            target.SetHealth(20);
            int initial = target.CurrentHealth;

            // Allow time to move into range and attack
            yield return new WaitForSeconds(5.0f);

            Assert.Less(target.CurrentHealth, initial, "Attacker should close distance and deal damage.");

            Cleanup(attacker, target);
        }

        private static UnitCombat SpawnUnit(string name, Vector3 pos, float attackCooldown, int attackDamage, Game.Domain.Units.Faction faction)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<UnitView>();
            view.Stats = new UnitStats { MaxHealth = 30, Speed = 3.5f };
            var combat = go.AddComponent<UnitCombat>();
            combat.AttackCooldown = attackCooldown;
            combat.AttackDamage = attackDamage;
            combat.Faction = faction;
            combat.RepathInterval = 0.15f;
            combat.RepathIntervalFar = 0.3f;
            combat.RepathIntervalVeryFar = 0.6f;
            combat.StallRepathSeconds = 0.2f;
            return combat;
        }

        private static void Cleanup(params UnitCombat[] units)
        {
            foreach (var uc in units)
            {
                if (uc != null)
                    Object.Destroy(uc.gameObject);
            }
        }
    }
}
