using Game.Presentation.View;
using UnityEngine;

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

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                var world = ScreenToWorld(cam, UnityEngine.Input.mousePosition);
                lastUnit = Instantiate(UnitPrefab, world, Quaternion.identity);
            }

            if (UnityEngine.Input.GetMouseButtonDown(1) && lastUnit != null)
            {
                var world = ScreenToWorld(cam, UnityEngine.Input.mousePosition);
                lastUnit.SetDestination(world);
            }
        }

        private static Vector3 ScreenToWorld(Camera cam, Vector3 mouse)
        {
            var world = cam.ScreenToWorldPoint(mouse);
            world.z = 0f; // 2D plane
            return world;
        }
    }
}

