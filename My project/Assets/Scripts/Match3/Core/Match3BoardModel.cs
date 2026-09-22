using System;
using System.Collections.Generic;
using Match3.Random;

namespace Match3.Core
{
    /// <summary>
    /// Pure C# domain model for the Match-3 grid.
    /// Decoupled from all Unity Engine systems (MonoBehaviour, GameObject, Transform, UnitCombat).
    /// Executes swaps as atomic transactions and returns deeply immutable BoardResolution instances.
    /// 
    /// Coordinate Convention:
    /// - (0, 0) is Bottom-Left.
    /// - X increases to the Right (0 .. Width - 1).
    /// - Y increases Upwards (0 .. Height - 1).
    /// - Gravity: Tiles drop from higher Y to lower Y within each column.
    /// - Canonical Ordering: Y ascending, then X ascending.
    /// </summary>
    public sealed class Match3BoardModel
    {
        private readonly Match3BoardConfig _config;
        private readonly IMatch3Random _random;
        private readonly Match3Tile[,] _grid;

        public Match3BoardConfig Config => _config;
        public int Width => _config.Width;
        public int Height => _config.Height;
        public IMatch3Random Random => _random;

        public Match3BoardModel(Match3BoardConfig config = null, IMatch3Random random = null)
        {
            _config = config ?? Match3BoardConfig.Default;
            if (_config.Width < 3 || _config.Height < 3)
                throw new ArgumentException("Board dimensions must be at least 3x3.");
            if (_config.ColorCount < 3)
                throw new ArgumentException("ColorCount must be at least 3.");

            _random = random ?? new SeededMatch3Random(Environment.TickCount);
            _grid = new Match3Tile[_config.Width, _config.Height];

            InitializeBoard();
        }

        public Match3Tile GetTile(BoardPosition pos)
        {
            if (!IsInBounds(pos))
                throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of bounds ({Width}x{Height}).");
            return _grid[pos.X, pos.Y];
        }

        public Match3Tile GetTile(int x, int y) => GetTile(new BoardPosition(x, y));

        public bool IsInBounds(BoardPosition pos) =>
            pos.X >= 0 && pos.X < _config.Width && pos.Y >= 0 && pos.Y < _config.Height;

        public bool IsInBounds(int x, int y) =>
            x >= 0 && x < _config.Width && y >= 0 && y < _config.Height;

        /// <summary>
        /// Explicit tile setter used for deterministic unit test setups.
        /// </summary>
        public void SetTile(BoardPosition pos, Match3Tile tile)
        {
            if (!IsInBounds(pos))
                throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of bounds.");
            _grid[pos.X, pos.Y] = tile;
        }

        /// <summary>
        /// Populates the board with zero initial matches and ensures at least one legal move exists.
        /// Bounded by MaxGenerationAttempts to avoid unbounded loops.
        /// </summary>
        public void InitializeBoard()
        {
            int attempts = 0;
            while (attempts++ < _config.MaxGenerationAttempts)
            {
                GenerateInitialTiles();

                if (FindMatches().Count == 0 && HasAnyLegalMove())
                    return;
            }

            // Controlled deterministic fallback if random sampling did not settle
            EmergencyDeterministicRegenerate();
        }

        private void GenerateInitialTiles()
        {
            for (int y = 0; y < _config.Height; y++)
            {
                for (int x = 0; x < _config.Width; x++)
                {
                    List<TileColor> validColors = GetColorsWithoutImmediateMatch(x, y);
                    TileColor chosenColor = validColors[_random.Next(0, validColors.Count)];
                    _grid[x, y] = new Match3Tile(chosenColor);
                }
            }
        }

        private List<TileColor> GetColorsWithoutImmediateMatch(int x, int y)
        {
            List<TileColor> allowed = new List<TileColor>(_config.ColorCount);
            for (int c = 0; c < _config.ColorCount; c++)
                allowed.Add((TileColor)c);

            // Avoid horizontal 3-in-a-row
            if (x >= 2)
            {
                TileColor c1 = _grid[x - 1, y].Color;
                TileColor c2 = _grid[x - 2, y].Color;
                if (c1 == c2)
                    allowed.Remove(c1);
            }

            // Avoid vertical 3-in-a-row
            if (y >= 2)
            {
                TileColor c1 = _grid[x, y - 1].Color;
                TileColor c2 = _grid[x, y - 2].Color;
                if (c1 == c2)
                    allowed.Remove(c1);
            }

            if (allowed.Count == 0)
            {
                for (int c = 0; c < _config.ColorCount; c++)
                    allowed.Add((TileColor)c);
            }

            return allowed;
        }

