using Game.Domain.Units;
using UnityEngine;

namespace Game.Presentation.View
{
    public class UnitView : MonoBehaviour
    {
        public UnitStats Stats = new UnitStats();

        private Vector3? destination;

        public void SetDestination(Vector3 target)
        {
            destination = target;
        }

        public void ClearDestination()
        {
            destination = null;
        }

        public bool TryGetDestination(out Vector3 target)
        {
            if (destination.HasValue)
            {
                target = destination.Value;
                return true;
            }
            target = default;
            return false;
        }

        private void Update()
        {
            if (destination.HasValue)
            {
                var target = destination.Value;
                var pos = transform.position;
                var step = Stats.Speed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(pos, target, step);
                if (Vector3.SqrMagnitude(transform.position - target) < 0.0001f)
                {
                    destination = null;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (destination.HasValue)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, destination.Value);
            }
        }
    }
}
