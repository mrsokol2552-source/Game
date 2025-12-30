using UnityEngine;

namespace SmallScaleInc.CharacterCreatorModern
{
    public class SmoothCameraFollow : MonoBehaviour
    {
        [Header("Target & Offset")]
        public Transform target;
        public Vector3 offset;

        [Header("Smooth Movement")]
        public float smoothTime = 0.3f;
        private Vector3 velocity = Vector3.zero;

        [Header("Look-Ahead Settings")]
        public bool enableLookAhead = true;
        public float lookAheadDistance = 2f;
        public float lookAheadSpeed = 5f;
        private Vector3 currentLookAhead = Vector3.zero;
        private Vector3 lastTargetPosition;

        [Header("Zoom Settings")]
        public float zoomSpeed = 5f;
        public float minZoom = 2f;
        public float maxZoom = 10f;
        private Camera cam;

        void Start()
        {
            if (target != null)
            {
                lastTargetPosition = target.position;
            }
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("No Camera component found on the GameObject.");
            }
        }

        void LateUpdate()
        {
            if (target == null || cam == null)
            {
                return;
            }

            // Zoom with mouse scroll wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                float newSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
                cam.orthographicSize = newSize;
            }

            // Determine how much the target has moved since the last frame
            Vector3 targetDelta = target.position - lastTargetPosition;
            lastTargetPosition = target.position;

            // Calculate look-ahead offset if enabled
            if (enableLookAhead)
            {
                Vector3 desiredLookAhead = targetDelta.normalized * lookAheadDistance;
                currentLookAhead = Vector3.Lerp(currentLookAhead, desiredLookAhead, Time.deltaTime * lookAheadSpeed);
            }
            else
            {
                currentLookAhead = Vector3.zero;
            }

            // Compute the desired camera position with offset and look-ahead
            Vector3 desiredPosition = target.position + offset + currentLookAhead;
            desiredPosition.z = transform.position.z;

            // Smoothly move the camera toward the desired position
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        }
    }
}