        /// <summary>
        /// Attempts an atomic swap between positions A and B.
        /// If no match forms and neither tile is a special, rolls back and returns Invalid.
        /// If valid, executes the complete cascade sequence (Clear, Gravity, Refill) until stable.
        /// </summary>
        public BoardResolution TrySwap(BoardPosition a, BoardPosition b)
        {
            if (!IsInBounds(a) || !IsInBounds(b))
                return BoardResolution.Invalid(a, b);

            if (!a.IsOrthogonalNeighbor(b))
                return BoardResolution.Invalid(a, b);

            Match3Tile tileA = _grid[a.X, a.Y];
            Match3Tile tileB = _grid[b.X, b.Y];

            if (!tileA.IsOccupied || !tileB.IsOccupied)
                return BoardResolution.Invalid(a, b);

            bool isSpecialSwap = tileA.Special != TileSpecial.None || tileB.Special != TileSpecial.None;

            // Perform internal swap
            _grid[a.X, a.Y] = tileB;
            _grid[b.X, b.Y] = tileA;

            IReadOnlyList<MatchGroup> initialMatches = FindMatches();

            // If neither tile was special and no match formed, rollback swap
            if (!isSpecialSwap && initialMatches.Count == 0)
            {
                _grid[a.X, a.Y] = tileA;
                _grid[b.X, b.Y] = tileB;
                return BoardResolution.Invalid(a, b);
            }

            List<ResolutionStep> steps = new List<ResolutionStep>();

            // Step 0: Swap execution
            steps.Add(new ResolutionStep(
                ResolutionStepType.Swap,
                moves: new[] { new TileMove(a, b), new TileMove(b, a) }
            ));

            int cascadeCount = 0;
            bool maxCascadeDepthExceeded = false;
            bool isFirstStep = true;

            while (true)
            {
                if (cascadeCount >= _config.MaxCascadeDepth)
                {
                    maxCascadeDepthExceeded = true;
                    if (_config.ThrowOnMaxCascadeDepthExceeded)
                    {
                        throw new InvalidOperationException(
                            $"Match3BoardModel cascade limit ({_config.MaxCascadeDepth}) exceeded on swap {a} -> {b}."
                        );
                    }
                    EmergencyStabilizeBoard();
                    break;
                }

                var context = new SpecialResolutionContext();

                if (isFirstStep && isSpecialSwap)
                {
                    ResolveSpecialSwap(a, b, tileA, tileB, context);
                }
                else
                {
                    var currentMatches = FindMatches();
                    if (currentMatches.Count == 0)
                        break; // Board reached stable equilibrium

                    // Sort matches canonically by their first position
                    var sortedMatches = new List<MatchGroup>(currentMatches);
                    sortedMatches.Sort((m1, m2) => m1.Positions[0].CompareTo(m2.Positions[0]));

                    foreach (var group in sortedMatches)
                    {
                        TileSpecial specialToSpawn = ClassifySpecial(group);
                        BoardPosition? anchor = null;

                        if (specialToSpawn != TileSpecial.None)
                        {
                            anchor = DetermineAnchor(group, a, b, isFirstStep);
                            _grid[anchor.Value.X, anchor.Value.Y] = new Match3Tile(group.Color, specialToSpawn);
                            context.SpecialSpawns.Add(new SpecialSpawn(anchor.Value, specialToSpawn, group.Color));
                            context.NewlySpawnedAnchors.Add(anchor.Value);
                        }

                        foreach (var pos in group.Positions)
                        {
                            if (anchor.HasValue && pos == anchor.Value)
                                continue; // Newly spawned special is preserved at anchor

                            Match3Tile tileAtPos = _grid[pos.X, pos.Y];
                            if (tileAtPos.Special != TileSpecial.None && !context.Visited.Contains(pos))
                            {
                                context.Visited.Add(pos);
                                context.ActivationQueue.Enqueue(pos);
                            }

                            if (!context.ClearedTiles.ContainsKey(pos))
                            {
                                context.ClearedTiles[pos] = new ClearedTile(pos, tileAtPos, ClearCause.NormalMatch);
                            }
                        }
                    }
                }

                // Drain activation queue (chain reactions resolved before physical board mutation)
                ProcessActivationQueue(context);

                if (context.ClearedTiles.Count == 0 && context.SpecialSpawns.Count == 0)
                    break;

                // Mutate internal grid once for all cleared tiles
                foreach (var ct in context.ClearedTiles.Values)
                {
                    _grid[ct.Position.X, ct.Position.Y] = Match3Tile.Empty;
                }

                // Step 1: Record MatchAndClear
                steps.Add(new ResolutionStep(
                    ResolutionStepType.MatchAndClear,
                    clearedTiles: context.ClearedTiles.Values,
                    specials: context.SpecialSpawns,
                    activations: context.Activations,
                    matches: isFirstStep && isSpecialSwap ? null : FindMatches()
                ));

                // Step 2: Gravity
                List<TileMove> dropMoves = new List<TileMove>();
                ApplyGravity(dropMoves);

                if (dropMoves.Count > 0)
                {
                    steps.Add(new ResolutionStep(
                        ResolutionStepType.Gravity,
                        moves: dropMoves
                    ));
                }

                // Step 3: Refill
                List<TileSpawn> newSpawns = new List<TileSpawn>();
                RefillEmptyCells(newSpawns);

                steps.Add(new ResolutionStep(
                    ResolutionStepType.Refill,
                    spawns: newSpawns
                ));

                isFirstStep = false;
                cascadeCount++;
            }

            // Check if stable board has any remaining legal moves
            bool autoShuffled = false;
            if (!HasAnyLegalMove())
            {
                AutoShuffle();
                autoShuffled = true;
                steps.Add(new ResolutionStep(ResolutionStepType.AutoShuffle));
            }

            return new BoardResolution(
                isValidMove: true,
                from: a,
                to: b,
                steps: steps,
                cascadeCount: cascadeCount,
                boardAutoShuffled: autoShuffled,
                maxCascadeDepthExceeded: maxCascadeDepthExceeded
            );
        }

