using UnityEngine;
using RtsGame.Application.Services;
using RtsGame.Domain.Economy;

namespace RtsGame.Presentation.UI
{
    public class HudController : MonoBehaviour
    {
        private GameStateService _service;

        public void Bind(GameStateService service)
        {
            _service = service;
        }

        private void OnGUI()
        {
            if (_service == null) return;
            GUILayout.BeginArea(new Rect(10, 10, 300, 120), GUI.skin.box);
            GUILayout.Label($"Labor: {_service.Economy.Get(ResourceType.LaborHours)}");
            GUILayout.Label($"Power: {_service.Economy.Get(ResourceType.Power)}");
            GUILayout.Label($"Materials: {_service.Economy.Get(ResourceType.Materials)}");
            GUILayout.Label($"Food: {_service.Economy.Get(ResourceType.Food)}");
            GUILayout.EndArea();
        }
    }
}

