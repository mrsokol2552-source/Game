using System.Collections.Generic;
using Game.Presentation.View;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    [RequireComponent(typeof(UnitView))]
    public class UnitPathFollower : MonoBehaviour
    {
        public enum PathSource { Manual, Combat }
        public float WaypointEpsilon = 0.05f;
        [Header("Debug Draw")]
        public bool DrawPathGizmos = true;
        public Color PathColor = new Color(1f, 0.92f, 0.16f, 0.95f); // yellow-ish
        public Color NextPointColor = new Color(0.2f, 1f, 0.2f, 0.95f);

        private readonly Queue<Vector3> _points = new Queue<Vector3>();
        private UnitView _view;
        public PathSource Source { get; private set; } = PathSource.Manual;

        public bool HasPath
        {
            get
            {
                if (_points.Count > 0) return true;
                return _view != null && _view.TryGetDestination(out var _);
            }
        }

        private void Awake()
        {
            _view = GetComponent<UnitView>();
        }

        private void Update()
        {
            if (_view == null) return;

            if (!_view.TryGetDestination(out var current))
            {
                TryAdvance();
                return;
            }

            // If we have a destination and are close enough, request next
            var pos = transform.position;
            if ((current - pos).sqrMagnitude <= WaypointEpsilon * WaypointEpsilon)
            {
                _view.ClearDestination();
                TryAdvance();
            }
        }

        private void TryAdvance()
        {
            if (_points.Count > 0)
            {
                var next = _points.Dequeue();
                _view.SetDestination(next);
            }
        }

        public void SetWorldPath(IEnumerable<Vector3> points, PathSource source = PathSource.Manual)
        {
            _points.Clear();
            foreach (var p in points) _points.Enqueue(p);
            _view.ClearDestination();
            Source = source;
            TryAdvance();
        }

        public void Cancel()
        {
            _points.Clear();
            _view.ClearDestination();
            Source = PathSource.Manual;
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawPathGizmos) return;
            if (_view == null) _view = GetComponent<UnitView>();
            var from = transform.position;

            // Build a snapshot of remaining points including current destination if any
            var list = new System.Collections.Generic.List<Vector3>(_points);
            if (_view != null && _view.TryGetDestination(out var curDest))
                list.Insert(0, curDest);

            if (list.Count == 0) return;

            // Draw lines from current position through all queued points
            Gizmos.color = PathColor;
            Vector3 prev = from;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                Gizmos.DrawLine(prev, p);
                prev = p;
            }

            // Mark the next waypoint
            Gizmos.color = NextPointColor;
            Gizmos.DrawSphere(list[0], 0.06f);
        }
    }
}
