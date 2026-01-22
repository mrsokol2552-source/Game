using UnityEngine;

namespace Game.Presentation.View
{
    [CreateAssetMenu(fileName = "DirectionalAnimationSet", menuName = "RTS/Directional Animation Set")]
    public class DirectionalAnimationSet : ScriptableObject
    {
        [Header("Frame Layout")]
        [Min(1)]
        public int FramesPerDirection = 15;

        [Header("Playback")]
        [Min(0.1f)]
        public float IdleFps = 6f;
        [Min(0.1f)]
        public float WalkFps = 10f;
        [Min(0.1f)]
        public float AttackFps = 12f;
        [Min(0.1f)]
        public float DeathFps = 8f;

        [Header("Frames (E, SE, S, SW, W, NW, N, NE)")]
        public Sprite[] IdleFrames;
        public Sprite[] WalkFrames;
        public Sprite[] AttackFrames;
        public Sprite[] DeathFrames;

        public int GetDirectionIndex(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
                return 0;

            // Map to clockwise index starting at East: E, SE, S, SW, W, NW, N, NE
            float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            int sector = Mathf.RoundToInt(angle / 45f) & 7;
            return sector;
        }

        public Sprite GetFrame(Sprite[] frames, int dirIndex, float time, float fps)
        {
            if (frames == null || frames.Length == 0 || FramesPerDirection <= 0)
                return null;

            int start = dirIndex * FramesPerDirection;
            if (start < 0 || start >= frames.Length)
                return null;

            int frameCount = Mathf.Min(FramesPerDirection, frames.Length - start);
            if (frameCount <= 0)
                return null;

            int frame = Mathf.FloorToInt(time * fps) % frameCount;
            return frames[start + frame];
        }

        public float GetDuration(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0 || FramesPerDirection <= 0 || fps <= 0f)
                return 0f;
            int frameCount = Mathf.Min(FramesPerDirection, frames.Length);
            return frameCount / fps;
        }
    }
}