        private void ResolveSpecialSwap(
            BoardPosition a,
            BoardPosition b,
            Match3Tile tileA,
            Match3Tile tileB,
            SpecialResolutionContext context)
        {
            // Note: internal swap has already moved tileB to A and tileA to B
            bool bothSpecials = tileA.Special != TileSpecial.None && tileB.Special != TileSpecial.None;

            if (bothSpecials)
            {
                // Combo detonates at destination B
                context.ClearedTiles[a] = new ClearedTile(a, _grid[a.X, a.Y], ClearCause.SpecialCombo);
                context.ClearedTiles[b] = new ClearedTile(b, _grid[b.X, b.Y], ClearCause.SpecialCombo);
                context.Visited.Add(a);
                context.Visited.Add(b);

                bool isRocketA = tileA.Special == TileSpecial.RocketHorizontal || tileA.Special == TileSpecial.RocketVertical;
                bool isRocketB = tileB.Special == TileSpecial.RocketHorizontal || tileB.Special == TileSpecial.RocketVertical;
                bool isDynamiteA = tileA.Special == TileSpecial.Dynamite;
                bool isDynamiteB = tileB.Special == TileSpecial.Dynamite;
                bool isAirstrikeA = tileA.Special == TileSpecial.Airstrike;
                bool isAirstrikeB = tileB.Special == TileSpecial.Airstrike;

                if (isRocketA && isRocketB)
                {
                    // Rocket + Rocket: row + column cross
                    var affected = new List<BoardPosition>();
                    for (int x = 0; x < _config.Width; x++) affected.Add(new BoardPosition(x, b.Y));
                    for (int y = 0; y < _config.Height; y++) affected.Add(new BoardPosition(b.X, y));
                    AddAffectedToContext(affected, ClearCause.SpecialCombo, context);
                    context.Activations.Add(new SpecialActivation(b, TileSpecial.RocketHorizontal, TileColor.None, affected));
                }
                else if ((isRocketA && isDynamiteB) || (isDynamiteA && isRocketB))
                {
                    // Rocket + Dynamite: 3 rows + 3 columns centered at B
                    var affected = new List<BoardPosition>();
                    for (int r = b.Y - 1; r <= b.Y + 1; r++)
                    {
                        if (r >= 0 && r < _config.Height)
                        {
                            for (int x = 0; x < _config.Width; x++) affected.Add(new BoardPosition(x, r));
                        }
                    }
                    for (int c = b.X - 1; c <= b.X + 1; c++)
                    {
                        if (c >= 0 && c < _config.Width)
                        {
                            for (int y = 0; y < _config.Height; y++) affected.Add(new BoardPosition(c, y));
                        }
                    }
                    AddAffectedToContext(affected, ClearCause.SpecialCombo, context);
                    context.Activations.Add(new SpecialActivation(b, TileSpecial.Dynamite, TileColor.None, affected));
                }
                else if (isDynamiteA && isDynamiteB)
                {
                    // Dynamite + Dynamite: 5x5 square (Chebyshev distance <= 2)
                    var affected = new List<BoardPosition>();
                    for (int y = b.Y - 2; y <= b.Y + 2; y++)
                    {
                        for (int x = b.X - 2; x <= b.X + 2; x++)
                        {
                            var p = new BoardPosition(x, y);
                            if (IsInBounds(p)) affected.Add(p);
                        }
                    }
                    AddAffectedToContext(affected, ClearCause.SpecialCombo, context);
                    context.Activations.Add(new SpecialActivation(b, TileSpecial.Dynamite, TileColor.None, affected));
                }
                else if (isAirstrikeA && isAirstrikeB)
                {
                    // Airstrike + Airstrike: clear complete board
                    var affected = new List<BoardPosition>();
                    for (int y = 0; y < _config.Height; y++)
                    {
                        for (int x = 0; x < _config.Width; x++)
                        {
                            if (_grid[x, y].IsOccupied) affected.Add(new BoardPosition(x, y));
                        }
                    }
                    AddAffectedToContext(affected, ClearCause.SpecialCombo, context);
                    context.Activations.Add(new SpecialActivation(b, TileSpecial.Airstrike, TileColor.None, affected));
                }
                else if (isAirstrikeA || isAirstrikeB)
                {
                    // Airstrike + Special: transform all tiles of target color into that special and activate
                    TileSpecial otherSpecial = isAirstrikeA ? tileB.Special : tileA.Special;
                    TileColor targetColor = isAirstrikeA ? tileB.Color : tileA.Color;

                    var affected = new List<BoardPosition>();
                    bool toggleH = true;
                    for (int y = 0; y < _config.Height; y++)
                    {
                        for (int x = 0; x < _config.Width; x++)
                        {
                            if (_grid[x, y].IsOccupied && _grid[x, y].Color == targetColor)
                            {
                                var p = new BoardPosition(x, y);
                                TileSpecial spawnedSpecial = otherSpecial;
                                if (otherSpecial == TileSpecial.RocketHorizontal || otherSpecial == TileSpecial.RocketVertical)
                                {
                                    spawnedSpecial = toggleH ? TileSpecial.RocketHorizontal : TileSpecial.RocketVertical;
                                    toggleH = !toggleH;
                                }

                                _grid[x, y] = new Match3Tile(targetColor, spawnedSpecial);
                                affected.Add(p);
                                if (!context.Visited.Contains(p))
                                {
                                    context.Visited.Add(p);
                                    context.ActivationQueue.Enqueue(p);
                                }
                            }
                        }
                    }
                    context.Activations.Add(new SpecialActivation(b, TileSpecial.Airstrike, targetColor, affected));
                }
            }
            else if (tileA.Special == TileSpecial.Airstrike || tileB.Special == TileSpecial.Airstrike)
            {
                // Airstrike + Normal: clears all tiles of normal tile's color
                TileColor targetColor = tileA.Special == TileSpecial.Airstrike ? tileB.Color : tileA.Color;
                context.ClearedTiles[a] = new ClearedTile(a, _grid[a.X, a.Y], ClearCause.Airstrike);
                context.ClearedTiles[b] = new ClearedTile(b, _grid[b.X, b.Y], ClearCause.Airstrike);
                context.Visited.Add(a);
                context.Visited.Add(b);

                var affected = new List<BoardPosition>();
                for (int y = 0; y < _config.Height; y++)
                {
                    for (int x = 0; x < _config.Width; x++)
                    {
                        if (_grid[x, y].IsOccupied && _grid[x, y].Color == targetColor)
                        {
                            affected.Add(new BoardPosition(x, y));
                        }
                    }
                }
                AddAffectedToContext(affected, ClearCause.Airstrike, context);
                context.Activations.Add(new SpecialActivation(b, TileSpecial.Airstrike, targetColor, affected));
            }
            else
            {
                // Single Rocket / Dynamite swapped with normal tile:
                // Special is now at B, normal tile is now at A
                var matches = FindMatches();
                if (matches.Count > 0)
                {
                    foreach (var group in matches)
                    {
                        foreach (var p in group.Positions)
                        {
                            if (!context.ClearedTiles.ContainsKey(p))
                                context.ClearedTiles[p] = new ClearedTile(p, _grid[p.X, p.Y], ClearCause.NormalMatch);
                        }
                    }
                }

                if (!context.Visited.Contains(b))
                {
                    context.Visited.Add(b);
                    context.ActivationQueue.Enqueue(b);
                }
            }
        }

