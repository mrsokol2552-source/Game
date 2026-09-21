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

/*
@file: My project/Assets/Scripts/Presentation/UI/ActionsPanel.cs
@module: presentation.ui.actions
@purpose: Main developer actions panel shell for spawning, resource cheats, build/research toggles, and save utilities.
@entry: SCRIPTS-PRESENTATION-UI-ACTIONSPANEL
@api: ActionsPanel MonoBehaviour
@deps: CompositionRoot, UnitSpawnerCommander, UnitView, UnitCombat, ResearchPanel, HudController
@data: panel visibility, scroll position, panel dimensions
@perf: IMGUI-only; lightweight when hidden
@thread: main thread only
@tests: manual dev-panel interaction and save/load smoke checks
@config: Visible, Width, Height
@assets: scene HUD/dev panel integration
@notes: self-test flow is isolated in ActionsPanel.SelfTest.cs so the shell stays focused on live dev actions
*/

// [CODE-ID: SCRIPTS-PRESENTATION-UI-ACTIONSPANEL]
// Logical block: Scripts/Presentation/UI/ActionsPanel.

namespace Game.Presentation.UI
{
    public partial class ActionsPanel : MonoBehaviour
    {
        public static bool Visible = false;
        public float Width = 220f;
        public float Height = 220f;

        private Vector2 _scroll;
        private CompositionRoot root;
        private bool _selfTestRunning = false;

        private void Awake()
        {
            root = UnityEngine.Object.FindAnyObjectByType<CompositionRoot>();
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (root == null) root = UnityEngine.Object.FindAnyObjectByType<CompositionRoot>();

            var area = new Rect(Screen.width - Width - 10f, Screen.height - Height - 10f, Width, Height);
            HudController.AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Actions");
            _scroll = GUILayout.BeginScrollView(_scroll, false, true);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Unit"))
                SpawnUnit(Faction.Player);
            if (GUILayout.Button("Spawn Enemy"))
                SpawnUnit(Faction.Enemy);
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

            GUILayout.Space(6);
            if (GUILayout.Button("Self-Test Save/Load") && !_selfTestRunning)
                StartCoroutine(SelfTestRoutine());

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void SpawnUnit(Faction faction)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var world = RandomWorldPointInView(cam, 0.12f);

            var spawner = UnityEngine.Object.FindAnyObjectByType<UnitSpawnerCommander>();
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
            if (u.GetComponent<UnitHpOverlay>() == null) u.gameObject.AddComponent<UnitHpOverlay>();

            var sr = u.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = faction == Faction.Enemy ? Color.red : Color.white;
                if (root != null)
                {
                    if (faction == Faction.Enemy && root.EnemySprite != null)
                        sr.sprite = root.EnemySprite;
                    else if (faction == Faction.Player && root.PlayerSprite != null)
                        sr.sprite = root.PlayerSprite;
                }
            }
        }

        private static Vector3 RandomWorldPointInView(Camera cam, float viewportMargin)
        {
            float m = Mathf.Clamp01(viewportMargin);
            float vx = UnityEngine.Random.Range(m, 1f - m);
            float vy = UnityEngine.Random.Range(m, 1f - m);

            Vector3 world;
            if (cam.orthographic)
            {
                world = cam.ViewportToWorldPoint(new Vector3(vx, vy, 0f));
            }
            else
            {
                var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
                if (Mathf.Abs(ray.direction.z) > 1e-4f)
                {
                    float t = -ray.origin.z / ray.direction.z;
                    if (t < cam.nearClipPlane) t = cam.nearClipPlane;
                    world = ray.origin + ray.direction * t;
                }
                else
                {
                    world = cam.ViewportToWorldPoint(new Vector3(vx, vy, cam.nearClipPlane));
                }
            }

            world.z = 0f;
            return world;
        }
    }
}
