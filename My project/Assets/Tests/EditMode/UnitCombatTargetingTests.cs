using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Domain.Units;
using Game.Presentation.View;
using Game.Presentation.Performance;

namespace Tests.EditMode
{
    public class UnitCombatTargetingTests
    {
        private MethodInfo _resolveTarget;
        private FieldInfo _targetRefreshTimer;
        private FieldInfo _jobTimer;
        private FieldInfo _forcedTimer;

        [SetUp]
        public void SetUp()
        {
            CleanupAll();
            _resolveTarget = typeof(UnitCombat).GetMethod("ResolveTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            _targetRefreshTimer = typeof(UnitCombat).GetField("_targetRefreshTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            _jobTimer = typeof(UnitCombat).GetField("_jobNearestTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            _forcedTimer = typeof(UnitCombat).GetField("_forcedTargetTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [TearDown]
        public void TearDown()
        {
            CleanupAll();
        }

        [Test]
        public void JobTarget_TakesPriority_OverForced()
        {
            var self = Spawn(Faction.Player, "self");
            var forced = Spawn(Faction.Enemy, "forced");
            var job = Spawn(Faction.Enemy, "job");

            EnsureSchedulerEnabled();
            SetRefreshNow(self);

            // both job and forced set; job should win
            InvokeSetJobNearest(self, job);
            self.AssignSquadTarget(forced, 2f);

            var target = InvokeResolve(self);
            Assert.AreSame(job, target);
        }

        [Test]
        public void ForcedTarget_Used_WhenJobExpired()
        {
            var self = Spawn(Faction.Player, "self");
            var forced = Spawn(Faction.Enemy, "forced");
            var job = Spawn(Faction.Enemy, "job");

            EnsureSchedulerEnabled();
            SetRefreshNow(self);

            InvokeSetJobNearest(self, job);
            // expire job TTL
            _jobTimer.SetValue(self, 0f);
            self.AssignSquadTarget(forced, 2f);

            var target = InvokeResolve(self);
            Assert.AreSame(forced, target);
        }

        [Test]
        public void LocalSearch_Used_WhenNoJobOrForced()
        {
            var self = Spawn(Faction.Player, "self");
            var near = Spawn(Faction.Enemy, "near");
            var far = Spawn(Faction.Enemy, "far");

            near.transform.position = Vector3.right * 2f;
            far.transform.position = Vector3.right * 10f;

            EnsureSchedulerEnabled();
            SetRefreshNow(self);
            // clear any timers
            _jobTimer.SetValue(self, 0f);
            _forcedTimer.SetValue(self, 0f);

            var target = InvokeResolve(self);
            Assert.AreSame(near, target);
        }

        private static UnitCombat Spawn(Faction faction, string name)
        {
            var go = new GameObject(name);
            go.AddComponent<SpriteRenderer>();
            var uc = go.AddComponent<UnitCombat>();
            uc.Faction = faction;
            return uc;
        }

        private UnitCombat InvokeResolve(UnitCombat uc)
        {
            return (UnitCombat)_resolveTarget.Invoke(uc, null);
        }

        private void InvokeSetJobNearest(UnitCombat uc, UnitCombat target)
        {
            var method = typeof(UnitCombat).GetMethod("SetJobNearest", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method.Invoke(uc, new object[] { target });
        }

        private void SetRefreshNow(UnitCombat uc)
        {
            _targetRefreshTimer.SetValue(uc, 0f);
        }

        private void EnsureSchedulerEnabled()
        {
            if (UnitCombatJobScheduler.Instance != null)
                UnitCombatJobScheduler.Instance.Disabled = false;
        }

        private void CleanupAll()
        {
            if (UnitCombatJobScheduler.Instance != null)
            {
                Object.DestroyImmediate(UnitCombatJobScheduler.Instance.gameObject);
            }

            foreach (var uc in new System.Collections.Generic.List<UnitCombat>(UnitCombat.All))
            {
                if (uc != null)
                    Object.DestroyImmediate(uc.gameObject);
            }
            UnitCombat.All.Clear();
        }
    }
}
