using UnityEngine;

namespace RtsGame.Presentation.View
{
    public class UnitView : MonoBehaviour
    {
        public RtsGame.Domain.Units.UnitStats Stats;

        // Placeholder movement; to be replaced with pathfinding integration
        public void MoveTo(Vector3 world)
        {
            transform.position = world;
        }
    }
}

