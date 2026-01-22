using UnityEngine;
using Game.Presentation.Bootstrap;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITSORTBYY]
// Logical block: Scripts/Presentation/View/UnitSortByY.

namespace Game.Presentation.View
{
    /// <summary>
    /// Stable Y-based sorting to prevent sprite flicker when units overlap.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class UnitSortByY : MonoBehaviour
    {
        public bool UseCompositionRootBaseOrder = true;
        public int BaseOrder = 0;
        [Tooltip("Sorting order units per 1 world unit of Y. Higher = stronger separation.")]
        public float OrderPerWorldUnit = 10f;
        public bool AddInstanceTieBreaker = true;

        private SpriteRenderer _sr;
        private int _tie;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (UseCompositionRootBaseOrder)
            {
                var root = Object.FindAnyObjectByType<CompositionRoot>();
                if (root != null)
                {
                    BaseOrder = root.UnitSortingOrder;
                    if (!string.IsNullOrEmpty(root.UnitSortingLayerName) && SortingLayerExists(root.UnitSortingLayerName))
                        _sr.sortingLayerName = root.UnitSortingLayerName;
                }
            }
            _tie = AddInstanceTieBreaker ? (Mathf.Abs(GetInstanceID()) % 3) : 0;
        }

        private void LateUpdate()
        {
            if (_sr == null) return;
            int order = BaseOrder - Mathf.RoundToInt(transform.position.y * OrderPerWorldUnit);
            _sr.sortingOrder = order + _tie;
        }

        private static bool SortingLayerExists(string name)
        {
            foreach (var l in SortingLayer.layers)
            {
                if (l.name == name) return true;
            }
            return false;
        }
    }
}