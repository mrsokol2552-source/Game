using Game.Presentation.View;
using System.Collections;
using UnityEngine;
using Game.Presentation.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation.Input
{
    public class UnitSpawnerCommander : MonoBehaviour
    {
        public UnitView UnitPrefab;
        private UnitView lastUnit;
        public float RmbCoalesceSeconds = 0.08f;
        private Coroutine _rmbApplyRoutine;
        private Vector3 _rmbPendingWorld;
        private float _lastRmbTime;
        private Vector3 _lastRmbWorld;

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || UnitPrefab == null) return;
            if (HudController.IsPointerOverHud()) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    var world = ScreenToWorld(cam, mouse.position.ReadValue());
                    lastUnit = Instantiate(UnitPrefab, world, Quaternion.identity);
                    var combat = lastUnit.GetComponent<UnitCombat>();
                    if (combat == null) combat = lastUnit.gameObject.AddComponent<UnitCombat>();
                    combat.Faction = Game.Domain.Units.Faction.Player;
                    if (lastUnit.GetComponent<UnitHpOverlay>() == null) lastUnit.gameObject.AddComponent<UnitHpOverlay>();
                    // Ensure visual parity with legacy/dev spawn: set player sprite if available
                    var sr = lastUnit.GetComponent<SpriteRenderer>();
                    var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                    if (sr != null)
                    {
                        // Color tint for player
                        sr.color = Color.white;
                        // Assign sprite from Bootstrap Visuals if available
                        if (root != null && root.PlayerSprite != null)
                            sr.sprite = root.PlayerSprite;
                    }
                }
                if (mouse.rightButton.wasPressedThisFrame && lastUnit != null)
                {
                    var world = ScreenToWorld(cam, mouse.position.ReadValue());
                    EnqueueRmb(world);
                }
            }
#else
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                var world = ScreenToWorld(cam, UnityEngine.Input.mousePosition);
                lastUnit = Instantiate(UnitPrefab, world, Quaternion.identity);
                var combat = lastUnit.GetComponent<UnitCombat>();
                if (combat == null) combat = lastUnit.gameObject.AddComponent<UnitCombat>();
                combat.Faction = Game.Domain.Units.Faction.Player;
                if (lastUnit.GetComponent<UnitHpOverlay>() == null) lastUnit.gameObject.AddComponent<UnitHpOverlay>();
                var sr = lastUnit.GetComponent<SpriteRenderer>();
                var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                if (sr != null && root != null && root.PlayerSprite != null) sr.sprite = root.PlayerSprite;
            }
            if (UnityEngine.Input.GetMouseButtonDown(1) && lastUnit != null)
            {
                var world = ScreenToWorld(cam, UnityEngine.Input.mousePosition);
                EnqueueRmb(world);
            }
#endif
        }

        private static Vector3 ScreenToWorld(Camera cam, Vector3 mouse)
        {
            var world = cam.ScreenToWorldPoint(mouse);
            world.z = 0f; // 2D plane
            return world;
        }

        private void TrySetPath(Game.Presentation.View.UnitView unit, Vector3 worldTarget)
        {
            var pm = Game.Presentation.Pathfinding.PathManager.Ensure();
            if (pm != null && pm.BuildPath(unit, worldTarget,
                    allowDiag: true,
                    smooth: true,
                    autoFit: false,
                    out var worldPoints))
            {
                var follower = unit.GetComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
                if (follower == null) follower = unit.gameObject.AddComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
                else follower.Cancel();
                follower.SetWorldPath(worldPoints, Game.Presentation.Pathfinding.UnitPathFollower.PathSource.Manual);
                var uc = unit.GetComponent<Game.Presentation.View.UnitCombat>();
                if (uc != null) uc.NotifyManualMove();
            }
            else
            {
                unit.SetDestination(worldTarget);
                var uc = unit.GetComponent<Game.Presentation.View.UnitCombat>();
                if (uc != null) uc.NotifyManualMove();
            }
        }

        // Brush editing removed; pathfinding is always on by default.

        private void EnqueueRmb(Vector3 world)
        {
            _rmbPendingWorld = world;
            if (_rmbApplyRoutine == null)
                _rmbApplyRoutine = StartCoroutine(ApplyRmbAfterCoalesce());
        }

        private IEnumerator ApplyRmbAfterCoalesce()
        {
            yield return new WaitForSeconds(RmbCoalesceSeconds);
            var target = _rmbPendingWorld;
            var unit = lastUnit;
            if (unit != null)
            {
                TrySetPath(unit, target);
            }
            _rmbApplyRoutine = null;
        }

        private bool ShouldIgnoreRmb(Vector3 world)
        {
            if (Time.time - _lastRmbTime < 0.08f && (world - _lastRmbWorld).sqrMagnitude < 0.0004f)
                return true;
            _lastRmbTime = Time.time;
            _lastRmbWorld = world;
            return false;
        }

        public void SetLastUnit(UnitView unit)
        {
            lastUnit = unit;
        }
    }
}
