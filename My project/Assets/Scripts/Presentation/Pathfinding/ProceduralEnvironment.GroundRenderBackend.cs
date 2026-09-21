/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderBackend.cs
@module: presentation.pathfinding.worldgen.ground_render_backend
@purpose: Hosts world-tilemap writeback for ground render payloads so normal terrain rendering can consume cached payloads instead of depending on inline tilemap writes.
@entry: PENV-35, ProceduralEnvironment.ApplyGroundRenderDecisionBlockToTilemaps, ProceduralEnvironment.TryApplyCachedGroundRenderPayloadToTilemaps
@api: partial class implementation for ProceduralEnvironment
@deps: ground render payload, ground render source cache, ground and transition tilemaps
@data: cached full-ground decisions and transient tile arrays for tilemap writeback
@perf: hot path; centralizes tilemap backend work so future shader/world render backends can swap under one seam
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: transition tilemap usage and terrain ruleset generation settings in ProceduralEnvironment
@assets: ground and transition tilemaps
@notes: keep this backend write-focused; decision building stays in GroundRenderPayload and cached extraction stays in GroundRenderSource
*/

using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDRENDERBACKEND]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderBackend.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void ApplyGroundRenderDecisionBlockToTilemaps(
            int width,
            int startRow,
            int rowCount,
            bool skipBase,
            bool useTransition,
            GroundRenderCellDecision[] decisions)
        {
            if (decisions == null || decisions.Length == 0)
                return;

            TileBase[] groundTiles = (!skipBase || !useTransition) ? new TileBase[decisions.Length] : null;
            TileBase[] edgeTiles = useTransition ? new TileBase[decisions.Length] : null;
            ApplyGroundRenderDecisionBlock(skipBase, useTransition, decisions, groundTiles, edgeTiles);
            WriteGroundRenderDecisionBlock(width, startRow, rowCount, skipBase, useTransition, groundTiles, edgeTiles);
        }

        private bool TryPopulateGroundRenderDecisionBlockFromCache(
            int startRow,
            int rowCount,
            GroundRenderCellDecision[] decisions)
        {
            return TryPopulateGroundRenderDecisionBlockFromPayloadStorage(
                startRow,
                rowCount,
                true,
                decisions);
        }

        private bool TryPopulateGroundRenderDecisionBlockFromPayloadStorage(
            int startRow,
            int rowCount,
            bool requireCommitted,
            GroundRenderCellDecision[] decisions)
        {
            if ((requireCommitted && !_hasCachedGroundRenderPayload)
                || !HasGroundRenderPayloadStorage
                || _cachedGroundRenderPayload == null
                || decisions == null
                || startRow < 0
                || rowCount <= 0
                || startRow + rowCount > _cachedGroundRenderHeight
                || decisions.Length != _cachedGroundRenderWidth * rowCount)
            {
                return false;
            }

            System.Array.Copy(
                _cachedGroundRenderPayload,
                startRow * _cachedGroundRenderWidth,
                decisions,
                0,
                decisions.Length);
            return true;
        }

        private bool TryApplyGroundRenderPayloadBlockToTilemaps(
            int startRow,
            int rowCount,
            bool skipBase,
            bool requireCommitted)
        {
            if (_ground == null
                || !HasGroundRenderPayloadStorage
                || startRow < 0
                || rowCount <= 0
                || startRow + rowCount > _cachedGroundRenderHeight)
            {
                return false;
            }

            var decisions = new GroundRenderCellDecision[_cachedGroundRenderWidth * rowCount];
            if (!TryPopulateGroundRenderDecisionBlockFromPayloadStorage(startRow, rowCount, requireCommitted, decisions))
                return false;

            bool useTransition = _cachedGroundRenderUsesTransition && _transitions != null;
            ApplyGroundRenderDecisionBlockToTilemaps(
                _cachedGroundRenderWidth,
                startRow,
                rowCount,
                skipBase,
                useTransition,
                decisions);
            return true;
        }

        private bool TryApplyCachedGroundRenderPayloadToTilemaps()
        {
            return TryApplyGroundRenderPayloadBlockToTilemaps(
                0,
                _cachedGroundRenderHeight,
                false,
                true);
        }
    }
}
