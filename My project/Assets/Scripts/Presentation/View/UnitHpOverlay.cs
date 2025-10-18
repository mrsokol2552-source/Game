using UnityEngine;

namespace Game.Presentation.View
{
    [RequireComponent(typeof(UnitCombat))]
    public class UnitHpOverlay : MonoBehaviour
    {
        public Vector3 Offset = new Vector3(0, 0.6f, 0);
        public Vector2 Size = new Vector2(44, 6);

        private UnitCombat _combat;
        private static Texture2D _tex;

        private void Awake()
        {
            _combat = GetComponent<UnitCombat>();
            if (_tex == null)
            {
                _tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _tex.SetPixel(0, 0, Color.white);
                _tex.Apply();
            }
        }

        private void OnGUI()
        {
            if (_combat == null) return;
            var cam = Camera.main; if (cam == null) return;
            Vector3 wp = transform.position + Offset;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z < 0) return;

            float x = sp.x - Size.x / 2f;
            float y = Screen.height - sp.y - Size.y;

            // Background (red)
            DrawRect(new Rect(x - 1, y - 1, Size.x + 2, Size.y + 2), new Color(0, 0, 0, 0.5f));
            DrawRect(new Rect(x, y, Size.x, Size.y), new Color(0.2f, 0, 0, 0.9f));

            // Fill (green)
            float pct = Mathf.Clamp01(_combat.MaxHealth > 0 ? (float)_combat.CurrentHealth / _combat.MaxHealth : 0f);
            DrawRect(new Rect(x, y, Size.x * pct, Size.y), new Color(0.1f, 0.8f, 0.1f, 0.9f));
        }

        private static void DrawRect(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _tex);
            GUI.color = prev;
        }
    }
}

