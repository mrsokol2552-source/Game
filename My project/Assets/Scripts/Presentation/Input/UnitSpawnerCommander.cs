using Game.Presentation.View;
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
                }
                if (mouse.rightButton.wasPressedThisFrame && lastUnit != null)
                {
                    var world = ScreenToWorld(cam, mouse.position.ReadValue());
                    lastUnit.SetDestination(world);
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
            }
            if (UnityEngine.Input.GetMouseButtonDown(1) && lastUnit != null)
            {
                var world = ScreenToWorld(cam, UnityEngine.Input.mousePosition);
                lastUnit.SetDestination(world);
            }
#endif
        }

        private static Vector3 ScreenToWorld(Camera cam, Vector3 mouse)
        {
            var world = cam.ScreenToWorldPoint(mouse);
            world.z = 0f; // 2D plane
            return world;
        }

        public void SetLastUnit(UnitView unit)
        {
            lastUnit = unit;
        }
    }
}
