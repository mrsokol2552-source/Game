using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Lightweight on-screen stats for pathfinding cost. Shows builds per frame/sec and rejects.
    /// </summary>
    public class PathDebugHUD : MonoBehaviour
    {
        public bool Show = true;
        public Vector2 Offset = new Vector2(10, 10);
        public Color TextColor = Color.white;

        private GUIStyle _style;

        private void OnGUI()
        {
            if (!Show) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = TextColor } };
            }

            var stats = PathProfiler.CollectAndReset();
            string txt = $"Path builds/frame: {stats.BuildsThisFrame}\n" +
                         $"Accepts: {stats.Accepts} | Rejects: {stats.Rejects}\n" +
                         $"Paths: {stats.PathsThisFrame} | MaxLen: {stats.MaxPathLengthThisFrame} | TotalLen: {stats.TotalPathLengthThisFrame}\n" +
                         $"Commands: {stats.CommandsThisFrame} | Jitter: {stats.JitterThisFrame} | CrowdMoves: {stats.CrowdMovesThisFrame}\n" +
                         $"PathResets: {stats.PathResetsThisFrame}\n" +
                         $"MaxNodes: {stats.MaxNodesThisFrame}\n" +
                         $"Frame: {Time.frameCount}";
            GUI.Label(new Rect(Offset.x, Offset.y, 300, 80), txt, _style);
        }
    }
}
