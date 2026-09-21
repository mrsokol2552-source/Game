/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewRenderSource.cs
@module: presentation.pathfinding.worldgen.farview_render_source
@purpose: Defines the typed per-chunk far-view render source contract so chunk bake, raster backends, and future shader paths can consume one payload object instead of many mutable chunk fields.
@entry: PENV-29, ProceduralEnvironment.FarViewChunkRenderSource
@api: partial class implementation for ProceduralEnvironment
@deps: far-view background payload, tilemap chunk snapshots
@data: cached per-chunk background payload, tile snapshots, slice versions, and chunk capture metadata
@perf: low; data holder only, used to reduce redundant slice rebuilds and simplify renderer backends
@thread: main thread only
@tests: indirect coverage via Unity recompilation, far-view HUD counters, and repo audits
@config: far-view chunked bake settings in ProceduralEnvironment
@assets: none directly
@notes: keep this source renderer-agnostic so future GPU backends can consume one stable chunk contract
*/

using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWRENDERSOURCE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewRenderSource.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private sealed class FarViewChunkRenderSource
        {
            public BackgroundRenderCellDecision[] BackgroundPayload;
            public int BackgroundPayloadVersion;
            public int BackgroundCellCount;
            public RectInt BackgroundCellRect;
            public Bounds BackgroundBounds;
            public TilemapChunkSnapshot GroundSnapshot;
            public TilemapChunkSnapshot TransitionSnapshot;
            public TilemapChunkSnapshot PropSnapshot;
            public TilemapChunkSnapshot BlockerSnapshot;
            public int GroundCellCount;
            public int TransitionCellCount;
            public int PropCellCount;
            public int BlockerCellCount;
            public bool TileSnapshotResolved;
            public int TileSnapshotVersion;
            public Bounds TileSnapshotCaptureBounds;

            public bool HasBackgroundPayload => BackgroundPayload != null && BackgroundPayload.Length > 0;

            public bool HasTileSnapshots =>
                (GroundSnapshot != null && GroundSnapshot.HasTiles)
                || (TransitionSnapshot != null && TransitionSnapshot.HasTiles)
                || (PropSnapshot != null && PropSnapshot.HasTiles)
                || (BlockerSnapshot != null && BlockerSnapshot.HasTiles);

            public bool HasAnyPayloadSources => HasBackgroundPayload || HasTileSnapshots;
        }
    }
}
