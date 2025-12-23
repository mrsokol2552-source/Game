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
        [Header("Smoothing")]
        [Tooltip("If consecutive waypoints keep direction within this dot threshold, merge them into one segment.")]
        public float StraightDotThreshold = 0.99f;
        [Tooltip("Minimum run length (in waypoints) before merging straight segments.")]
        public int MinStraightRun = 3;
        [Header("Debug Draw")]
        public bool DrawPathGizmos = true;
        [Tooltip("Also draw debug lines each frame (visible in Scene/Game with Gizmos on).")]
        public bool DrawDebugLines = false;
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

            // If we have a destination and are close enough, advance immediately to avoid a stop/reset.
            var pos = transform.position;
            if ((current - pos).sqrMagnitude <= WaypointEpsilon * WaypointEpsilon)
            {
                if (!TryAdvance())
                {
                    _view.ClearDestination("path-complete");
                }
            }

            if (DrawDebugLines && UnityEngine.Application.isPlaying)
            {
                DrawPathDebug();
            }
        }

        private void DrawPathDebug()
        {
            var list = new System.Collections.Generic.List<Vector3>(_points);
            if (_view != null && _view.TryGetDestination(out var curDest))
                list.Insert(0, curDest);
            if (list.Count == 0) return;

            var prev = transform.position;
            var col = PathColor;
            col.a = 0.8f;
            int limit = Mathf.Min(list.Count, 64);
            for (int i = 0; i < limit; i++)
            {
                var p = list[i];
                Debug.DrawLine(prev, p, col, 0f, false);
                prev = p;
            }
        }

        private bool TryAdvance()
        {
            if (_points.Count <= 0)
                return false;
            var next = _points.Dequeue();
            _view.SetDestination(next);
            return true;
        }

        public void SetWorldPath(IEnumerable<Vector3> points, PathSource source = PathSource.Manual)
        {
            _points.Clear();
            foreach (var p in SimplifyStraightSegments(points, StraightDotThreshold, MinStraightRun))
                _points.Enqueue(p);
            Source = source;
            TryAdvance();
        }

        // Set path from a shared list with an offset applied to each point (no extra list allocations).
        public void SetSharedPathWithOffset(IList<Vector3> basePath, Vector3 offset, PathSource source = PathSource.Manual)
        {
            _points.Clear();
            if (basePath != null)
            {
                for (int i = 0; i < basePath.Count; i++)
                {
                    _points.Enqueue(basePath[i] + offset);
                }
            }
            Source = source;
            TryAdvance();
        }

        // Merge consecutive points that keep nearly the same direction to avoid per-hex braking on straight runs.
        private static IEnumerable<Vector3> SimplifyStraightSegments(IEnumerable<Vector3> pts, float dotThreshold, int minRun)
        {
            var list = (pts is System.Collections.Generic.IList<Vector3> l) ? l : new System.Collections.Generic.List<Vector3>(pts);
            int count = list.Count;
            if (count == 0) yield break;
            if (count == 1) { yield return list[0]; yield break; }

            minRun = Mathf.Max(2, minRun); // at least 2 waypoints to consider a run

            int runStart = 0;
            Vector3 prevDir = (list[1] - list[0]);
            prevDir.z = 0f;
            prevDir = prevDir.sqrMagnitude > 0f ? prevDir.normalized : Vector3.right;

            for (int i = 1; i < count; i++)
            {
                var dir = (list[i] - list[i - 1]);
                dir.z = 0f;
                var mag = dir.magnitude;
                if (mag < 0.0001f) continue;
                var ndir = dir / mag;
                bool changed = Vector3.Dot(prevDir, ndir) < dotThreshold;
                if (changed)
                {
                    // flush run [runStart .. i-1]
                    int runEnd = i - 1;
                    int runLen = runEnd - runStart + 1;
                    if (runLen < minRun)
                    {
                        for (int k = runStart; k <= runEnd; k++)
                            yield return list[k];
                    }
                    else
                    {
                        // keep only start of straight run
                        yield return list[runStart];
                    }
                    runStart = i - 1;
                    prevDir = ndir;
                }
            }
            // flush last run to include final point
            int lastRunEnd = count - 1;
            int lastRunLen = lastRunEnd - runStart + 1;
            if (lastRunLen < minRun)
            {
                for (int k = runStart; k <= lastRunEnd; k++)
                    yield return list[k];
            }
            else
            {
                yield return list[runStart];
                yield return list[count - 1];
            }
        }

        public void Cancel()
        {
            bool hadPoints = _points.Count > 0;
            bool hadDestination = _view != null && _view.TryGetDestination(out _);
            _points.Clear();
            if (_view != null)
                _view.ClearDestination("follower-cancel");
            if (!hadDestination && hadPoints)
                Game.Presentation.Pathfinding.PathProfiler.CountPathReset("follower-cancel");
            Source = PathSource.Manual;
        }

        private void OnDrawGizmos()
        {
            if (!DrawPathGizmos) return;
            if (!UnityEngine.Application.isPlaying) return;
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
