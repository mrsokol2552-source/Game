using Game.Presentation.View;
using System.Collections;
using Game.Presentation.UI;
using UnityEngine;
using Game.Presentation.Performance;
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
        private int _rmbPendingSquadId;
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
                var world = SnapToHex(ScreenToWorld(cam, mouse.position.ReadValue()));
                lastUnit = Instantiate(UnitPrefab, world, Quaternion.identity);
                var combat = lastUnit.GetComponent<UnitCombat>();
                if (combat == null) combat = lastUnit.gameObject.AddComponent<UnitCombat>();
                combat.Faction = Game.Domain.Units.Faction.Player;
                    if (lastUnit.GetComponent<UnitHpOverlay>() == null) lastUnit.gameObject.AddComponent<UnitHpOverlay>();
                    if (lastUnit.GetComponent<UnitVisualCulling>() == null) lastUnit.gameObject.AddComponent<UnitVisualCulling>();
                    // Ensure visual parity with legacy/dev spawn: set player sprite if available
                    var sr = lastUnit.GetComponent<SpriteRenderer>();
                    var root = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Bootstrap.CompositionRoot>();
                    if (sr != null)
                    {
                        // Color tint for player
                        sr.color = Color.white;
                        // Assign sprite from Bootstrap Visuals if available
                        if (root != null && root.PlayerSprite != null)
                            sr.sprite = root.PlayerSprite;
                        ApplyUnitSorting(sr, root);
                    }
                }
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    var world = SnapToHex(ScreenToWorld(cam, mouse.position.ReadValue()));
                    EnqueueRmb(world);
                }
            }
#else
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                var world = SnapToHex(ScreenToWorld(cam, UnityEngine.Input.mousePosition));
                lastUnit = Instantiate(UnitPrefab, world, Quaternion.identity);
                var combat = lastUnit.GetComponent<UnitCombat>();
                if (combat == null) combat = lastUnit.gameObject.AddComponent<UnitCombat>();
                combat.Faction = Game.Domain.Units.Faction.Player;
                if (lastUnit.GetComponent<UnitHpOverlay>() == null) lastUnit.gameObject.AddComponent<UnitHpOverlay>();
                if (lastUnit.GetComponent<UnitVisualCulling>() == null) lastUnit.gameObject.AddComponent<UnitVisualCulling>();
                var sr = lastUnit.GetComponent<SpriteRenderer>();
                var root = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Bootstrap.CompositionRoot>();
                if (sr != null)
                {
                    if (root != null && root.PlayerSprite != null) sr.sprite = root.PlayerSprite;
                    ApplyUnitSorting(sr, root);
                }
            }
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                var world = SnapToHex(ScreenToWorld(cam, UnityEngine.Input.mousePosition));
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

        private static Vector3 SnapToHex(Vector3 world)
        {
            var hex = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Pathfinding.HexPathfindingBootstrap>();
            if (hex == null) return world;
            var cell = hex.WorldToGrid(world);
            return hex.GridToWorld(cell.x, cell.y);
        }

        private static void ApplyUnitSorting(SpriteRenderer sr, Game.Presentation.Bootstrap.CompositionRoot root)
        {
            if (sr == null) return;
            if (root != null && !string.IsNullOrEmpty(root.UnitSortingLayerName) && SortingLayerExists(root.UnitSortingLayerName))
                sr.sortingLayerName = root.UnitSortingLayerName;
            sr.sortingOrder = root != null ? root.UnitSortingOrder : sr.sortingOrder;
        }

        private static bool SortingLayerExists(string name)
        {
            foreach (var l in SortingLayer.layers)
            {
                if (l.name == name) return true;
            }
            return false;
        }

        private void TrySetPath(Game.Presentation.View.UnitView unit, Vector3 worldTarget)
        {
            var pm = Game.Presentation.Pathfinding.PathManager.Ensure();
            Vector3 target = worldTarget;
            if (pm != null && pm.IsWorldOccupied(target, unit) && pm.TryFindNearestFreeWorld(target, unit, 3, out var alt))
                target = alt;

            Game.Presentation.Pathfinding.PathRequestQueue.Ensure();
            Game.Presentation.Pathfinding.PathRequestQueue.Instance.Enqueue(unit, target, allowDiag: true, smooth: true, onDone: (ok, worldPoints) =>
            {
                if (ok && worldPoints != null)
                {
                    var follower = unit.GetComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
                    if (follower == null) follower = unit.gameObject.AddComponent<Game.Presentation.Pathfinding.UnitPathFollower>();
                    follower.SetWorldPath(worldPoints, Game.Presentation.Pathfinding.UnitPathFollower.PathSource.Manual);
                    Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPoints);
                    var uc = unit.GetComponent<Game.Presentation.View.UnitCombat>();
                    if (uc != null) uc.NotifyManualMove();
                }
                else
                {
                    // If target cell is blocked, ignore the command instead of walking straight into an obstacle
                    bool allowed = true;
                    var hex = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Pathfinding.HexPathfindingBootstrap>();
                    if (hex != null && !hex.IsWalkableWorld(worldTarget))
                        allowed = false;
                    var pm2 = Game.Presentation.Pathfinding.PathManager.Ensure();
                    if (pm2 != null && pm2.IsWorldOccupied(worldTarget, unit))
                        allowed = false;
                    if (allowed)
                    {
                        unit.SetDestination(target);
                        var uc = unit.GetComponent<Game.Presentation.View.UnitCombat>();
                        if (uc != null) uc.NotifyManualMove();
                    }
                }
            });
        }

        // Brush editing removed; pathfinding is always on by default.

        private void EnqueueRmb(Vector3 world)
        {
            if (!HudController.TryGetSelectedSquadId(out _rmbPendingSquadId) && lastUnit == null)
                return;
            _rmbPendingWorld = world;
            if (_rmbApplyRoutine == null)
                _rmbApplyRoutine = StartCoroutine(ApplyRmbAfterCoalesce());
        }

        private IEnumerator ApplyRmbAfterCoalesce()
        {
            yield return new WaitForSeconds(RmbCoalesceSeconds);
            var target = _rmbPendingWorld;
            bool issued = false;
            if (_rmbPendingSquadId > 0)
                issued = TrySetPathForSquad(_rmbPendingSquadId, target);
            var unit = lastUnit;
            if (!issued && unit != null)
                TrySetPath(unit, target);
            _rmbApplyRoutine = null;
        }

        private bool TrySetPathForSquad(int squadId, Vector3 worldTarget)
        {
            bool issued = false;
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                if (uc.Faction != Game.Domain.Units.Faction.Player) continue;
                if (!uc.IsInSquad || uc.SquadId != squadId) continue;
                var unit = uc.GetComponent<UnitView>();
                if (unit == null) continue;
                TrySetPath(unit, worldTarget);
                issued = true;
            }
            return issued;
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


