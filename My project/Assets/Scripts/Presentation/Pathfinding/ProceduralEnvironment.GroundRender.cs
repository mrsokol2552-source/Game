/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRender.cs
@module: presentation.pathfinding.worldgen.ground_render
@purpose: Hosts ground and transition ruleset orchestration outside the ProceduralEnvironment monolith so future render backends can swap writeback without re-embedding terrain loops in ProceduralEnvironment.cs.
@entry: PENV-34, ProceduralEnvironment.ApplyRulesetToGround, ProceduralEnvironment.ApplyRulesetToGroundRoutine
@api: partial class implementation for ProceduralEnvironment
@deps: ground render payload, ground render source, terrain layers, transition tilemap writeback
@data: per-cell ground decisions, cached full-ground payloads, tile arrays for sync/coroutine writeback
@perf: hot path; keeps terrain write orchestration localized so payload extraction and render migration can evolve independently
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: transition tilemap usage, coroutine row budget, and terrain ruleset generation settings in ProceduralEnvironment
@assets: ground and transition tilemaps plus generated terrain tiles
@notes: keep tile selection in GroundRenderPayload and cache extraction in GroundRenderSource so this file only owns orchestration/writeback flow
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDRENDER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRender.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void ApplyRulesetToGround(
            int width,
            int height,
            bool skipBase,
            List<TerrainLayer> layers,
            Dictionary<int, TileBase>[] edgeLookup,
            List<HexMaskTile>[] edgeByBits,
            int tileSeed,
            int[] layerIndex,
            HexTerrainRuleset ruleset)
        {
            ApplyRulesetToGroundCore(
                width,
                height,
                skipBase,
                layers,
                edgeLookup,
                edgeByBits,
                tileSeed,
                layerIndex,
                ruleset);
        }

        private IEnumerator ApplyRulesetToGroundRoutine(
            int width,
            int height,
            bool skipBase,
            List<TerrainLayer> layers,
            Dictionary<int, TileBase>[] edgeLookup,
            List<HexMaskTile>[] edgeByBits,
            int tileSeed,
            int[] layerIndex,
            HexTerrainRuleset ruleset)
        {
            return ApplyRulesetToGroundRoutineCore(
                width,
                height,
                skipBase,
                layers,
                edgeLookup,
                edgeByBits,
                tileSeed,
                layerIndex,
                ruleset);
        }

        private void ApplyRulesetToGroundCore(
            int width,
            int height,
            bool skipBase,
            List<TerrainLayer> layers,
            Dictionary<int, TileBase>[] edgeLookup,
            List<HexMaskTile>[] edgeByBits,
            int tileSeed,
            int[] layerIndex,
            HexTerrainRuleset ruleset)
        {
            if (_ground == null || layers == null || layerIndex == null)
                return;

            bool useTransition = UseTransitionTilemap && _transitions != null;
            int size = width * height;
            var decisions = new GroundRenderCellDecision[size];
            PopulateGroundRenderDecisionBlock(
                width,
                height,
                skipBase,
                useTransition,
                layers,
                edgeLookup,
                edgeByBits,
                tileSeed,
                layerIndex,
                ruleset,
                0,
                height,
                decisions);

            if (skipBase && !useTransition)
            {
                ClearGroundRenderPayloadCache();
                WriteGroundRenderDecisionOverlay(decisions);
                return;
            }

            if (!skipBase)
            {
                CacheGroundRenderPayload(width, height, useTransition, decisions);
                if (TryApplyGroundRenderPayloadBlockToTilemaps(0, height, false, true))
                    return;
            }
            else
                ClearGroundRenderPayloadCache();

            ApplyGroundRenderDecisionBlockToTilemaps(width, 0, height, skipBase, useTransition, decisions);
        }

        private IEnumerator ApplyRulesetToGroundRoutineCore(
            int width,
            int height,
            bool skipBase,
            List<TerrainLayer> layers,
            Dictionary<int, TileBase>[] edgeLookup,
            List<HexMaskTile>[] edgeByBits,
            int tileSeed,
            int[] layerIndex,
            HexTerrainRuleset ruleset)
        {
            if (_ground == null || layers == null || layerIndex == null)
                yield break;

            bool useTransition = UseTransitionTilemap && _transitions != null;
            int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
            bool stagedPayloadStorage = !skipBase && TryPrepareGroundRenderPayloadStorage(width, height, useTransition);
            if (!skipBase && !stagedPayloadStorage)
                ClearGroundRenderPayloadCache();

            for (int row = 0; row < height; row += rowsPerFrame)
            {
                int rowCount = Mathf.Min(rowsPerFrame, height - row);
                int blockSize = width * rowCount;
                var decisions = new GroundRenderCellDecision[blockSize];
                PopulateGroundRenderDecisionBlock(
                    width,
                    height,
                    skipBase,
                    useTransition,
                    layers,
                    edgeLookup,
                    edgeByBits,
                    tileSeed,
                    layerIndex,
                    ruleset,
                    row,
                    rowCount,
                    decisions);

                if (skipBase && !useTransition)
                {
                    ClearGroundRenderPayloadCache();
                    WriteGroundRenderDecisionOverlay(decisions);
                    yield return null;
                    continue;
                }

                bool appliedFromPayloadStorage = false;
                if (stagedPayloadStorage
                    && TryStageGroundRenderDecisionBlockInPayloadStorage(row, rowCount, decisions)
                    && TryApplyGroundRenderPayloadBlockToTilemaps(row, rowCount, false, false))
                {
                    appliedFromPayloadStorage = true;
                }

                if (!appliedFromPayloadStorage)
                    ApplyGroundRenderDecisionBlockToTilemaps(width, row, rowCount, skipBase, useTransition, decisions);

                yield return null;
            }

            if (stagedPayloadStorage)
                CommitGroundRenderPayloadStorage();
            else
                ClearGroundRenderPayloadCache();
        }
    }
}
