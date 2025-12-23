using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Disables visual components (SpriteRenderer, Animator, UnitHpOverlay) when the unit is far or outside the camera frustum.
    /// Logic continues to run; only visuals are toggled.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitVisualCulling : MonoBehaviour
    {
        [Header("Culling")]
        [Tooltip("Check interval in seconds to reduce CPU load.")]
        public float CheckInterval = 0.25f;
        [Tooltip("Extra radius multiplier relative to camera size. 1 = roughly viewport; increase to keep margin.")]
        public float DistanceMargin = 1.2f;
        [Tooltip("If enabled, also test renderer bounds against camera frustum.")]
        public bool UseFrustumTest = true;

        private SpriteRenderer _sr;
        private Animator _anim;
        private Game.Presentation.View.UnitHpOverlay _hp;
        private float _timer;
        private bool _culled;
        private readonly Plane[] _planes = new Plane[6];

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            // Ensure visuals are on when component becomes active
            SetCulled(false);
        }

        private void CacheRefs()
        {
            _sr = GetComponent<SpriteRenderer>();
            _anim = GetComponent<Animator>();
            _hp = GetComponent<Game.Presentation.View.UnitHpOverlay>();
        }

        private void Update()
        {
            if (_sr == null || _anim == null || _hp == null)
                CacheRefs(); // Catch late-added components

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = CheckInterval;

            var cam = Camera.main;
            if (cam == null)
            {
                SetCulled(false);
                return;
            }

            bool visible = IsWithinRange(cam) && (!UseFrustumTest || IsInFrustum(cam));
            SetCulled(!visible);
        }

        private bool IsWithinRange(Camera cam)
        {
            float range;
            if (cam.orthographic)
            {
                // Diagonal of the viewport scaled by margin
                range = cam.orthographicSize * DistanceMargin * Mathf.Sqrt(cam.aspect * cam.aspect + 1f);
            }
            else
            {
                // Approximate range for perspective cameras
                range = 50f * DistanceMargin;
            }
            return (cam.transform.position - transform.position).sqrMagnitude <= range * range;
        }

        private bool IsInFrustum(Camera cam)
        {
            Bounds b;
            if (_sr != null && _sr.sprite != null)
                b = _sr.bounds;
            else
                b = new Bounds(transform.position, Vector3.one * 0.1f);
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            return GeometryUtility.TestPlanesAABB(_planes, b);
        }

        private void SetCulled(bool culled)
        {
            if (_culled == culled) return;
            _culled = culled;
            if (_sr != null) _sr.enabled = !culled;
            if (_anim != null) _anim.enabled = !culled;
            if (_hp != null) _hp.enabled = !culled;
        }
    }
}