        private void ProcessActivationQueue(SpecialResolutionContext context)
        {
            while (context.ActivationQueue.Count > 0)
            {
                BoardPosition pos = context.ActivationQueue.Dequeue();
                Match3Tile tile = _grid[pos.X, pos.Y];

                if (tile.Special == TileSpecial.None && context.ClearedTiles.TryGetValue(pos, out var ct))
                    tile = ct.Tile;

                if (tile.Special == TileSpecial.None)
                    continue;

                List<BoardPosition> affected = GetAffectedPositions(pos, tile.Special, tile.Color);
                ClearCause cause = GetClearCauseForSpecial(tile.Special);

                context.Activations.Add(new SpecialActivation(pos, tile.Special, tile.Color, affected));
                AddAffectedToContext(affected, cause, context);
            }
        }

        private List<BoardPosition> GetAffectedPositions(BoardPosition origin, TileSpecial special, TileColor color)
        {
            var affected = new List<BoardPosition>();

            switch (special)
            {
                case TileSpecial.RocketHorizontal:
                    for (int x = 0; x < _config.Width; x++)
                        affected.Add(new BoardPosition(x, origin.Y));
                    break;

                case TileSpecial.RocketVertical:
                    for (int y = 0; y < _config.Height; y++)
                        affected.Add(new BoardPosition(origin.X, y));
                    break;

                case TileSpecial.Dynamite:
                    // Chebyshev distance <= 1 (3x3 square)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = origin.X + dx;
                            int ny = origin.Y + dy;
                            if (IsInBounds(nx, ny))
                                affected.Add(new BoardPosition(nx, ny));
                        }
                    }
                    break;

