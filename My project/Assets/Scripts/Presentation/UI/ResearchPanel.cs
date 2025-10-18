using System;
using System.Collections.Generic;
using Game.Application.UseCases;
using Game.Domain.Economy;
using Game.Domain.Research;
using Game.Infrastructure.Configs;
using Game.Presentation.Bootstrap;
using UnityEngine;

namespace Game.Presentation.UI
{
    public class ResearchPanel : MonoBehaviour
    {
        public Vector2 Scroll;
        public float Width = 360f;
        public float Height = 260f;

        private CompositionRoot Root;

        private void Awake()
        {
            Root = FindObjectOfType<CompositionRoot>();
        }

        private void OnGUI()
        {
            if (Root == null) Root = FindObjectOfType<CompositionRoot>();
            var area = new Rect(Screen.width - Width - 10f, 10f, Width, Height);
            HudController.AddUiRect(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Research");

            if (Root == null || Root.Game == null)
            {
                GUILayout.Label("Game not ready");
                GUILayout.EndArea();
                return;
            }

            ResearchConfig cfg = Root.TestResearch;
            if (cfg == null || cfg.Items == null || cfg.Items.Length == 0)
            {
                GUILayout.Label("No ResearchConfig assigned");
                GUILayout.EndArea();
                return;
            }

            Scroll = GUILayout.BeginScrollView(Scroll);
            for (int i = 0; i < cfg.Items.Length; i++)
            {
                var def = cfg.Items[i];
                if (string.IsNullOrEmpty(def.Id)) continue;
                var status = Root.Game.Research.GetStatus(def.Id);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{def.Id} — {status}");
                if (def.Cost != null && def.Cost.Length > 0)
                {
                    GUILayout.Label(CostToString(def.Cost));
                }

                GUILayout.BeginHorizontal();
                using (new EditorEnabledScope(status == ResearchStatus.Locked))
                {
                    if (GUILayout.Button("Start"))
                    {
                        TryStart(def);
                    }
                }
                using (new EditorEnabledScope(status == ResearchStatus.Queued))
                {
                    if (GUILayout.Button("Complete"))
                    {
                        TryComplete(def);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void TryStart(ResearchDef def)
        {
            var use = new StartResearch(Root.Game);
            IReadOnlyList<ResourceAmount> cost = def.Cost ?? Array.Empty<ResourceAmount>();
            var res = use.Execute(def.Id, cost, out var shortfall);
            switch (res)
            {
                case ResearchStartResult.Started:
                    Root.SetStatus($"Research: '{def.Id}' started (Queued)");
                    break;
                case ResearchStartResult.AlreadyQueued:
                    Root.SetStatus($"Research: '{def.Id}' already queued");
                    break;
                case ResearchStartResult.AlreadyDone:
                    Root.SetStatus($"Research: '{def.Id}' already done");
                    break;
                case ResearchStartResult.InsufficientResources:
                    string msg = $"Research: Not enough resources for '{def.Id}'";
                    if (shortfall != null)
                    {
                        foreach (var s in shortfall)
                            msg += $"\n- Need +{s.Amount} of {s.Type}";
                    }
                    Root.SetStatus(msg);
                    break;
                default:
                    Root.SetStatus("Research: invalid request");
                    break;
            }
        }

        private void TryComplete(ResearchDef def)
        {
            var use = new CompleteResearch(Root.Game);
            if (use.Execute(def.Id))
                Root.SetStatus($"Research: '{def.Id}' completed (Done)");
            else
                Root.SetStatus($"Research: '{def.Id}' not started (Locked)");
        }

        private static string CostToString(ResourceAmount[] cost)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < cost.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(cost[i].Type).Append(": ").Append(cost[i].Amount);
            }
            return sb.ToString();
        }

        private readonly struct EditorEnabledScope : IDisposable
        {
            private readonly bool prev;
            public EditorEnabledScope(bool enabled)
            {
                prev = GUI.enabled;
                GUI.enabled = enabled;
            }
            public void Dispose()
            {
                GUI.enabled = prev;
            }
        }
    }
}

