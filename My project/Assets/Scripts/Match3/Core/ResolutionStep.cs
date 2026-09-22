using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Match3.Core
{
    public enum ResolutionStepType : byte
    {
        Swap = 0,
        MatchAndClear = 1,
        Gravity = 2,
        Refill = 3,
        AutoShuffle = 4
    }

    /// <summary>
    /// An immutable discrete step within a full board resolution sequence.
    /// Uses ReadOnlyCollection to ensure deep immutability across external consumers.
    /// Encapsulates rich ClearedTile snapshots, activations, moves, spawns, and created specials.
    /// Automatically canonicalizes element orderings (Y ascending, then X ascending).
    /// </summary>
    public sealed class ResolutionStep
    {
        public ResolutionStepType Type { get; }
        public IReadOnlyList<ClearedTile> ClearedTiles { get; }
        public IReadOnlyList<BoardPosition> Cleared { get; }
        public IReadOnlyList<TileMove> Moves { get; }
        public IReadOnlyList<TileSpawn> Spawns { get; }
        public IReadOnlyList<SpecialSpawn> Specials { get; }
        public IReadOnlyList<SpecialActivation> Activations { get; }
        public IReadOnlyList<MatchGroup> Matches { get; }

        public ResolutionStep(
            ResolutionStepType type,
            IEnumerable<ClearedTile> clearedTiles = null,
            IEnumerable<BoardPosition> cleared = null,
            IEnumerable<TileMove> moves = null,
            IEnumerable<TileSpawn> spawns = null,
            IEnumerable<SpecialSpawn> specials = null,
            IEnumerable<SpecialActivation> activations = null,
            IEnumerable<MatchGroup> matches = null)
        {
            Type = type;

            var clearedTilesList = clearedTiles != null ? new List<ClearedTile>(clearedTiles) : new List<ClearedTile>();
            clearedTilesList.Sort();
            ClearedTiles = new ReadOnlyCollection<ClearedTile>(clearedTilesList);

            if (cleared != null)
            {
                var clearedList = new List<BoardPosition>(cleared);
                clearedList.Sort();
                Cleared = new ReadOnlyCollection<BoardPosition>(clearedList);
            }
            else if (clearedTilesList.Count > 0)
            {
                var positions = new List<BoardPosition>(clearedTilesList.Count);
                foreach (var ct in clearedTilesList)
                    positions.Add(ct.Position);
                Cleared = new ReadOnlyCollection<BoardPosition>(positions);
            }
            else
            {
                Cleared = (IReadOnlyList<BoardPosition>)Array.Empty<BoardPosition>();
            }

            var movesList = moves != null ? new List<TileMove>(moves) : new List<TileMove>();
            movesList.Sort();
            Moves = movesList.Count > 0 ? new ReadOnlyCollection<TileMove>(movesList) : (IReadOnlyList<TileMove>)Array.Empty<TileMove>();

            var spawnsList = spawns != null ? new List<TileSpawn>(spawns) : new List<TileSpawn>();
            spawnsList.Sort();
            Spawns = spawnsList.Count > 0 ? new ReadOnlyCollection<TileSpawn>(spawnsList) : (IReadOnlyList<TileSpawn>)Array.Empty<TileSpawn>();

            var specialsList = specials != null ? new List<SpecialSpawn>(specials) : new List<SpecialSpawn>();
            specialsList.Sort();
            Specials = specialsList.Count > 0 ? new ReadOnlyCollection<SpecialSpawn>(specialsList) : (IReadOnlyList<SpecialSpawn>)Array.Empty<SpecialSpawn>();

            var activationsList = activations != null ? new List<SpecialActivation>(activations) : new List<SpecialActivation>();
            Activations = activationsList.Count > 0 ? new ReadOnlyCollection<SpecialActivation>(activationsList) : (IReadOnlyList<SpecialActivation>)Array.Empty<SpecialActivation>();

            Matches = matches != null
                ? new ReadOnlyCollection<MatchGroup>(new List<MatchGroup>(matches))
                : (IReadOnlyList<MatchGroup>)Array.Empty<MatchGroup>();
        }
    }
}
