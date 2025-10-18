using System;
using Game.Domain.Economy;
using Game.Presentation.Bootstrap;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation.UI
{
    public class HudController : MonoBehaviour
    {
        private static System.Collections.Generic.List<Rect> s_UiAreas = new System.Collections.Generic.List<Rect>(4);
        private static int s_LastFrame = -1;
        private Vector2 _statusScroll;

        public static bool IsPointerOverHud()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            Vector2 p = mouse.position.ReadValue();
#else
            Vector2 p = UnityEngine.Input.mousePosition;
#endif
            // Convert to IMGUI coordinates (top-left origin)
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
            // For prototype we rely on OnGUI to repaint each frame.
        }

        private void OnGUI()
        {
            if (CompositionRoot.Game == null) return;

            var area = new Rect(10, 10, 300, 230);
            AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Resources:");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int v = CompositionRoot.Game.Economy.GetStock(type);
                GUILayout.Label($"- {type}: {v}");
            }

            // Controls first so they stay visible
            if (GUILayout.Button("Save"))
            {
                FindObjectOfType<CompositionRoot>()?.Save();
            }
            if (GUILayout.Button("Load"))
            {
                FindObjectOfType<CompositionRoot>()?.Load();
            }

            GUILayout.Space(4);
            var label = ResearchPanel.Visible ? "Hide Research" : "Research";
            if (GUILayout.Button(label))
            {
                ResearchPanel.Visible = !ResearchPanel.Visible;
            }

            var devLabel = ActionsPanel.Visible ? "Hide Dev" : "Dev";
            if (GUILayout.Button(devLabel))
            {
                if (!ActionsPanel.Visible)
                {
                    // Ensure panel exists in scene before showing
                    var ap = FindObjectOfType<ActionsPanel>();
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

            // Scrollable status area so long messages don't push controls out
            var root = FindObjectOfType<CompositionRoot>();
            if (root != null && !string.IsNullOrEmpty(root.LastStatusMessage))
            {
                GUILayout.Space(6);
                var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
                _statusScroll = GUILayout.BeginScrollView(_statusScroll, GUILayout.Height(100));
                GUILayout.Label(root.LastStatusMessage, style);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }
    }
}

