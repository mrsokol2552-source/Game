using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    public static class PathProfiler
    {
        public struct Stats
        {
            public int BuildsThisFrame;
            public int Accepts;
            public int Rejects;
            public int MaxNodesThisFrame;
            public int PathsThisFrame;
            public int MaxPathLengthThisFrame;
            public int TotalPathLengthThisFrame;
            public int CommandsThisFrame;
            public int JitterThisFrame;
            public int CrowdMovesThisFrame;
            public int PathResetsThisFrame;
        }

        public static bool EnableAnomalyLog = true;
        public static float AnomalyFrameTimeMs = 120f;
        public static int AnomalyJitter = 10;
        public static int AnomalyCrowdMoves = 20;
        public static int AnomalyMaxPathLen = 200;
        public static int AnomalyPathResets = 5;

        private static int _builds;
        private static int _accepts;
        private static int _rejects;
        private static int _lastFrame;
        private static int _maxNodesThisFrame;
        private static int _pathsThisFrame;
        private static int _maxPathLenThisFrame;
        private static int _totalPathLenThisFrame;
        private static int _commandsThisFrame;
        private static int _jitterThisFrame;
        private static int _crowdMovesThisFrame;
        private static int _pathResetsThisFrame;
        private static int _lastLogFrame;
        public static int TotalPathResets;
        private static readonly System.Collections.Generic.Dictionary<string, int> _resetReasonTotals = new System.Collections.Generic.Dictionary<string, int>(16);

        public static void CountBuild(bool accepted)
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
                _pathResetsThisFrame = 0;
            }
            _builds++;
            if (accepted) _accepts++; else _rejects++;
        }

        public static void CountPath(int nodes)
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
                _pathResetsThisFrame = 0;
            }
            if (nodes > _maxNodesThisFrame) _maxNodesThisFrame = nodes;
        }

        public static void CountPathLength(int length)
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
                _pathResetsThisFrame = 0;
            }
            _pathsThisFrame++;
            _totalPathLenThisFrame += length;
            if (length > _maxPathLenThisFrame) _maxPathLenThisFrame = length;
        }

        public static void CountCommand()
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
            }
            _commandsThisFrame++;
        }

        public static void CountJitter()
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
                _pathResetsThisFrame = 0;
            }
            _jitterThisFrame++;
        }

        public static void CountCrowdMoves(int moves)
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
                _pathResetsThisFrame = 0;
            }
            _crowdMovesThisFrame += Mathf.Max(0, moves);
        }

        public static void CountPathReset(string reason = "unspecified")
        {
            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                _lastFrame = f;
                _builds = 0;
                _accepts = 0;
                _rejects = 0;
                _maxNodesThisFrame = 0;
                _pathsThisFrame = 0;
                _maxPathLenThisFrame = 0;
                _totalPathLenThisFrame = 0;
                _commandsThisFrame = 0;
                _jitterThisFrame = 0;
                _crowdMovesThisFrame = 0;
                _pathResetsThisFrame = 0;
            }
            _pathResetsThisFrame++;
            TotalPathResets++;
            if (!string.IsNullOrEmpty(reason))
            {
                if (_resetReasonTotals.TryGetValue(reason, out var v))
                    _resetReasonTotals[reason] = v + 1;
                else
                    _resetReasonTotals[reason] = 1;
            }
        }

        public static Stats CollectAndReset()
        {
            var s = new Stats
            {
                BuildsThisFrame = _builds,
                Accepts = _accepts,
                Rejects = _rejects,
                MaxNodesThisFrame = _maxNodesThisFrame,
                PathsThisFrame = _pathsThisFrame,
                MaxPathLengthThisFrame = _maxPathLenThisFrame,
                TotalPathLengthThisFrame = _totalPathLenThisFrame,
                CommandsThisFrame = _commandsThisFrame,
                JitterThisFrame = _jitterThisFrame,
                CrowdMovesThisFrame = _crowdMovesThisFrame,
                PathResetsThisFrame = _pathResetsThisFrame
            };
            MaybeLogAnomaly(s);
            _accepts = 0; _rejects = 0;
            _pathsThisFrame = 0;
            _maxPathLenThisFrame = 0;
            _totalPathLenThisFrame = 0;
            _commandsThisFrame = 0;
            _jitterThisFrame = 0;
            _crowdMovesThisFrame = 0;
            _pathResetsThisFrame = 0;
            return s;
        }

        public static void ResetTotals()
        {
            TotalPathResets = 0;
            _resetReasonTotals.Clear();
        }

        public static System.Collections.Generic.Dictionary<string, int> GetResetReasonTotals()
        {
            return new System.Collections.Generic.Dictionary<string, int>(_resetReasonTotals);
        }

        private static void MaybeLogAnomaly(Stats s)
        {
            if (!EnableAnomalyLog) return;
            int f = Time.frameCount;
            if (f == _lastLogFrame) return;
            float dtMs = Time.deltaTime * 1000f;
            bool anomaly =
                dtMs >= AnomalyFrameTimeMs ||
                s.JitterThisFrame >= AnomalyJitter ||
                s.CrowdMovesThisFrame >= AnomalyCrowdMoves ||
                s.MaxPathLengthThisFrame >= AnomalyMaxPathLen ||
                s.PathResetsThisFrame >= AnomalyPathResets;
            if (!anomaly) return;
            _lastLogFrame = f;
            Debug.LogWarning($"[PathAnomaly] frame={f} dt={dtMs:F1}ms cmds={s.CommandsThisFrame} jit={s.JitterThisFrame} crowd={s.CrowdMovesThisFrame} paths={s.PathsThisFrame} maxLen={s.MaxPathLengthThisFrame} resets={s.PathResetsThisFrame} maxNodes={s.MaxNodesThisFrame}");
        }
    }
}
