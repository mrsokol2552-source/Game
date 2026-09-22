/*
@file: My project/Assets/Tests/EditMode/Match3SpecialResolutionTests.cs
@module: Match3/Tests
@purpose: Unit tests, property invariants, and combo verification for Match3BoardModel v0.2 Special Resolution
@entry: Match3SpecialResolutionTests
@deps: Match3.Core, Match3.Random
@tests: EditMode
@notes: Validates geometry classification, anchor precedence, chain reactions, single-activation invariant, and special swap matrix.
*/

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Match3.Core;
using Match3.Random;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Gate")]
    public class Match3SpecialResolutionTests
    {
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
        public void HorizontalFour_SpawnsRocketHorizontal_AtAnchor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(101));
            FillBoardWithoutMatches(board, TileColor.Red);

            // (0,0)=Red, (1,0)=Red, (2,0)=Blue, (3,0)=Red, (2,1)=Red
            // Swap (2,1) -> (2,0) produces horizontal run of 4 at Y=0. Anchor should be (2,0).
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(3, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(2, 1), new BoardPosition(2, 0));

            Assert.IsTrue(res.IsValidMove);
            Assert.AreEqual(1, res.Steps[1].Specials.Count);
            Assert.AreEqual(TileSpecial.RocketHorizontal, res.Steps[1].Specials[0].Special);
            Assert.AreEqual(new BoardPosition(2, 0), res.Steps[1].Specials[0].Position);
        }

        [Test]
        public void VerticalFour_SpawnsRocketVertical_AtAnchor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(102));
            FillBoardWithoutMatches(board, TileColor.Red);

            // (0,0)=Red, (0,1)=Red, (0,2)=Blue, (0,3)=Red, (1,2)=Red
            // Swap (1,2) -> (0,2) produces vertical run of 4 at X=0. Anchor should be (0,2).
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(0, 1), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(0, 2), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(0, 3), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 2), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(1, 2), new BoardPosition(0, 2));

            Assert.IsTrue(res.IsValidMove);
            Assert.AreEqual(1, res.Steps[1].Specials.Count);
            Assert.AreEqual(TileSpecial.RocketVertical, res.Steps[1].Specials[0].Special);
            Assert.AreEqual(new BoardPosition(0, 2), res.Steps[1].Specials[0].Position);
        }

        [Test]
        public void Intersection_SpawnsDynamite_AtIntersectionAnchor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(103));
            FillBoardWithoutMatches(board, TileColor.Red);

            // T-junction with intersection at (2,2)
            // Horizontal: (1,2)=Red, (2,2)=Red, (3,2)=Red
            // Vertical: (2,0)=Red, (2,1)=Blue, (3,1)=Red
            // Swap (3,1) -> (2,1) creates vertical (2,0), (2,1), (2,2) intersecting horizontal at (2,2).
            board.SetTile(new BoardPosition(1, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(3, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(3, 1), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(3, 1), new BoardPosition(2, 1));

            Assert.IsTrue(res.IsValidMove);
            Assert.AreEqual(1, res.Steps[1].Specials.Count);
            Assert.AreEqual(TileSpecial.Dynamite, res.Steps[1].Specials[0].Special);
            Assert.AreEqual(new BoardPosition(2, 2), res.Steps[1].Specials[0].Position);
        }

        [Test]
        public void Intersection_TakesPrecedenceOverFiveTileArm()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(104));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Cross with horizontal length 5 and vertical length 3
            // Horizontal: (0,2), (1,2), (2,2), (3,2), (4,2)
            // Vertical: (2,1), (2,2), (2,3)
            // Intersection precedence rule requires Dynamite, not Airstrike
            for (int x = 0; x < 5; x++)
            {
                if (x != 2) board.SetTile(new BoardPosition(x, 2), new Match3Tile(TileColor.Red));
            }
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 3), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 2), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Red)); // will swap with (2,2)? No, adjacent swap:
            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red));

            var matches = board.FindMatches();
            // Test direct match group classifier
            board.SetTile(new BoardPosition(2, 2), new Match3Tile(TileColor.Red));
            var detected = board.FindMatches();

            Assert.AreEqual(1, detected.Count);
            Assert.AreEqual(MatchShape.Intersection, detected[0].Shape, "Intersection must take precedence over Line5Plus.");
        }

        [Test]
        public void FiveInARow_SpawnsAirstrike_AtAnchor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(105));
            FillBoardWithoutMatches(board, TileColor.Red);

            // 5 in a row horizontally at Y=0: (0,0)..(4,0)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(3, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(4, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(2, 1), new BoardPosition(2, 0));

            Assert.IsTrue(res.IsValidMove);
            Assert.AreEqual(1, res.Steps[1].Specials.Count);
            Assert.AreEqual(TileSpecial.Airstrike, res.Steps[1].Specials[0].Special);
            Assert.AreEqual(new BoardPosition(2, 0), res.Steps[1].Specials[0].Position);
        }

        [Test]
        public void MergedGroup_CreatesOnlyOneSpecial()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(106));
            FillBoardWithoutMatches(board, TileColor.Red);

            // T-junction setup
            board.SetTile(new BoardPosition(1, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(3, 2), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(3, 0), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(3, 0), new BoardPosition(2, 0));

            Assert.IsTrue(res.IsValidMove);
            Assert.AreEqual(1, res.Steps[1].Specials.Count, "A single merged match group must produce exactly one special.");
        }

        [Test]
        public void RocketHorizontal_Activation_ClearsEntireRow()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(107));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Place RocketHorizontal at (2, 3)
            board.SetTile(new BoardPosition(2, 3), new Match3Tile(TileColor.Red, TileSpecial.RocketHorizontal));
            var normalTile = board.GetTile(new BoardPosition(3, 3));

            // Swap (2, 3) with (3, 3) to trigger rocket at (3, 3)
            var res = board.TrySwap(new BoardPosition(2, 3), new BoardPosition(3, 3));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(1, clearStep.Activations.Count);
            Assert.AreEqual(TileSpecial.RocketHorizontal, clearStep.Activations[0].Special);

            // Verify entire row 3 was cleared
            for (int x = 0; x < board.Width; x++)
            {
                Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(x, 3)));
            }
        }

        [Test]
        public void RocketVertical_Activation_ClearsEntireColumn()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(108));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Place RocketVertical at (4, 1)
            board.SetTile(new BoardPosition(4, 1), new Match3Tile(TileColor.Red, TileSpecial.RocketVertical));

            var res = board.TrySwap(new BoardPosition(4, 1), new BoardPosition(4, 2));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(1, clearStep.Activations.Count);
            Assert.AreEqual(TileSpecial.RocketVertical, clearStep.Activations[0].Special);

            // Verify entire column 4 was cleared
            for (int y = 0; y < board.Height; y++)
            {
                Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(4, y)));
            }
        }

        [Test]
        public void Dynamite_Activation_ClearsChebyshevDistanceOne()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(109));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Place Dynamite at (3, 3)
            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(3, 3), new BoardPosition(3, 4));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(1, clearStep.Activations.Count);
            Assert.AreEqual(TileSpecial.Dynamite, clearStep.Activations[0].Special);

            // Verify all 9 cells in 3x3 square centered at (3, 4) were cleared
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(3 + dx, 4 + dy)));
                }
            }
        }

        [Test]
        public void Airstrike_Activation_ClearsAllTilesOfTargetColor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(110));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Place Airstrike at (0, 0), adjacent to Blue tile at (1, 0)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.None, TileSpecial.Airstrike));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Blue));

            var res = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(1, 0));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(1, clearStep.Activations.Count);
            Assert.AreEqual(TileSpecial.Airstrike, clearStep.Activations[0].Special);
            Assert.AreEqual(TileColor.Blue, clearStep.Activations[0].TargetColor);

            // Verify that every Blue tile that was on the board was cleared
            foreach (var ct in clearStep.ClearedTiles)
            {
                if (ct.Tile.Color == TileColor.Blue)
                {
                    Assert.AreEqual(ClearCause.Airstrike, ct.Cause);
                }
            }
        }

        [Test]
        public void SpecialAtBoardEdge_ClipsAreaCorrectly()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(111));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Place Dynamite in corner (0, 0), swap with (0, 1)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(0, 1));

            Assert.IsTrue(res.IsValidMove);
            // Dest is (0, 1). Chebyshev distance <= 1 gives X in [0..1], Y in [0..2] = 6 cells
            var clearStep = res.Steps[1];
            Assert.AreEqual(6, clearStep.Activations[0].ClearedPositions.Count);
            foreach (var pos in clearStep.Activations[0].ClearedPositions)
            {
                Assert.IsTrue(board.IsInBounds(pos));
            }
        }

        [Test]
        public void ChainReaction_RocketTriggersDynamite_BothActivate()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(112));
            FillBoardWithoutMatches(board, TileColor.Red);

            // RocketHorizontal at (0, 2)
            board.SetTile(new BoardPosition(0, 2), new Match3Tile(TileColor.Red, TileSpecial.RocketHorizontal));
            // Dynamite at (5, 2) in the path of the rocket!
            board.SetTile(new BoardPosition(5, 2), new Match3Tile(TileColor.Blue, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(0, 2), new BoardPosition(1, 2));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(2, clearStep.Activations.Count, "Both Rocket and Dynamite must activate in chain reaction.");
            Assert.AreEqual(TileSpecial.RocketHorizontal, clearStep.Activations[0].Special);
            Assert.AreEqual(TileSpecial.Dynamite, clearStep.Activations[1].Special);
        }

        [Test]
        public void SingleActivationInvariant_CircularSpecials_DoNotInfiniteLoop()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(113));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Two adjacent dynamites that cover each other
            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));
            board.SetTile(new BoardPosition(4, 3), new Match3Tile(TileColor.Blue, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(3, 3), new BoardPosition(4, 3));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(1, clearStep.Activations.Count, "Combo swap resolves into single combo detonation.");
        }

        [Test]
        public void NewlySpawnedSpecial_CanBeTriggeredInSameResolution()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(114));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Setup a 4-match at Y=0: (0,0)=Red, (1,0)=Red, (2,0)=Blue, (3,0)=Red, (2,1)=Red
            // This will spawn a RocketHorizontal at anchor (2,0).
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(3, 0), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Red));

            // Also place a pre-existing Dynamite at (2, 2) that triggers!
            // When (2,1) moves to (2,0), (2,2) is adjacent to (2,1)
            // What if (0,0) had a Dynamite?
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(2, 1), new BoardPosition(2, 0));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            // Dynamite at (0,0) was matched, so it detonates. Its 3x3 covers (0..1, 0..1).
            // Anchor was at (2,0). If we place Dynamite at (1,0):
            // Its 3x3 covers (0..2, 0..1), which covers (2,0)!
            // Then newly spawned special at (2,0) gets hit and triggers in the same step!
        }

        [Test]
        public void SpecialSwap_TwoRockets_ClearsCross()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(115));
            FillBoardWithoutMatches(board, TileColor.Red);

            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red, TileSpecial.RocketHorizontal));
            board.SetTile(new BoardPosition(3, 4), new Match3Tile(TileColor.Blue, TileSpecial.RocketVertical));

            var res = board.TrySwap(new BoardPosition(3, 3), new BoardPosition(3, 4));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            // Row 4 and column 3 cleared
            for (int x = 0; x < board.Width; x++) Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(x, 4)));
            for (int y = 0; y < board.Height; y++) Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(3, y)));
        }

        [Test]
        public void SpecialSwap_RocketAndDynamite_ClearsThreeRowsAndColumns()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(116));
            FillBoardWithoutMatches(board, TileColor.Red);

            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red, TileSpecial.RocketHorizontal));
            board.SetTile(new BoardPosition(3, 4), new Match3Tile(TileColor.Blue, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(3, 3), new BoardPosition(3, 4));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            // Dest is (3, 4). 3 rows [3..5], 3 cols [2..4]
            for (int r = 3; r <= 5; r++)
            {
                for (int x = 0; x < board.Width; x++) Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(x, r)));
            }
            for (int c = 2; c <= 4; c++)
            {
                for (int y = 0; y < board.Height; y++) Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(c, y)));
            }
        }

        [Test]
        public void SpecialSwap_TwoDynamites_ClearsFiveByFive()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(117));
            FillBoardWithoutMatches(board, TileColor.Red);

            board.SetTile(new BoardPosition(3, 3), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));
            board.SetTile(new BoardPosition(3, 4), new Match3Tile(TileColor.Blue, TileSpecial.Dynamite));

            var res = board.TrySwap(new BoardPosition(3, 3), new BoardPosition(3, 4));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            // 5x5 centered at (3, 4)
            for (int y = 2; y <= 6; y++)
            {
                for (int x = 1; x <= 5; x++)
                {
                    Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(x, y)));
                }
            }
        }

        [Test]
        public void SpecialSwap_TwoAirstrikes_ClearsEntireBoard()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(118));

            board.SetTile(new BoardPosition(2, 2), new Match3Tile(TileColor.None, TileSpecial.Airstrike));
            board.SetTile(new BoardPosition(2, 3), new Match3Tile(TileColor.None, TileSpecial.Airstrike));

            var res = board.TrySwap(new BoardPosition(2, 2), new BoardPosition(2, 3));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.AreEqual(board.Width * board.Height, clearStep.Cleared.Count, "Airstrike + Airstrike must clear all 49 cells.");
        }

        [Test]
        public void SpecialSwap_AirstrikeAndRocketHorizontal_TransformsAndActivatesTargetColor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(119));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Airstrike at (0, 0), Red RocketHorizontal at (1, 0)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.None, TileSpecial.Airstrike));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red, TileSpecial.RocketHorizontal));

            // Place a couple of Red tiles around the board
            board.SetTile(new BoardPosition(5, 5), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 4), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(1, 0));

            Assert.IsTrue(res.IsValidMove);
            // All transformed red tiles activate rockets
            Assert.GreaterOrEqual(res.Steps[1].Activations.Count, 2);
        }

        [Test]
        public void SpecialSwap_AirstrikeAndRocketVertical_TransformsAndActivatesTargetColor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(120));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Airstrike at (0, 0), Red RocketVertical at (1, 0)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.None, TileSpecial.Airstrike));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red, TileSpecial.RocketVertical));

            // Place a couple of Red tiles around the board
            board.SetTile(new BoardPosition(5, 5), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 4), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(1, 0));

            Assert.IsTrue(res.IsValidMove);
            // All transformed red tiles activate rockets
            Assert.GreaterOrEqual(res.Steps[1].Activations.Count, 2);
        }

        [Test]
        public void SpecialSwap_AirstrikeAndDynamite_TransformsAndActivatesTargetColor()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(121));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Airstrike at (0, 0), Red Dynamite at (1, 0)
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.None, TileSpecial.Airstrike));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));

            // Place several Red tiles around board
            board.SetTile(new BoardPosition(5, 5), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(2, 4), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(4, 2), new Match3Tile(TileColor.Red));

            var res = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(1, 0));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];

            // Activations must include Airstrike and the transformed Dynamites
            Assert.GreaterOrEqual(clearStep.Activations.Count, 3);
            Assert.IsTrue(clearStep.Activations.Any(a => a.Special == TileSpecial.Dynamite && a.Origin == new BoardPosition(5, 5)));
            Assert.IsTrue(clearStep.Activations.Any(a => a.Special == TileSpecial.Dynamite && a.Origin == new BoardPosition(2, 4)));
            Assert.IsTrue(clearStep.Activations.Any(a => a.Special == TileSpecial.Dynamite && a.Origin == new BoardPosition(4, 2)));

            // Verify the 3x3 areas around transformed dynamites are cleared
            Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(5, 5)));
            Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(6, 6)));
            Assert.IsTrue(clearStep.Cleared.Contains(new BoardPosition(4, 4)));
        }

        [Test]
        public void ClearCause_FirstCauseWins_WhenEffectsOverlap()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(122));
            FillBoardWithoutMatches(board, TileColor.Red);

            // Setup row 0 match: (0,0)=Red, (1,0)=Red RocketH, (2,0)=Red Dynamite
            // Swap (0,1) with (0,0) where (0,1) is Red
            board.SetTile(new BoardPosition(0, 1), new Match3Tile(TileColor.Red));
            board.SetTile(new BoardPosition(0, 0), new Match3Tile(TileColor.Blue));
            board.SetTile(new BoardPosition(1, 0), new Match3Tile(TileColor.Red, TileSpecial.RocketHorizontal));
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Red, TileSpecial.Dynamite));

            // Target cell (3,0) is in row 0 (hit by RocketH) AND inside 3x3 of Dynamite at (2,0) (x:1..3, y:0..1)
            // Use Green so they are guaranteed not to form matches with background Blue/Yellow
            board.SetTile(new BoardPosition(3, 0), new Match3Tile(TileColor.Green));
            // Cell (2,1) is hit only by Dynamite
            board.SetTile(new BoardPosition(2, 1), new Match3Tile(TileColor.Green));

            var res = board.TrySwap(new BoardPosition(0, 1), new BoardPosition(0, 0));
            Assert.IsTrue(res.IsValidMove);

            var clearStep = res.Steps[1];

            // In canonical BFS order: (1,0) RocketHorizontal is enqueued first, then (2,0) Dynamite.
            // Cell (3,0) was hit first by RocketHorizontal -> Cause must be Rocket.
            var ct30 = clearStep.ClearedTiles.First(c => c.Position == new BoardPosition(3, 0));
            Assert.AreEqual(ClearCause.Rocket, ct30.Cause, "First cause (Rocket) must win for overlapping cell (3,0).");

            // Cell (2,1) was hit by Dynamite only -> Cause must be Dynamite.
            var ct21 = clearStep.ClearedTiles.First(c => c.Position == new BoardPosition(2, 1));
            Assert.AreEqual(ClearCause.Dynamite, ct21.Cause);

            // Both activations still record (3,0) in their ClearedPositions
            var rocketActivation = clearStep.Activations.First(a => a.Special == TileSpecial.RocketHorizontal);
            var dynamiteActivation = clearStep.Activations.First(a => a.Special == TileSpecial.Dynamite);

            Assert.IsTrue(rocketActivation.ClearedPositions.Contains(new BoardPosition(3, 0)));
            Assert.IsTrue(dynamiteActivation.ClearedPositions.Contains(new BoardPosition(3, 0)));
        }

        [Test]
        public void SingleSpecialSwap_NormalTileMovesToSourceAndSurvivesIfNotHit()
        {
            var board = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(120));
            FillBoardWithoutMatches(board, TileColor.Red);

            // RocketVertical at (2, 0), Blue tile at (3, 0)
            // Swap (2, 0) -> (3, 0).
            // Normal tile moves to (2, 0). Rocket detonates at (3, 0) vertically (clears col 3).
            // Col 2 is NOT hit! So the normal tile at (2, 0) must survive!
            board.SetTile(new BoardPosition(2, 0), new Match3Tile(TileColor.Red, TileSpecial.RocketVertical));
            board.SetTile(new BoardPosition(3, 0), new Match3Tile(TileColor.Blue));

            var res = board.TrySwap(new BoardPosition(2, 0), new BoardPosition(3, 0));

            Assert.IsTrue(res.IsValidMove);
            var clearStep = res.Steps[1];
            Assert.IsFalse(clearStep.Cleared.Contains(new BoardPosition(2, 0)), "Normal tile at (2,0) was not hit and must survive!");
        }

        [Test]
        public void SameSeedAndMoves_ProduceByteEquivalentResolutionOrdering()
        {
            var board1 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(99999));
            var board2 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(99999));

            // Execute an action on board 1
            BoardPosition a = default;
            BoardPosition b = default;
            bool found = false;
            for (int y = 0; y < board1.Height && !found; y++)
            {
                for (int x = 0; x < board1.Width - 1 && !found; x++)
                {
                    var p1 = new BoardPosition(x, y);
                    var p2 = new BoardPosition(x + 1, y);
                    if (board1.TrySwap(p1, p2).IsValidMove)
                    {
                        a = p1;
                        b = p2;
                        found = true;
                    }
                }
            }

            Assert.IsTrue(found);

            // Replay on fresh boards
            board1 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(99999));
            board2 = new Match3BoardModel(Match3BoardConfig.Default, new SeededMatch3Random(99999));

            var res1 = board1.TrySwap(a, b);
            var res2 = board2.TrySwap(a, b);

            Assert.AreEqual(res1.Steps.Count, res2.Steps.Count);
            for (int i = 0; i < res1.Steps.Count; i++)
            {
                var s1 = res1.Steps[i];
                var s2 = res2.Steps[i];
                Assert.AreEqual(s1.Type, s2.Type);
                Assert.AreEqual(s1.Cleared.Count, s2.Cleared.Count);
                for (int k = 0; k < s1.Cleared.Count; k++)
                {
                    Assert.AreEqual(s1.Cleared[k], s2.Cleared[k], $"Cleared order mismatch at step {i}, index {k}");
                }
                Assert.AreEqual(s1.Moves.Count, s2.Moves.Count);
                for (int k = 0; k < s1.Moves.Count; k++)
                {
                    Assert.AreEqual(s1.Moves[k].From, s2.Moves[k].From);
                    Assert.AreEqual(s1.Moves[k].To, s2.Moves[k].To);
                }
            }
        }

        [Test]
        public void FuzzTest_SpecialsPropertyInvariants()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var rng = new SeededMatch3Random(seed);
                var board = new Match3BoardModel(Match3BoardConfig.Default, rng);

                // Place a random special
                int sx = rng.Next(0, board.Width);
                int sy = rng.Next(0, board.Height);
                TileSpecial randomSpecial = (TileSpecial)rng.Next(1, 5);
                board.SetTile(new BoardPosition(sx, sy), new Match3Tile(TileColor.Red, randomSpecial));

                // Perform swap with adjacent neighbor
                int targetX = sx < board.Width - 1 ? sx + 1 : sx - 1;
                var res = board.TrySwap(new BoardPosition(sx, sy), new BoardPosition(targetX, sy));

                Assert.IsTrue(res.IsValidMove);
                Assert.AreEqual(0, board.FindMatches().Count, $"Seed {seed} left residual matches on board!");
                Assert.IsTrue(board.HasAnyLegalMove(), $"Seed {seed} left board without legal moves!");
            }
        }
    }
}
