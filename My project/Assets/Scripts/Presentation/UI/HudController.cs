using System;
using System.Collections.Generic;
using Game.Domain.Economy;
using Game.Presentation.Bootstrap;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/*
@file: My project/Assets/Scripts/Presentation/UI/HudController.cs
@module: presentation.ui.hud
@purpose: Main HUD shell for resources, status text, panel toggles, and pointer-over-UI blocking.
@entry: SCRIPTS-PRESENTATION-UI-HUDCONTROLLER
@api: HudController MonoBehaviour
@deps: CompositionRoot, ActionsPanel, ResearchPanel
@data: IMGUI rect registry, status scroll, squad panel state
@perf: IMGUI-only; low cost relative to world systems
@thread: main thread only
@tests: manual HUD interaction and pointer-block verification
@config: none beyond scene presence
@assets: scene HUD root only
@notes: squad summary/selection strip is isolated in HudController.Squads.cs
*/

// [CODE-ID: SCRIPTS-PRESENTATION-UI-HUDCONTROLLER]
// Logical block: Scripts/Presentation/UI/HudController.

namespace Game.Presentation.UI
{
    public partial class HudController : MonoBehaviour
    {
        private static readonly List<Rect> s_UiAreas = new List<Rect>(4);
        private static int s_LastFrame = -1;

        private Vector2 _statusScroll;
        private Vector2 _squadScroll;
        private readonly Dictionary<int, SquadUiInfo> _squadInfo = new Dictionary<int, SquadUiInfo>(32);
        private readonly List<int> _squadIds = new List<int>(32);

        public static int SelectedSquadId { get; private set; }

        public static bool IsPointerOverHud()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            Vector2 p = mouse.position.ReadValue();
#else
            Vector2 p = UnityEngine.Input.mousePosition;
#endif
            p.y = Screen.height - p.y;
            for (int i = 0; i < s_UiAreas.Count; i++)
            {
                if (s_UiAreas[i].Contains(p)) return true;
            }
            return false;
        }

        public static void AddUiRect(Rect rect)
        {
            if (s_LastFrame != Time.frameCount)
            {
                s_UiAreas.Clear();
                s_LastFrame = Time.frameCount;
            }
            s_UiAreas.Add(rect);
        }

        public static bool TryGetSelectedSquadId(out int squadId)
        {
            squadId = SelectedSquadId;
            return squadId > 0;
        }

        public static void ClearSelectedSquad()
        {
            SelectedSquadId = 0;
        }

        private void OnEnable()
        {
            Subscribe(true);
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void Subscribe(bool add)
        {
            if (CompositionRoot.Game == null) return;
            if (add)
                CompositionRoot.Game.Economy.OnStockChanged += OnStockChanged;
            else
                CompositionRoot.Game.Economy.OnStockChanged -= OnStockChanged;
        }

        private void OnStockChanged(ResourceType type, int value)
        {
        }

        private void OnGUI()
        {
            if (CompositionRoot.Game == null) return;

            var area = new Rect(10, 10, 300, 230);
            AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"Units: {Game.Presentation.View.UnitCombat.All.Count}");
            GUILayout.Label("Resources:");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int v = CompositionRoot.Game.Economy.GetStock(type);
                GUILayout.Label($"- {type}: {v}");
            }

            if (GUILayout.Button("Save"))
                UnityEngine.Object.FindAnyObjectByType<CompositionRoot>()?.Save();
            if (GUILayout.Button("Load"))
                UnityEngine.Object.FindAnyObjectByType<CompositionRoot>()?.Load();

            GUILayout.Space(4);
            var label = ResearchPanel.Visible ? "Hide Research" : "Research";
            if (GUILayout.Button(label))
                ResearchPanel.Visible = !ResearchPanel.Visible;

            var devLabel = ActionsPanel.Visible ? "Hide Dev" : "Dev";
            if (GUILayout.Button(devLabel))
            {
                if (!ActionsPanel.Visible)
                {
                    var ap = UnityEngine.Object.FindAnyObjectByType<ActionsPanel>();
                    if (ap == null)
                    {
                        var go = new GameObject("ActionsPanel (Auto)");
                        go.AddComponent<ActionsPanel>();
                    }
                    ActionsPanel.Visible = true;
                }
                else
                {
                    ActionsPanel.Visible = false;
                }
            }

            var root = UnityEngine.Object.FindAnyObjectByType<CompositionRoot>();
            if (root != null && !string.IsNullOrEmpty(root.LastStatusMessage))
            {
                GUILayout.Space(6);
                var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
                _statusScroll = GUILayout.BeginScrollView(_statusScroll, GUILayout.Height(100));
                GUILayout.Label(root.LastStatusMessage, style);
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();

            DrawPlayerSquads();
        }
    }
}
