using UnityEngine;

namespace RtsGame.Presentation.Input
{
    public class InputController : MonoBehaviour
    {
        public KeyCode AddMaterialsKey = KeyCode.M;

        private RtsGame.Application.Services.GameStateService _service;

        public void Bind(RtsGame.Application.Services.GameStateService service)
        {
            _service = service;
        }

        private void Update()
        {
            if (_service == null) return;
            if (UnityEngine.Input.GetKeyDown(AddMaterialsKey))
            {
                _service.Economy.Add(RtsGame.Domain.Economy.ResourceType.Materials, 10);
            }
        }
    }
}

