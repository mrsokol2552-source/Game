using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Coordinates job-based systems to ensure a stable update order.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class JobPipelineCoordinator : MonoBehaviour
    {
        public static JobPipelineCoordinator Instance { get; private set; }

        [Tooltip("Enable coordinated updates for job systems.")]
        public bool Enabled = true;
        [Tooltip("Drive ORCA avoidance from the coordinator.")]
        public bool DriveOrca = true;
        [Tooltip("Drive movement jobs from the coordinator.")]
        public bool DriveMovement = true;
        [Tooltip("Drive UnitSoARegistry snapshot updates.")]
        public bool DriveSoA = true;

        private OrcaAvoidanceSystem _orca;
        private MovementJobSystem _movement;
        private UnitSoARegistry _soa;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("JobPipelineCoordinator");
            Instance = go.AddComponent<JobPipelineCoordinator>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Enabled) return;
            EnsureRefs();
            float dt = Time.deltaTime;
            if (DriveSoA && _soa != null)
            {
                if (_orca != null)
                {
                    _soa.OrcaCellSize = _orca.CellSize;
                    _soa.OrcaMinResponsibility = _orca.MinResponsibility;
                }
                _soa.Tick();
            }
            if (DriveOrca && _orca != null)
                _orca.Tick(dt);
            if (DriveMovement && _movement != null)
                _movement.Tick(dt);
        }

        private void OnEnable()
        {
            EnsureRefs();
            ApplyExternalFlags(true);
        }

        private void OnDisable()
        {
            ApplyExternalFlags(false);
        }

        private void EnsureRefs()
        {
            if (_orca == null)
                _orca = OrcaAvoidanceSystem.Instance;
            if (_movement == null)
                _movement = MovementJobSystem.Instance;
            if (_soa == null)
                _soa = UnitSoARegistry.Instance;
            ApplyExternalFlags(true);
        }

        private void ApplyExternalFlags(bool value)
        {
            if (_orca != null) _orca.ExternalUpdate = value;
            if (_movement != null) _movement.ExternalUpdate = value;
            if (_soa != null) _soa.ExternalUpdate = value;
        }
    }
}
