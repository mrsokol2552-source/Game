using Game.Presentation.View;
using UnityEngine;

/*
@file: My project/Assets/Scripts/Presentation/UI/HudController.Squads.cs
@module: presentation.ui.hud
@purpose: Squad-summary aggregation and bottom-strip squad selection UI extracted from HudController.
@entry: SCRIPTS-PRESENTATION-UI-HUDCONTROLLER-SQUADS
@api: HudController partial squad UI helpers
@deps: UnitCombat
@data: per-squad count/mode summary and selected squad state
@perf: lightweight IMGUI aggregation over UnitCombat.All each frame
@thread: main thread only
@tests: manual squad selection UI verification
@config: none
@assets: scene HUD root only
@notes: isolated so HUD shell stays focused on generic status/resource panels
*/

// [CODE-ID: SCRIPTS-PRESENTATION-UI-HUDCONTROLLER-SQUADS]
// Logical block: Scripts/Presentation/UI/HudController.Squads.

namespace Game.Presentation.UI
{
    public partial class HudController
    {
        private struct SquadUiInfo
        {
            public int Count;
            public UnitCombat.SquadMode Mode;
            public bool Mixed;
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
                    string label = isSelected ? $"[S{id}] ({info.Count}) {modeLabel}" : $"S{id} ({info.Count}) {modeLabel}";
                    if (GUILayout.Button(label))
                        SelectedSquadId = isSelected ? 0 : id;
                }
            }

            if (_squadIds.Count > 0 && GUILayout.Button("Clear"))
                SelectedSquadId = 0;

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
