/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderPayload.cs
@module: presentation.pathfinding.worldgen.ground_render_payload
@purpose: Builds typed per-cell ground and transition render decisions so tilemap writeback, far-view, and future world render backends can consume the same terrain payload.
@entry: PENV-32, ProceduralEnvironment.BuildGroundRenderDecision, ProceduralEnvironment.PopulateGroundRenderDecisionBlock
@api: partial class implementation for ProceduralEnvironment
@deps: terrain ruleset, layer palettes, edge lookup, runtime ground conversion, and tilemap writeback
@data: per-cell layer index, edge mask, resolved ground tile, and resolved transition tile
@perf: hot path; centralizes ground/transition tile selection to remove duplicated sync/coroutine terrain write logic
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: runtime ground conversion, transition tilemap usage, and terrain ruleset settings in ProceduralEnvironment
@assets: ground and transition tiles selected from terrain layers
@notes: keep this payload renderer-agnostic so tilemaps, far-view, and future shader paths can share one ground decision layer
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDRENDERPAYLOAD]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderPayload.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private struct GroundRenderCellDecision
        {
            public int GlobalIndex;
            public int Col;
            public int Row;
            public int LayerIndex;
            public int NeighborMask;
            public TileBase GroundTile;
            public TileBase TransitionTile;
        }

        private GroundRenderCellDecision BuildGroundRenderDecision(
            int width,
            int height,
            bool skipBase,
            bool useTransition,
            List<TerrainLayer> layers,
            Dictionary<int, TileBase>[] edgeLookup,
            List<HexMaskTile>[] edgeByBits,
            int tileSeed,
            int[] layerIndex,
            HexTerrainRuleset ruleset,
            int col,
            int row,
            int globalIdx)
        {
            int layerIdx = layerIndex[globalIdx];
            var decision = new GroundRenderCellDecision
            {
                GlobalIndex = globalIdx,
                Col = col,
                Row = row,
                LayerIndex = layerIdx
            };

            if (layerIdx < 0 || layerIdx >= layers.Count)
                return decision;

            var layer = layers[layerIdx];
            TileBase baseTile = null;
            if (!skipBase)
            {
                baseTile = PickLayerBaseTile(layer, col, row, tileSeed);
                if (ConvertGroundTilesRuntime)
                    baseTile = GetOrCreateRuntimeGroundTile(baseTile);
            }

            TileBase edgeTile = null;
            if (layer != null && layer.EdgeTiles != null && layer.EdgeTiles.Count > 0)
            {
                int mask = BuildNeighborMask(col, row, width, height, layerIdx, layerIndex, ruleset);
                decision.NeighborMask = mask;
                if (mask != 0)
                    edgeTile = PickEdgeTile(mask, layer, edgeLookup[layerIdx], edgeByBits[layerIdx]);
            }

            if (useTransition)
            {
                decision.GroundTile = skipBase ? null : baseTile;
                decision.TransitionTile = edgeTile;
            }
            else
            {
                decision.GroundTile = skipBase ? edgeTile : (edgeTile ?? baseTile);
            }

            return decision;
        }

        private void PopulateGroundRenderDecisionBlock(
            int width,
            int height,
            bool skipBase,
            bool useTransition,
            List<TerrainLayer> layers,
            Dictionary<int, TileBase>[] edgeLookup,
            List<HexMaskTile>[] edgeByBits,
            int tileSeed,
            int[] layerIndex,
            HexTerrainRuleset ruleset,
            int startRow,
            int rowCount,
            GroundRenderCellDecision[] decisions)
        {
            int blockIndex = 0;
            for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
            {
                int row = startRow + rowOffset;
                for (int col = 0; col < width; col++)
                {
                    int globalIdx = (row * width) + col;
                    decisions[blockIndex++] = BuildGroundRenderDecision(
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
                        col,
                        row,
                        globalIdx);
                }
            }
        }

        private void ApplyGroundRenderDecisionBlock(
            bool skipBase,
            bool useTransition,
            GroundRenderCellDecision[] decisions,
            TileBase[] groundTiles,
            TileBase[] transitionTiles)
        {
            for (int i = 0; i < decisions.Length; i++)
            {
                var decision = decisions[i];
                if (useTransition)
                {
                    if (!skipBase && groundTiles != null)
                        groundTiles[i] = decision.GroundTile;
                    if (transitionTiles != null)
                        transitionTiles[i] = decision.TransitionTile;
                }
                else if (groundTiles != null)
                {
                    groundTiles[i] = decision.GroundTile;
                }
            }
        }

        private void WriteGroundRenderDecisionOverlay(GroundRenderCellDecision[] decisions)
        {
            if (_ground == null || decisions == null)
                return;

            for (int i = 0; i < decisions.Length; i++)
            {
                var decision = decisions[i];
                if (decision.GroundTile == null)
                    continue;

                _ground.SetTile(new Vector3Int(decision.Col, decision.Row, 0), decision.GroundTile);
            }
        }

        private void WriteGroundRenderDecisionBlock(
            int width,
            int startRow,
            int rowCount,
            bool skipBase,
            bool useTransition,
            TileBase[] groundTiles,
            TileBase[] transitionTiles)
        {
            var bounds = new BoundsInt(0, startRow, 0, width, rowCount, 1);
            if (groundTiles != null && (!skipBase || !useTransition))
                _ground.SetTilesBlock(bounds, groundTiles);
            if (transitionTiles != null && _transitions != null)
                _transitions.SetTilesBlock(bounds, transitionTiles);
        }
    }
}
