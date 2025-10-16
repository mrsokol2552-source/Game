using System;
using Game.Domain.Economy;
using Game.Presentation.Bootstrap;
using UnityEngine;

namespace Game.Presentation.UI
{
    public class HudController : MonoBehaviour
    {
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

            GUILayout.BeginArea(new Rect(10, 10, 300, 200), GUI.skin.box);
            GUILayout.Label("Resources:");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int v = CompositionRoot.Game.Economy.GetStock(type);
                GUILayout.Label($"- {type}: {v}");
            }

            if (GUILayout.Button("Save"))
            {
                FindObjectOfType<CompositionRoot>()?.Save();
            }
            if (GUILayout.Button("Load"))
            {
                FindObjectOfType<CompositionRoot>()?.Load();
            }
            GUILayout.EndArea();
        }
    }
}

