/*
@file: My project/Assets/Scripts/Presentation/View/UnitView.Rendering.cs
@module: presentation.view.unit
@purpose: Extracted render-order maintenance and gizmo helpers for UnitView.
@entry: UVEW-04
@api: UnitView partial rendering helpers
@deps: SpriteRenderer, CompositionRoot
@data: sorting initialization state, sorting tie-breakers, destination gizmo
@perf: lightweight per-frame LateUpdate when Y-sorting is enabled
@thread: main thread only
@tests: manual overlap/flicker verification
@config: UseYSorting, SortOrderPerWorldUnit, SortingOrderBase, CompositionRoot sorting settings
@assets: SpriteRenderer on the unit GameObject
@notes: render-only responsibilities live here so future visual migration can bypass UnitView movement code cleanly
*/

using Game.Presentation.Bootstrap;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITVIEW-RENDERING]
// Logical block: UnitView render-order extraction.

namespace Game.Presentation.View
{
    public partial class UnitView
    {
        // [UVEW-04]
        // LateUpdate Y-sorting, sorting bootstrap from CompositionRoot, and selection gizmos.
        private void LateUpdate()
        {
            if (!UseYSorting || _sr == null) return;

            if (!_sortingInitialized)
            {
                if (UseCompositionRootSorting)
                {
                    var root = Object.FindAnyObjectByType<CompositionRoot>();
                    if (root != null)
                    {
                        SortingOrderBase = root.UnitSortingOrder + SortingOrderOffset;
                        if (!string.IsNullOrEmpty(root.UnitSortingLayerName))
                            _sr.sortingLayerName = root.UnitSortingLayerName;
                    }
                }

                _sortingTie = AddSortingTieBreaker ? (Mathf.Abs(GetInstanceID()) % 3) : 0;
                _sortingInitialized = true;
            }

            int order = SortingOrderBase - Mathf.RoundToInt(transform.position.y * SortOrderPerWorldUnit);
            _sr.sortingOrder = order + _sortingTie;
        }

        private void OnDrawGizmosSelected()
        {
            if (!destination.HasValue) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, destination.Value);
        }
    }
}
