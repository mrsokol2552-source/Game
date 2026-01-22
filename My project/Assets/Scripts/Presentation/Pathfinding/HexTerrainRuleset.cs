using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Presentation.Pathfinding
{
    [CreateAssetMenu(menuName = "RTS/Environment/Hex Terrain Ruleset")]
    public class HexTerrainRuleset : ScriptableObject
    {
        [Header("Noise")]
        public bool UseRandomSeed = true;
        public int Seed = 12345;
        [Range(0.001f, 1f)]
        public float NoiseScale = 0.03f;
        public int Octaves = 3;
        [Range(0f, 1f)]
        public float Persistence = 0.5f;
        public float Lacunarity = 2f;
        public Vector2 NoiseOffset = Vector2.zero;
        public bool RandomizeNoiseOffset = true;

        [Header("Edges")]
        public bool PreferLowerNeighbors = true;
        public bool TreatOutOfBoundsAsLower = false;

        [Header("Layers")]
        public List<TerrainLayer> Layers = new List<TerrainLayer>();
    }

    [Serializable]
    public class TerrainLayer
    {
        public string Id = "Layer";
        [Range(0f, 1f)]
        public float MaxHeight = 1f;
        public TileBase[] BaseTiles;
        public TileBase DefaultEdgeTile;
        public List<HexMaskTile> EdgeTiles = new List<HexMaskTile>();
    }

    [Serializable]
    public class HexMaskTile
    {
        [Range(0, 63)]
        public int Mask = 0;
        public TileBase Tile;
    }
}
