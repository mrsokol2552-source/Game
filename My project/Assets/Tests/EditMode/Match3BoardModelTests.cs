/*
@file: My project/Assets/Tests/EditMode/Match3BoardModelTests.cs
@module: Match3/Tests
@purpose: Comprehensive unit tests and fuzz tests for pure C# Match3BoardModel v0.1
@entry: Match3BoardModelTests
@deps: Match3.Core, Match3.Random
@tests: EditMode
@notes: Validates board generation, match detection, swap rollback, cascades, gravity, and fuzz invariant.
*/

using System.Collections.Generic;
using NUnit.Framework;
using Match3.Core;
using Match3.Random;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Gate")]
    public class Match3BoardModelTests
    {
        [Test]
        public void GeneratedBoard_HasCorrectDimensions()
        {
            var config = new Match3BoardConfig { Width = 7, Height = 7 };
            var board = new Match3BoardModel(config, new SeededMatch3Random(42));

            Assert.AreEqual(7, board.Width);
            Assert.AreEqual(7, board.Height);
        }

        [Test]
        public void GeneratedBoard_HasNoInitialMatches()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(101));
            var matches = board.FindMatches();

            Assert.AreEqual(0, matches.Count, "Generated board must contain zero initial matches.");
        }

        [Test]
        public void GeneratedBoard_HasAtLeastOneLegalMove()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(202));

            Assert.IsTrue(board.HasAnyLegalMove(), "Generated board must have at least one legal swap available.");
        }

        [Test]
        public void Swap_NonAdjacentCells_IsRejected()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(303));
            var resolution = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(0, 2));

            Assert.IsFalse(resolution.IsValidMove, "Non-adjacent swap must be rejected.");
            Assert.AreEqual(0, resolution.Steps.Count);
        }

        [Test]
        public void Swap_DiagonalCells_IsRejected()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(404));
            var resolution = board.TrySwap(new BoardPosition(1, 1), new BoardPosition(2, 2));

            Assert.IsFalse(resolution.IsValidMove, "Diagonal swap must be rejected.");
            Assert.AreEqual(0, resolution.Steps.Count);
        }

        [Test]
        public void Swap_WithoutMatch_IsRolledBack()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(505));

            // Artificially configure two cells whose swap produces no match
            var posA = new BoardPosition(0, 0);
            var posB = new BoardPosition(1, 0);

            board.SetTile(posA, new Match3Tile(TileColor.Red));
            board.SetTile(posB, new Match3Tile(TileColor.Blue));
            // Surrounding cells prevent match
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Yellow));
            board.SetTile(new BoardPosition(0, 1), new Match3Tile(TileColor.Green));
            board.SetTile(new BoardPosition(1, 1), new Match3Tile(TileColor.Purple));

            var originalA = board.GetTile(posA);
            var originalB = board.GetTile(posB);

            var resolution = board.TrySwap(posA, posB);

            Assert.IsFalse(resolution.IsValidMove, "Swap without match must be rejected.");
            Assert.AreEqual(originalA, board.GetTile(posA), "Tile A must be rolled back to original position.");
            Assert.AreEqual(originalB, board.GetTile(posB), "Tile B must be rolled back to original position.");
        }

        [Test]
        public void Swap_CreatingHorizontalMatch_IsAccepted()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(606));

            // Setup: (0,0)=Red, (1,0)=Red, (2,0)=Blue, (2,1)=Red
            // Swapping (2,0) with (2,1) creates Red at (0,0), (1,0), (2,0)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var resolution = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));

            Assert.IsTrue(resolution.IsValidMove, "Valid horizontal match swap must be accepted.");
            Assert.GreaterOrEqual(resolution.Steps.Count, 3, "Must produce at least Swap, MatchAndClear, and Refill steps.");
        }

        [Test]
        public void Swap_CreatingVerticalMatch_IsAccepted()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(707));

            // Setup: (0,0)=Blue, (0,1)=Blue, (0,2)=Green, (1,2)=Blue
            // Swapping (0,2) with (1,2) creates vertical Blue line at (0,0), (0,1), (0,2)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(0, 1), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(0, 2), new Match3Tile(TileColor.Green));
            board.SetTile(new BoardPosition(1, 2), new Match3Tile(TileColor.Blue));

            var resolution = board.TrySwap(new BoardPosition(0, 2), new BoardPosition(1, 2));

            Assert.IsTrue(resolution.IsValidMove, "Valid vertical match swap must be accepted.");
            Assert.AreEqual(ResolutionStepType.Swap, resolution.Steps[0].Type);
        }

        private void FillBoardWithoutMatches(Match3BoardModel board, TileColor testColor)
        {
            TileColor c1 = TileColor.Blue;
            TileColor c2 = TileColor.Yellow;
            if (c1 == testColor) c1 = TileColor.Green;
            if (c2 == testColor || c2 == c1) c2 = TileColor.Purple;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    board.SetTile(new BoardPosition(x, y), new Match3Tile((x + y) % 2 == 0 ? c1 : c2));
                }
            }
        }

        [Test]
        public void MatchDetector_FindsThree()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(808));
            FillBoardWithoutMatches(board, TileColor.Red);

            board.SetTile(new BoardPosition(1, 3), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 3), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red));

            var matches = board.FindMatches();

            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(TileColor.Red, matches[0].Color);
            Assert.AreEqual(3, matches[0].Positions.Count);
            Assert.AreEqual(MatchShape.Line3, matches[0].Shape);
        }

        [Test]
        public void MatchDetector_FindsFour()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(909));
            FillBoardWithoutMatches(board, TileColor.Red);

            board.SetTile(new BoardPosition(0, 4), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 4), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 4), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(3, 4), new Match3Tile(TileColor.Red));

            var matches = board.FindMatches();

            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(4, matches[0].Positions.Count);
            Assert.AreEqual(MatchShape.Line4, matches[0].Shape);
        }

        [Test]
        public void MatchDetector_FindsFive()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1010));
            FillBoardWithoutMatches(board, TileColor.Red);

            for (int y = 1; y <= 5; y++)
            {
                board.SetTile(new BoardPosition(3, y), new Match3Tile(TileColor.Red));
            }

            var matches = board.FindMatches();

            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(5, matches[0].Positions.Count);
            Assert.AreEqual(MatchShape.Line5Plus, matches[0].Shape);
        }

        [Test]
        public void MatchDetector_MergesCrossingMatches()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1111));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Cross centered at (3, 3):
            // Horizontal: (2,3), (3,3), (4,3)
            // Vertical: (3,2), (3,3), (3,4)
            board.SetTile(new BoardPosition(2, 3), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(4, 3), new Match3Tile(TileColor.Red));

            board.SetTile(new BoardPosition(3, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(3, 4), new Match3Tile(TileColor.Red));

            var matches = board.FindMatches();

            Assert.AreEqual(1, matches.Count, "Crossing runs must merge into a single match group.");
            Assert.AreEqual(5, matches[0].Positions.Count);
            Assert.AreEqual(MatchShape.Intersection, matches[0].Shape);
        }

        [Test]
        public void Clear_RemovesMatchedTiles()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1212));

            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var resolution = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));
            Assert.IsTrue(resolution.IsValidMove);

            var clearStep = resolution.Steps[1];
            Assert.AreEqual(ResolutionStepType.MatchAndClear, clearStep.Type);
            Assert.Contains(new BoardPosition(0, 0), (System.Collections.ICollection)clearStep.Cleared);
            Assert.Contains(new BoardPosition(1, 0), (System.Collections.ICollection)clearStep.Cleared);
            Assert.Contains(new BoardPosition(2, 0), (System.Collections.ICollection)clearStep.Cleared);
        }

        [Test]
        public void Gravity_CompactsColumnCorrectly()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1313));

            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var tileAbove = board.GetTile(new BoardPosition(0, 1));

            var resolution = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));
            Assert.IsTrue(resolution.IsValidMove);

            // Gravity step must record movements of tiles dropping down
            bool foundGravity = false;
            foreach (var step in resolution.Steps)
            {
                if (step.Type == ResolutionStepType.Gravity)
                {
                    foundGravity = true;
                    Assert.Greater(step.Moves.Count, 0, "Gravity step must contain TileMoves.");
                }
            }
            Assert.IsTrue(foundGravity, "Resolution must contain a Gravity step.");
        }

        [Test]
        public void Refill_FillsAllEmptyCells()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1414));

            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var resolution = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));
            Assert.IsTrue(resolution.IsValidMove);

            // Ensure no cell on board remains empty
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var tile = board.GetTile(x, y);
                    Assert.IsTrue((int)tile.Color < board.Config.ColorCount);
                }
            }
        }

        [Test]
        public void Cascade_ResolvesUntilStable()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1515));

            // Execute an action and verify board reaches stable state
            bool swapped = false;
            for (int y = 0; y < board.Height && !swapped; y++)
            {
                for (int x = 0; x < board.Width - 1 && !swapped; x++)
                {
                    var a = new BoardPosition(x, y);
                    var b = new BoardPosition(x + 1, y);
                    var res = board.TrySwap(a, b);
                    if (res.IsValidMove)
                    {
                        swapped = true;
                        Assert.AreEqual(0, board.FindMatches().Count, "Board must have 0 matches after resolution completes.");
                    }
                }
            }
            Assert.IsTrue(swapped);
        }

        [Test]
        public void Cascade_ProducesOrderedResolutionSteps()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(1616));

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width - 1; x++)
                {
                    var res = board.TrySwap(new BoardPosition(x, y), new BoardPosition(x + 1, y));
                    if (res.IsValidMove)
                    {
                        Assert.AreEqual(ResolutionStepType.Swap, res.Steps[0].Type);
                        Assert.AreEqual(ResolutionStepType.MatchAndClear, res.Steps[1].Type);
                        return;
                    }
                }
            }
        }

        [Test]
        public void SameSeed_ProducesSameBoard()
        {
            var board1 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(424242));
            var board2 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(424242));

            for (int y = 0; y < board1.Height; y++)
            {
                for (int x = 0; x < board1.Width; x++)
                {
                    Assert.AreEqual(board1.GetTile(x, y), board2.GetTile(x, y), $"Tile at ({x},{y}) must match exactly.");
                }
            }
        }

        [Test]
        public void SameSeedAndMoves_ProduceSameResolution()
        {
            var board1 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(98765));
            var board2 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(98765));

            BoardPosition moveA = default;
            BoardPosition moveB = default;
            bool foundMove = false;

            for (int y = 0; y < board1.Height && !foundMove; y++)
            {
                for (int x = 0; x < board1.Width - 1 && !foundMove; x++)
                {
                    var a = new BoardPosition(x, y);
                    var b = new BoardPosition(x + 1, y);
                    if (board1.TrySwap(a, b).IsValidMove)
                    {
                        moveA = a;
                        moveB = b;
                        foundMove = true;
                    }
                }
            }

            Assert.IsTrue(foundMove);

            // Re-instantiate board 1 to replay the exact same move
            board1 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(98765));
            board2 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(98765));

            var res1 = board1.TrySwap(moveA, moveB);
            var res2 = board2.TrySwap(moveA, moveB);

            Assert.AreEqual(res1.Steps.Count, res2.Steps.Count);
            Assert.AreEqual(res1.CascadeCount, res2.CascadeCount);

            for (int y = 0; y < board1.Height; y++)
            {
                for (int x = 0; x < board1.Width; x++)
                {
                    Assert.AreEqual(board1.GetTile(x, y), board2.GetTile(x, y));
                }
            }
        }

        [Test]
        public void StableBoard_HasNoExistingMatches()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(777));
            Assert.AreEqual(0, board.FindMatches().Count);
        }

        [Test]
        public void StableBoard_HasAtLeastOneLegalMove()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(888));
            Assert.IsTrue(board.HasAnyLegalMove());
        }

        [Test]
        public void FuzzTest_TenThousandBoards_HaveNoInitialMatchesAndHaveLegalMoves()
        {
            for (int seed = 0; seed < 10000; seed++)
            {
                var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(seed));

                Assert.AreEqual(0, board.FindMatches().Count, $"Seed {seed} generated an initial match!");
                Assert.IsTrue(board.HasAnyLegalMove(), $"Seed {seed} generated a board with no legal moves!");
            }
        }

        [Test]
        public void Pcg32Random_MatchesGoldenVector()
        {
            var rng = new Pcg32Match3Random(42UL, 54UL);
            uint[] expectedUints = { 2707161783U, 2068313097U, 3122475824U, 2211639955U, 3215226955U };
            for (int i = 0; i < expectedUints.Length; i++)
            {
                Assert.AreEqual(expectedUints[i], rng.NextUInt(), $"NextUInt mismatch at index {i}");
            }

            var rngRanged = new Pcg32Match3Random(42UL, 54UL);
            int[] expectedRanged = { 3, 2, 4, 0, 0, 1, 0, 0, 4, 4 };
            for (int i = 0; i < expectedRanged.Length; i++)
            {
                Assert.AreEqual(expectedRanged[i], rngRanged.Next(0, 5), $"Next(0, 5) mismatch at index {i}");
            }
        }

        [Test]
        public void BoardResolution_CollectionsAreDeeplyImmutable()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(42));
            BoardResolution resolution = null;
            for (int y = 0; y < board.Height && resolution == null; y++)
            {
                for (int x = 0; x < board.Width - 1 && resolution == null; x++)
                {
                    var a = new BoardPosition(x, y);
                    var b = new BoardPosition(x + 1, y);
                    var res = board.TrySwap(a, b);
                    if (res.IsValidMove)
                        resolution = res;
                }
            }

            Assert.IsNotNull(resolution, "Must find at least one valid swap.");
            Assert.IsInstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<ResolutionStep>>(resolution.Steps);

            var stepsList = (IList<ResolutionStep>)resolution.Steps;
            Assert.Throws<System.NotSupportedException>(() => stepsList.Add(new ResolutionStep(ResolutionStepType.Swap)));
            Assert.Throws<System.NotSupportedException>(() => stepsList.Clear());

            var step = resolution.Steps[0];
            var movesList = (IList<TileMove>)step.Moves;
            Assert.Throws<System.NotSupportedException>(() => movesList.Add(new TileMove(new BoardPosition(0, 0), new BoardPosition(1, 1))));

            if (resolution.Steps.Count > 1 && resolution.Steps[1].Cleared.Count > 0)
            {
                var clearStep = resolution.Steps[1];
                var clearedList = (IList<BoardPosition>)clearStep.Cleared;
                Assert.Throws<System.NotSupportedException>(() => clearedList.Add(new BoardPosition(0, 0)));

                if (clearStep.Matches.Count > 0)
                {
                    var matchesList = (IList<MatchGroup>)clearStep.Matches;
                    Assert.Throws<System.NotSupportedException>(() => matchesList.Clear());

                    var groupPositionsList = (IList<BoardPosition>)clearStep.Matches[0].Positions;
                    Assert.Throws<System.NotSupportedException>(() => groupPositionsList.Add(new BoardPosition(0, 0)));
                }
            }
        }

        [Test]
        public void HasAnyLegalMove_DoesNotMutateBoardOrRandomState()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(9999));
            ulong stateBefore = board.Random.State;

            var tilesBefore = new Match3Tile[board.Width, board.Height];
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    tilesBefore[x, y] = board.GetTile(x, y);
                }
            }

            bool hasMove1 = board.HasAnyLegalMove();
            bool hasMove2 = board.HasAnyLegalMove();

            Assert.AreEqual(hasMove1, hasMove2);
            Assert.AreEqual(stateBefore, board.Random.State, "Random state must remain identical after HasAnyLegalMove.");

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Assert.AreEqual(tilesBefore[x, y], board.GetTile(x, y), $"Tile at ({x}, {y}) was mutated by HasAnyLegalMove!");
                }
            }
        }

        [Test]
        public void MaxCascadeDepth_WhenThrowConfigured_ThrowsException()
        {
            var config = new Match3BoardConfig
            {
                MaxCascadeDepth = 0,
                ThrowOnMaxCascadeDepthExceeded = true
            };
            var board = new Match3BoardModel(config, new SeededMatch3Random(1234));

            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));
            });
        }

        [Test]
        public void MaxCascadeDepth_WhenNotThrowing_SetsFlagAndStabilizesBoard()
        {
            var config = new Match3BoardConfig
            {
                MaxCascadeDepth = 0,
                ThrowOnMaxCascadeDepthExceeded = false
            };
            var board = new Match3BoardModel(config, new SeededMatch3Random(1234));

            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));

            Assert.IsTrue(res.IsValidMove);
            Assert.IsTrue(res.MaxCascadeDepthExceeded, "Flag MaxCascadeDepthExceeded must be set.");
            Assert.AreEqual(0, board.FindMatches().Count, "Board must be stabilized without residual matches.");
        }

        [Test]
        public void CoordinateConvention_BottomLeftAndGravityDropDirection()
        {
            var p00 = new BoardPosition(0, 0);
            var p01 = new BoardPosition(0, 1);
            Assert.IsTrue(p00.IsOrthogonalNeighbor(p01));

            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(5555));
            FillBoardWithoutMatches(board, TileColor.Red);

            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(2, 1));

            Assert.IsTrue(res.IsValidMove);
            bool foundDrop = false;
            foreach (var step in res.Steps)
            {
                if (step.Type == ResolutionStepType.Gravity)
                {
                    foreach (var move in step.Moves)
                    {
                        if (move.From.X == 0 && move.From.Y == 1 && move.To.Y == 0)
                            foundDrop = true;
                    }
                }
            }
            Assert.IsTrue(foundDrop, "Gravity must drop tile downwards from Y=1 to Y=0.");
        }

        [Test]
        public void AutoShuffle_ReachesFallback_WhenAttemptsExhausted()
        {
            var config = new Match3BoardConfig
            {
                MaxShuffleAttempts = 1
            };
            var board = new Match3BoardModel(config, new SeededMatch3Random(777));
            board.AutoShuffle();

            Assert.AreEqual(0, board.FindMatches().Count, "Must have zero matches after emergency regeneration.");
            Assert.IsTrue(board.HasAnyLegalMove(), "Must have legal moves after emergency regeneration.");
        }

        [Test]
        public void MatchGroup_PositionsAreDeeplyImmutable()
        {
            var positions = new List<BoardPosition>
            {
                new BoardPosition(2, 0),
                new BoardPosition(1, 0),
                new BoardPosition(0, 0)
            };

            var group = new MatchGroup(TileColor.Red, positions, MatchShape.Line3, true);

            // Canonical ordering: (0,0), (1,0), (2,0)
            Assert.AreEqual(new BoardPosition(0, 0), group.Positions[0]);
            Assert.AreEqual(new BoardPosition(1, 0), group.Positions[1]);
            Assert.AreEqual(new BoardPosition(2, 0), group.Positions[2]);

            // External list modification does not affect group
            positions.Add(new BoardPosition(3, 0));
            Assert.AreEqual(3, group.Positions.Count);

            // Mutation via IList interface throws NotSupportedException
            Assert.Throws<System.NotSupportedException>(() =>
            {
                if (group.Positions is IList<BoardPosition> list)
                {
                    list.Add(new BoardPosition(4, 0));
                }
            });
        }
    }
}
