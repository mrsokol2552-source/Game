using Game.Domain.Economy;
using Game.Presentation.Bootstrap;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation.Input
{
    public class InputController : MonoBehaviour
    {
        private void Update()
        {
            if (CompositionRoot.Game == null) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.M].wasPressedThisFrame)
                    CompositionRoot.Game.Economy.Add(ResourceType.Materials, 10);
                if (kb[Key.F].wasPressedThisFrame)
                    CompositionRoot.Game.Economy.Add(ResourceType.Food, 5);
                if (kb[Key.B].wasPressedThisFrame)
                {
                    var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                    root?.AttemptPlaceTestBuilding();
                }
                if (kb[Key.R].wasPressedThisFrame)
                {
                    var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                    root?.AttemptStartTestResearch();
                }
                if (kb[Key.C].wasPressedThisFrame)
                {
                    var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                    root?.AttemptCompleteTestResearch();
                }
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.M))
                CompositionRoot.Game.Economy.Add(ResourceType.Materials, 10);
            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                CompositionRoot.Game.Economy.Add(ResourceType.Food, 5);
            if (UnityEngine.Input.GetKeyDown(KeyCode.B))
            {
                var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                root?.AttemptPlaceTestBuilding();
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                root?.AttemptStartTestResearch();
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                var root = FindObjectOfType<Game.Presentation.Bootstrap.CompositionRoot>();
                root?.AttemptCompleteTestResearch();
            }
#endif
        }
    }
}
