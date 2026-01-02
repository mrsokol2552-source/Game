using System;
using System.Collections.Generic;
using Game.Domain.Economy;
using Game.Presentation.Bootstrap;
using Game.Presentation.View;
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
        private Vector2 _squadScroll;
        private readonly Dictionary<int, SquadUiInfo> _squadInfo = new Dictionary<int, SquadUiInfo>(32);
        private readonly List<int> _squadIds = new List<int>(32);
        public static int SelectedSquadId { get; private set; }

        private struct SquadUiInfo
        {
            public int Count;
            public UnitCombat.SquadMode Mode;
            public bool Mixed;
        }

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
            // For prototype we rely on OnGUI to repaint each frame.
        }

        private void OnGUI()
        {
            if (CompositionRoot.Game == null) return;

            var area = new Rect(10, 10, 300, 230);
            AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"Units: {UnitCombat.All.Count}");
            GUILayout.Label("Resources:");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int v = CompositionRoot.Game.Economy.GetStock(type);
                GUILayout.Label($"- {type}: {v}");
            }

            // Controls first so they stay visible
            if (GUILayout.Button("Save"))
            {
                UnityEngine.Object.FindAnyObjectByType<CompositionRoot>()?.Save();
            }
            if (GUILayout.Button("Load"))
            {
                UnityEngine.Object.FindAnyObjectByType<CompositionRoot>()?.Load();
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

            // Scrollable status area so long messages don't push controls out
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

        private void DrawPlayerSquads()
        {
            BuildSquadSummary();
            float height = 70f;
            var area = new Rect(10, Screen.height - height - 10f, Screen.width - 20f, height);
            AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            string header = SelectedSquadId > 0 ? $"Player squads (selected: S{SelectedSquadId})" : "Player squads";
            GUILayout.Label(header);
            _squadScroll = GUILayout.BeginScrollView(_squadScroll, GUILayout.Height(36f));
            GUILayout.BeginHorizontal();

            if (_squadIds.Count == 0)
            {
                GUILayout.Label("No squads");
            }
            else
            {
                for (int i = 0; i < _squadIds.Count; i++)
                {
                    int id = _squadIds[i];
                    if (!_squadInfo.TryGetValue(id, out var info)) continue;
                    string modeLabel = info.Mixed ? "Mixed" : info.Mode.ToString();
                    bool isSelected = id == SelectedSquadId;
                    string label = isSelected ? $"▶ S{id} ({info.Count}) {modeLabel}" : $"S{id} ({info.Count}) {modeLabel}";
                    if (GUILayout.Button(label))
                        SelectedSquadId = isSelected ? 0 : id;
                }
            }

            if (_squadIds.Count > 0)
            {
                if (GUILayout.Button("Clear"))
                    SelectedSquadId = 0;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void BuildSquadSummary()
        {
            _squadInfo.Clear();
            _squadIds.Clear();

            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                if (uc.Faction != Game.Domain.Units.Faction.Player) continue;
                if (!uc.IsInSquad) continue;

                int id = uc.SquadId;
                if (!_squadInfo.TryGetValue(id, out var info))
                {
                    info = new SquadUiInfo
                    {
                        Count = 1,
                        Mode = uc.CurrentSquadMode,
                        Mixed = false
                    };
                    _squadInfo.Add(id, info);
                    _squadIds.Add(id);
                }
                else
                {
                    info.Count++;
                    if (info.Mode != uc.CurrentSquadMode)
                        info.Mixed = true;
                    _squadInfo[id] = info;
                }
            }

            if (_squadIds.Count > 1)
                _squadIds.Sort();

            if (SelectedSquadId > 0 && !_squadInfo.ContainsKey(SelectedSquadId))
                SelectedSquadId = 0;
        }
    }
}



