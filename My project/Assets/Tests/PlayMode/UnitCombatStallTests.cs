using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Presentation.View;
using Game.Domain.Units;

// [CODE-ID: TESTS-PLAYMODE-UNITCOMBATSTALLTESTS]
// Logical block: Tests/PlayMode/UnitCombatStallTests.

namespace Tests.PlayMode
{
    [Category("Gate")]
    public class UnitCombatStallTests
    {
        [SetUp]
        public void SetUp()
        {
            CleanupAll();
            UnitCombat.DisableCombat = false;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            UnitCombat.DisableCombat = false;
            Time.timeScale = 1f;
            CleanupAll();
        }

        [UnityTest]
        public IEnumerator RepeatsAttacksWhenAlreadyInRange()
        {
            var attacker = SpawnUnit("attacker", Vector3.zero, attackCooldown: 0.2f, attackDamage: 1, Game.Domain.Units.Faction.Player);
            var target = SpawnUnit("target", new Vector3(0.5f, 0f, 0f), attackCooldown: 1f, attackDamage: 0, Game.Domain.Units.Faction.Enemy);
            target.SetHealth(20);
            int initial = target.CurrentHealth;

            yield return WaitUntilHealthAtOrBelow(target, initial - 2, 1.0f);

            Assert.Less(target.CurrentHealth, initial - 1, "Target should have taken multiple hits when in range.");
        }

        [UnityTest]
        public IEnumerator ClosesDistanceAndAttacks()
        {
            var attacker = SpawnUnit("attacker", Vector3.zero, attackCooldown: 0.2f, attackDamage: 1, Game.Domain.Units.Faction.Player);
            var target = SpawnUnit("target", new Vector3(2.0f, 0f, 0f), attackCooldown: 1f, attackDamage: 0, Game.Domain.Units.Faction.Enemy);
            target.SetHealth(20);
            int initial = target.CurrentHealth;

            yield return WaitUntilHealthBelow(target, initial, 5.0f);

            Assert.Less(target.CurrentHealth, initial, "Attacker should close distance and deal damage.");
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

        private static void CleanupAll()
        {
            foreach (var uc in new System.Collections.Generic.List<UnitCombat>(UnitCombat.All))
            {
                if (uc != null)
                    Object.DestroyImmediate(uc.gameObject);
            }
            UnitCombat.All.Clear();
        }

        private static IEnumerator WaitUntilHealthBelow(UnitCombat target, int threshold, float timeoutSeconds)
        {
            yield return WaitUntilHealthMatches(target, health => health < threshold, timeoutSeconds);
        }

        private static IEnumerator WaitUntilHealthAtOrBelow(UnitCombat target, int threshold, float timeoutSeconds)
        {
            yield return WaitUntilHealthMatches(target, health => health <= threshold, timeoutSeconds);
        }

        private static IEnumerator WaitUntilHealthMatches(UnitCombat target, System.Func<int, bool> predicate, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (target != null && !predicate(target.CurrentHealth) && Time.time < deadline)
                yield return null;
        }
    }
}
