using UnityEngine;
using Game.Presentation.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public class CameraZoom2D : MonoBehaviour
    {
        public float MinOrthoSize = 2f;
        public float MaxOrthoSize = 30f;
        public float OrthoStep = 1.5f;
        public float OrthoLerpSpeed = 10f;

        public float MinFov = 25f;
        public float MaxFov = 75f;
        public float FovStep = 5f;
        public float FovLerpSpeed = 10f;

        public bool InvertScroll = false;

        private Camera _cam;
        private float _target;
        public float PanSpeed = 8f;
        public float PanZoomScale = 0.2f; // scale pan by ortho size
        private bool _dragging;
        private Vector3 _lastDragWorld;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _target = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;
        }

        private void Update()
        {
            // Do not zoom when pointer is over HUD (to allow UI scrollviews)
            if (HudController.IsPointerOverHud())
            {
                // Still allow pan with WASD even if over HUD, but block scroll/drag
                HandleWASD();
                return;
            }

            float delta = 0f;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var s = mouse.scroll.ReadValue().y; // up is positive
                delta += s;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.Equals].wasPressedThisFrame || kb[Key.NumpadPlus].wasPressedThisFrame) delta += 1f;
                if (kb[Key.Minus].wasPressedThisFrame || kb[Key.NumpadMinus].wasPressedThisFrame) delta -= 1f;
            }
#else
            delta += UnityEngine.Input.mouseScrollDelta.y; // legacy
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Equals) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadPlus)) delta += 1f;
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Minus) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadMinus)) delta -= 1f;
#endif

            if (Mathf.Abs(delta) > 0.0001f)
            {
                float sign = InvertScroll ? -1f : 1f;
                if (_cam.orthographic)
                {
                    _target -= sign * delta * OrthoStep;
                    _target = Mathf.Clamp(_target, MinOrthoSize, MaxOrthoSize);
                }
                else
                {
                    _target -= sign * delta * FovStep;
                    _target = Mathf.Clamp(_target, MinFov, MaxFov);
                }
            }

            if (_cam.orthographic)
            {
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _target, Time.deltaTime * OrthoLerpSpeed);
            }
            else
            {
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _target, Time.deltaTime * FovLerpSpeed);
            }

            // Pan (MMB drag) and WASD
            HandleDrag();
            HandleWASD();
        }

        private void HandleWASD()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = Keyboard.current;
            if (kb == null) return;
            Vector2 dir = Vector2.zero;
            if (kb[Key.W].isPressed) dir.y += 1f;
            if (kb[Key.S].isPressed) dir.y -= 1f;
            if (kb[Key.A].isPressed) dir.x -= 1f;
            if (kb[Key.D].isPressed) dir.x += 1f;
#else
            Vector2 dir = Vector2.zero;
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.W)) dir.y += 1f;
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.S)) dir.y -= 1f;
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.A)) dir.x -= 1f;
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.D)) dir.x += 1f;
#endif
            if (dir.sqrMagnitude > 0f)
            {
                dir.Normalize();
                float scale = _cam.orthographic ? Mathf.Max(1f, _cam.orthographicSize * PanZoomScale) : 1f;
                var move = new Vector3(dir.x, dir.y, 0f) * PanSpeed * scale * Time.deltaTime;
                transform.position += move;
            }
        }

        private void HandleDrag()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var mouse = Mouse.current;
            bool down = mouse != null && mouse.middleButton.isPressed;
            Vector3 sp = mouse != null ? (Vector3)mouse.position.ReadValue() : UnityEngine.Input.mousePosition;
#else
            bool down = UnityEngine.Input.GetMouseButton(2);
            Vector3 sp = UnityEngine.Input.mousePosition;
#endif
            if (down)
            {
                var wp = ScreenToWorldOnPlane(sp);
                if (!_dragging)
                {
                    _dragging = true;
                    _lastDragWorld = wp;
                }
                else
                {
                    var delta = _lastDragWorld - wp;
                    transform.position += delta;
                    _lastDragWorld = wp;
                }
            }
            else
            {
                _dragging = false;
            }
        }

        private Vector3 ScreenToWorldOnPlane(Vector3 sp)
        {
            if (_cam.orthographic)
            {
                var wp = _cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 0f));
                wp.z = transform.position.z; // keep camera z
                return wp;
            }
            else
            {
                var ray = _cam.ScreenPointToRay(sp);
                // Intersect with z=0 plane
                if (Mathf.Abs(ray.direction.z) < 1e-4f) return transform.position;
                float t = -ray.origin.z / ray.direction.z;
                return ray.origin + ray.direction * t;
            }
        }
    }
}
