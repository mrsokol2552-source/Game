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
        private static Rect s_LastHudArea;
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
            return s_LastHudArea.Contains(p);
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
            s_LastHudArea = area;
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
