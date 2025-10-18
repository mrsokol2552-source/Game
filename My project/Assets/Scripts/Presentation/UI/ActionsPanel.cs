using System.IO;
using Game.Domain.Economy;
using Game.Domain.Units;
using Game.Presentation.Bootstrap;
using Game.Presentation.Input;
using Game.Presentation.View;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation.UI
{
    public class ActionsPanel : MonoBehaviour
    {
        public static bool Visible = true;
        public float Width = 220f;
        public float Height = 220f;

        private CompositionRoot root;

        private void Awake()
        {
            root = FindObjectOfType<CompositionRoot>();
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (root == null) root = FindObjectOfType<CompositionRoot>();

            var area = new Rect(Screen.width - Width - 10f, Screen.height - Height - 10f, Width, Height);
            HudController.AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Actions");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Unit"))
            {
                SpawnUnit(Faction.Player);
            }
            if (GUILayout.Button("Spawn Enemy"))
            {
                SpawnUnit(Faction.Enemy);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 Materials"))
                CompositionRoot.Game?.Economy.Add(ResourceType.Materials, 10);
            if (GUILayout.Button("+5 Food"))
                CompositionRoot.Game?.Economy.Add(ResourceType.Food, 5);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (GUILayout.Button("Attempt Build"))
                root?.AttemptPlaceTestBuilding();

            if (GUILayout.Button(ResearchPanel.Visible ? "Hide Research" : "Show Research"))
                ResearchPanel.Visible = !ResearchPanel.Visible;

            GUILayout.Space(6);
            if (GUILayout.Button("Clear Save"))
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "save.json");
                if (File.Exists(path)) File.Delete(path);
                root?.SetStatus("Save cleared");
            }

            GUILayout.EndArea();
        }

        private void SpawnUnit(Faction faction)
        {
            var cam = Camera.main; if (cam == null) return;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current; if (mouse == null) return;
            Vector3 pos = mouse.position.ReadValue();
#else
            Vector3 pos = UnityEngine.Input.mousePosition;
#endif
            var world = cam.ScreenToWorldPoint(pos); world.z = 0f;

            var spawner = FindObjectOfType<UnitSpawnerCommander>();
            UnitView prefab = spawner != null ? spawner.UnitPrefab : null;
            if (prefab == null && root != null) prefab = root.DefaultUnitPrefab;
            if (prefab == null)
            {
                root?.SetStatus("No Unit prefab to spawn");
                return;
            }

            var u = Instantiate(prefab, world, Quaternion.identity);
            var combat = u.GetComponent<UnitCombat>();
            if (combat == null) combat = u.gameObject.AddComponent<UnitCombat>();
            combat.Faction = faction;

            var sr = u.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = (faction == Faction.Enemy ? Color.red : Color.white);
        }
    }
}