                case TileSpecial.Airstrike:
                    for (int y = 0; y < _config.Height; y++)
                    {
                        for (int x = 0; x < _config.Width; x++)
                        {
                            if (_grid[x, y].IsOccupied && _grid[x, y].Color == color)
                                affected.Add(new BoardPosition(x, y));
                        }
                    }
                    break;
            }

            return affected;
        }

        private void AddAffectedToContext(List<BoardPosition> affected, ClearCause cause, SpecialResolutionContext context)
        {
            foreach (var p in affected)
            {
                if (!IsInBounds(p)) continue;
                if (!_grid[p.X, p.Y].IsOccupied && !context.NewlySpawnedAnchors.Contains(p)) continue;

                Match3Tile tile = _grid[p.X, p.Y];
                if (!context.ClearedTiles.ContainsKey(p))
                {
                    context.ClearedTiles[p] = new ClearedTile(p, tile, cause);
                }

                // Secondary special triggered in chain reaction
                if (tile.Special != TileSpecial.None && !context.Visited.Contains(p))
                {
                    context.Visited.Add(p);
                    context.ActivationQueue.Enqueue(p);
                }
            }
        }

        private static ClearCause GetClearCauseForSpecial(TileSpecial special)
        {
            switch (special)
            {
                case TileSpecial.RocketHorizontal:
                case TileSpecial.RocketVertical:
                    return ClearCause.Rocket;
                case TileSpecial.Dynamite:
                    return ClearCause.Dynamite;
                case TileSpecial.Airstrike:
                    return ClearCause.Airstrike;
                default:
                    return ClearCause.NormalMatch;
            }
        }

        private static TileSpecial ClassifySpecial(MatchGroup group)
        {
            // Precedence: Intersection (Dynamite) > Line5Plus (Airstrike) > Line4 (Rocket) > Line3 (None)
            if (group.Shape == MatchShape.Intersection)
                return TileSpecial.Dynamite;

            if (group.Shape == MatchShape.Line5Plus)
                return TileSpecial.Airstrike;

            if (group.Shape == MatchShape.Line4)
                return group.IsHorizontal ? TileSpecial.RocketHorizontal : TileSpecial.RocketVertical;

            return TileSpecial.None;
        }

        private static BoardPosition DetermineAnchor(
            MatchGroup group,
            BoardPosition swapFrom,
            BoardPosition swapTo,
            bool isPlayerSwap)
        {
            // 1. Intersection coordinate if shape == Intersection
            if (group.Shape == MatchShape.Intersection && group.IntersectionPoint.HasValue && ContainsPosition(group.Positions, group.IntersectionPoint.Value))
            {
                return group.IntersectionPoint.Value;
            }

            // 2. Destination cell B if belongs to group
            if (isPlayerSwap && ContainsPosition(group.Positions, swapTo))
            {
                return swapTo;
            }

            // 3. Source cell A if belongs to group
            if (isPlayerSwap && ContainsPosition(group.Positions, swapFrom))
            {
                return swapFrom;
            }

            // 4 & 5. Lowest Y, then Lowest X (already sorted canonically)
            return group.Positions[0];
        }

        private static bool ContainsPosition(IReadOnlyList<BoardPosition> list, BoardPosition target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return true;
            }
            return false;
        }

        private void ApplyGravity(List<TileMove> moves)
        {
            for (int x = 0; x < _config.Width; x++)
            {
                int writeY = 0;
                for (int readY = 0; readY < _config.Height; readY++)
                {
                    if (_grid[x, readY].IsOccupied)
                    {
                        if (writeY != readY)
                        {
                            var tile = _grid[x, readY];
                            _grid[x, writeY] = tile;
                            _grid[x, readY] = Match3Tile.Empty;
                            moves.Add(new TileMove(new BoardPosition(x, readY), new BoardPosition(x, writeY)));
                        }
                        writeY++;
                    }
                }
            }
        }

        private void RefillEmptyCells(List<TileSpawn> spawns)
        {
            for (int x = 0; x < _config.Width; x++)
            {
                for (int y = 0; y < _config.Height; y++)
                {
                    if (!_grid[x, y].IsOccupied)
                    {
                        TileColor newColor = (TileColor)_random.Next(0, _config.ColorCount);
                        var newTile = new Match3Tile(newColor);
                        _grid[x, y] = newTile;
                        spawns.Add(new TileSpawn(new BoardPosition(x, y), newTile));
                    }
                }
            }
        }

        /// <summary>
        /// Scans horizontal and vertical runs and merges intersecting matches of the same color.
        /// Guaranteed canonical ordering of results.
        /// </summary>
        public IReadOnlyList<MatchGroup> FindMatches()
        {
            List<List<BoardPosition>> rawLines = new List<List<BoardPosition>>();
            List<TileColor> lineColors = new List<TileColor>();
            List<bool> lineIsHorizontal = new List<bool>();

            // 1. Scan Horizontal runs
            for (int y = 0; y < _config.Height; y++)
            {
                int x = 0;
                while (x < _config.Width)
                {
                    if (!_grid[x, y].IsOccupied)
                    {
                        x++;
                        continue;
                    }

                    TileColor color = _grid[x, y].Color;
                    int startX = x;
                    while (x < _config.Width && _grid[x, y].IsOccupied && _grid[x, y].Color == color)
                    {
                        x++;
                    }

                    int runLength = x - startX;
                    if (runLength >= _config.MinimumMatchLength)
                    {
                        var line = new List<BoardPosition>(runLength);
                        for (int k = startX; k < x; k++)
                            line.Add(new BoardPosition(k, y));

                        rawLines.Add(line);
                        lineColors.Add(color);
                        lineIsHorizontal.Add(true);
                    }
                }
            }

            // 2. Scan Vertical runs
            for (int x = 0; x < _config.Width; x++)
            {
                int y = 0;
                while (y < _config.Height)
                {
                    if (!_grid[x, y].IsOccupied)
                    {
                        y++;
                        continue;
                    }

                    TileColor color = _grid[x, y].Color;
                    int startY = y;
                    while (y < _config.Height && _grid[x, y].IsOccupied && _grid[x, y].Color == color)
                    {
                        y++;
                    }

                    int runLength = y - startY;
                    if (runLength >= _config.MinimumMatchLength)
                    {
                        var line = new List<BoardPosition>(runLength);
                        for (int k = startY; k < y; k++)
                            line.Add(new BoardPosition(x, k));

                        rawLines.Add(line);
                        lineColors.Add(color);
                        lineIsHorizontal.Add(false);
                    }
                }
            }

            if (rawLines.Count == 0)
                return Array.Empty<MatchGroup>();

            // 3. Merge connected / intersecting lines of identical color
            List<MatchGroup> mergedGroups = new List<MatchGroup>();
            bool[] visited = new bool[rawLines.Count];

            for (int i = 0; i < rawLines.Count; i++)
            {
                if (visited[i]) continue;
                visited[i] = true;

                TileColor groupColor = lineColors[i];
                HashSet<BoardPosition> groupPositions = new HashSet<BoardPosition>(rawLines[i]);
                bool hasIntersection = false;
                BoardPosition? intersectionPoint = null;
                bool isHorizontal = lineIsHorizontal[i];

                bool expanded = true;
                while (expanded)
                {
                    expanded = false;
                    for (int j = 0; j < rawLines.Count; j++)
                    {
                        if (visited[j] || lineColors[j] != groupColor) continue;

                        BoardPosition sharedPos = default;
                        bool intersects = false;
                        foreach (var pos in rawLines[j])
                        {
                            if (groupPositions.Contains(pos))
                            {
                                intersects = true;
                                sharedPos = pos;
                                break;
                            }
                        }

                        if (intersects)
                        {
                            visited[j] = true;
                            hasIntersection = true;
                            if (!intersectionPoint.HasValue)
                                intersectionPoint = sharedPos;

                            foreach (var p in rawLines[j])
                                groupPositions.Add(p);
                            expanded = true;
                        }
                    }
                }

                // Precedence: Intersection > Line5Plus > Line4 > Line3
                MatchShape shape;
                if (hasIntersection)
                {
                    shape = MatchShape.Intersection;
                }
                else if (groupPositions.Count >= 5)
                {
                    shape = MatchShape.Line5Plus;
                }
                else if (groupPositions.Count == 4)
                {
                    shape = MatchShape.Line4;
                }
                else
                {
                    shape = MatchShape.Line3;
                }

                mergedGroups.Add(new MatchGroup(groupColor, groupPositions, shape, isHorizontal, intersectionPoint));
            }

            // Sort groups canonically by their primary anchor
            mergedGroups.Sort((g1, g2) => g1.Positions[0].CompareTo(g2.Positions[0]));
            return mergedGroups;
        }

        /// <summary>
        /// Checks if at least one valid orthogonal swap exists on the current board.
        /// Strictly non-mutating: does NOT alter board cells or random generator state.
        /// </summary>
        public bool HasAnyLegalMove()
        {
            for (int y = 0; y < _config.Height; y++)
            {
                for (int x = 0; x < _config.Width; x++)
                {
                    var current = new BoardPosition(x, y);
                    var tile = _grid[x, y];
                    if (!tile.IsOccupied) continue;

                    // A special tile can always be swapped with an occupied orthogonal neighbor
                    if (tile.Special != TileSpecial.None)
                    {
                        if (x < _config.Width - 1 && _grid[x + 1, y].IsOccupied) return true;
                        if (y < _config.Height - 1 && _grid[x, y + 1].IsOccupied) return true;
                        if (x > 0 && _grid[x - 1, y].IsOccupied) return true;
                        if (y > 0 && _grid[x, y - 1].IsOccupied) return true;
                    }

                    // Check right neighbor
                    if (x < _config.Width - 1)
                    {
                        var right = new BoardPosition(x + 1, y);
                        if (SimulateSwapHasMatch(current, right))
                            return true;
                    }

                    // Check up neighbor
                    if (y < _config.Height - 1)
                    {
                        var up = new BoardPosition(x, y + 1);
                        if (SimulateSwapHasMatch(current, up))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool SimulateSwapHasMatch(BoardPosition a, BoardPosition b)
        {
            Match3Tile tileA = _grid[a.X, a.Y];
            Match3Tile tileB = _grid[b.X, b.Y];

            if (!tileA.IsOccupied || !tileB.IsOccupied)
                return false;

            // Swapping a special is always legal
            if (tileA.Special != TileSpecial.None || tileB.Special != TileSpecial.None)
                return true;

            if (tileA.Color == tileB.Color)
                return false;

            // Temporary swap without touching random
            _grid[a.X, a.Y] = tileB;
            _grid[b.X, b.Y] = tileA;

            bool hasMatch = HasMatchAt(a) || HasMatchAt(b);

            // Revert swap
            _grid[a.X, a.Y] = tileA;
            _grid[b.X, b.Y] = tileB;

            return hasMatch;
        }

        public bool HasMatchAt(BoardPosition pos)
        {
            if (!IsInBounds(pos)) return false;
            if (!_grid[pos.X, pos.Y].IsOccupied) return false;
            TileColor color = _grid[pos.X, pos.Y].Color;

            // Horizontal count
            int hCount = 1;
            for (int x = pos.X - 1; x >= 0 && _grid[x, pos.Y].IsOccupied && _grid[x, pos.Y].Color == color; x--) hCount++;
            for (int x = pos.X + 1; x < _config.Width && _grid[x, pos.Y].IsOccupied && _grid[x, pos.Y].Color == color; x++) hCount++;
            if (hCount >= _config.MinimumMatchLength) return true;

            // Vertical count
            int vCount = 1;
            for (int y = pos.Y - 1; y >= 0 && _grid[pos.X, y].IsOccupied && _grid[pos.X, y].Color == color; y--) vCount++;
            for (int y = pos.Y + 1; y < _config.Height && _grid[pos.X, y].IsOccupied && _grid[pos.X, y].Color == color; y++) vCount++;
            return vCount >= _config.MinimumMatchLength;
        }

        /// <summary>
        /// Free automatic board reshuffle when no legal moves remain.
        /// Strictly bounded by MaxShuffleAttempts to prevent infinite loops.
        /// </summary>
        public void AutoShuffle()
        {
            List<Match3Tile> existingTiles = new List<Match3Tile>(_config.Width * _config.Height);
            for (int y = 0; y < _config.Height; y++)
            {
                for (int x = 0; x < _config.Width; x++)
                {
                    existingTiles.Add(_grid[x, y]);
                }
            }

            int attempts = 0;
            while (attempts++ < _config.MaxShuffleAttempts)
            {
                // Fisher-Yates shuffle
                for (int i = existingTiles.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(0, i + 1);
                    var temp = existingTiles[i];
                    existingTiles[i] = existingTiles[j];
                    existingTiles[j] = temp;
                }

                int idx = 0;
                for (int y = 0; y < _config.Height; y++)
                {
                    for (int x = 0; x < _config.Width; x++)
                    {
                        _grid[x, y] = existingTiles[idx++];
                    }
                }

                if (FindMatches().Count == 0 && HasAnyLegalMove())
                    return;
            }

            // Deterministic emergency fallback if shuffle attempts exhausted
            EmergencyDeterministicRegenerate();
        }

        private void EmergencyDeterministicRegenerate()
        {
            // Deterministic pattern: offset modulo guarantees no 3-in-a-row
            for (int y = 0; y < _config.Height; y++)
            {
                for (int x = 0; x < _config.Width; x++)
                {
                    int colorIdx = (x + (y * 2)) % _config.ColorCount;
                    _grid[x, y] = new Match3Tile((TileColor)colorIdx);
                }
            }

            // Guarantee legal moves by configuring a deterministic 2-1 setup at (0,0)
            // (0,0)=0, (1,0)=0, (2,0)=1, (2,1)=0. Swapping (2,0) with (2,1) creates a 3-match at Y=0.
            if (_config.Width >= 3 && _config.Height >= 2 && _config.ColorCount >= 2)
            {
                _grid[0, 0] = new Match3Tile((TileColor)0);
                _grid[1, 0] = new Match3Tile((TileColor)0);
                _grid[2, 0] = new Match3Tile((TileColor)1);
                _grid[2, 1] = new Match3Tile((TileColor)0);
            }
        }

        private void EmergencyStabilizeBoard()
        {
            EmergencyDeterministicRegenerate();
        }

        private sealed class SpecialResolutionContext
        {
            public readonly Queue<BoardPosition> ActivationQueue = new Queue<BoardPosition>();
            public readonly HashSet<BoardPosition> Visited = new HashSet<BoardPosition>();
            public readonly Dictionary<BoardPosition, ClearedTile> ClearedTiles = new Dictionary<BoardPosition, ClearedTile>();
            public readonly List<SpecialSpawn> SpecialSpawns = new List<SpecialSpawn>();
            public readonly List<SpecialActivation> Activations = new List<SpecialActivation>();
            public readonly HashSet<BoardPosition> NewlySpawnedAnchors = new HashSet<BoardPosition>();
        }
    }
}
