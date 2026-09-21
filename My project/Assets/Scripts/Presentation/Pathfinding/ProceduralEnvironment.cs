/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs
@module: presentation.pathfinding.worldgen
@purpose: Generates the square-background world, biome masks, streaming chunks, props, trees, and far-view bake.
@entry: ProceduralEnvironment.Awake, ProceduralEnvironment.Generate, PENV-03, PENV-05, PENV-13
@api: scene MonoBehaviour, inspector-driven runtime system
@deps: HexPathfindingBootstrap, Tilemap/Grid, CameraZoom2D, StaticObstacleHash, CompositionRoot
@data: chunk states, background masks, converted tiles, water/rock masks, far-view bake queues
@perf: hotpath, frame-budgeted generation/streaming/bake, memory-sensitive on large maps
@thread: main thread orchestration with staged async-style work
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual scene verification
@config: inspector fields in PENV-01, SampleScene bindings, docs/runtime_switches.md
@assets: GroundTiles, PropTiles, TreeTiles, RockPropTiles, Background tilemap/grid
@notes: do not mix hex-grid and background-grid coordinates when reading masks or clearing chunks
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment : MonoBehaviour
    {
        // [PENV-01]
        // Inspector-facing configuration and runtime state for generation, streaming,
        // background conversion, biome masks, and far-view baking.
        [Header("General")]
        public bool Enabled = true;
        public bool GenerateOnAwake = true;
        public bool ClearBeforeGenerate = true;
        [Header("Async Generation")]
        public bool UseAsyncGeneration = true;
        public int GroundRowsPerFrame = 16;
        public int PropAttemptsPerFrame = 200;
        public int PropCandidatesPerFrame = 2000;

        [Header("Render Optimization")]
        public bool UseTilemapChunkMode = true;
        public bool CenterCameraOnGenerate = true;
        [Header("Ground Render Migration")]
        [Tooltip("Experimental: mirrors cached/streamed background payload into chunked mesh renderers. Tilemap writeback remains authoritative while this is validated.")]
        public bool UseBackgroundPayloadChunkRenderer = false;
        [Min(1)]
        public int BackgroundPayloadChunkRendererChunkSize = 64;
        [Tooltip("Optional material for the experimental background payload chunk renderer. If empty, a hidden sprite-texture shader is used.")]
        public Material BackgroundPayloadChunkRendererMaterial;
        [Tooltip("Experimental: mirrors cached ground payload into chunked mesh renderers. Tilemap writeback remains authoritative while this is validated.")]
        public bool UseGroundPayloadChunkRenderer = false;
        [Min(1)]
        public int GroundPayloadChunkRendererChunkSize = 64;
        [Tooltip("Optional material for the experimental ground payload chunk renderer. If empty, a hidden sprite-texture shader is used.")]
        public Material GroundPayloadChunkRendererMaterial;
        [Tooltip("Experimental: mirrors extracted water/rock biome-mask payload into chunked vertex-color meshes for validation.")]
        public bool UseBiomeMaskChunkRenderer = false;
        [Min(1)]
        public int BiomeMaskChunkRendererChunkSize = 64;
        [Tooltip("Optional material for the experimental biome-mask chunk renderer. If empty, a hidden vertex-color shader is used.")]
        public Material BiomeMaskChunkRendererMaterial;
        public Color BiomeMaskWaterColor = new Color(0.1f, 0.45f, 1f, 0.45f);
        public Color BiomeMaskRockColor = new Color(0.45f, 0.42f, 0.34f, 0.5f);

        [Header("Far View Bake")]
        public bool UseFarViewBake = false;
        public float FarViewOrthoThreshold = 90f;
        public float FarViewOrthoHysteresis = 5f;
        public bool UseFarViewThresholdFromCameraZoom = true;
        [Range(0.1f, 1f)]
        public float FarViewOrthoThresholdPercent = 0.5f;
        [Tooltip("Ignore the camera threshold and use baked far view at any zoom distance.")]
        public bool FarViewAlwaysActive = false;
        public bool UseFarViewChunkedBake = true;
        [Tooltip("Render each chunk directly to a RenderTexture without CPU readback.")]
        public bool UseFarViewDirectChunkRender = false;
        [Min(256)]
        public int FarViewChunkPixels = 2048;
        [Min(1)]
        public int FarViewChunksPerFrame = 4;
        public bool UseFarViewAsyncReadback = true;
        [Min(1)]
        public int FarViewReadbacksPerFrame = 1;
        [Min(1)]
        public int FarViewMaxPendingReadbacks = 4;
        [Min(0.1f)]
        public float FarViewReadbackTimeout = 2f;
        public bool FarViewFallbackToSyncReadback = true;
        [Min(0f)]
        public float FarViewBakeFrameBudgetMs = 6f;
        public bool FarViewBakeOnStart = false;
        public int FarViewPixelsPerUnit = 96;
        public int FarViewMaxTextureSize = 8192;
        public string FarViewBakeLayerName = "";
        public int FarViewSortingOrder = -10;
        public bool FarViewIncludeBackground = true;
        public bool FarViewIncludeGround = true;
        public bool FarViewIncludeProps = true;
        public bool FarViewIncludeBlockers = true;
        public bool FarViewIncludeTransitions = true;
        [Header("Far View Bake HUD")]
        public bool ShowFarViewBakeHUD = false;
        public bool FarViewShowHudOnlyWhileBaking = true;
        public bool FarViewHideMapWhileBaking = true;
        public string FarViewBakeOverlayText = "Loading world...";
        public Color FarViewBakeOverlayColor = new Color(0f, 0f, 0f, 0.75f);
        public Vector2 FarViewBakeHUDOffset = new Vector2(10f, 10f);
        public Color FarViewBakeHUDColor = Color.white;
        [Header("Far View Bake Debug")]
        public bool ShowFarViewBakeBounds = true;
        public Color FarViewBakeBoundsColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [Min(0.01f)]
        public float FarViewBakeBoundsLineWidth = 0.05f;
        public bool LogFarViewPayloadSourcesOnBake = false;
        [Header("Bake Debug Overlay")]
        public bool ShowBakeDebugOverlay = true;
        public Vector2 BakeDebugOverlayOffset = new Vector2(10f, 10f);
        public Color BakeDebugOverlayColor = Color.white;
        [Header("World Streaming")]
        public bool UseWorldStreaming = false;
        public int StreamChunkSize = 64;
        public int StreamActiveRadius = 6;
        public int StreamPrefetchRadius = 8;
        public int StreamUnloadRadius = 8;
        public int StreamChunksPerFrame = 2;
        public int StreamMaxLoadedChunks = 256;
        public bool StreamIncludeProps = true;
        public bool StreamIncludeTrees = true;
        public bool StreamIncludeBlockers = true;
        [Tooltip("If enabled, props/trees are placed on the background (rect) grid. Otherwise they stay on the hex grid.")]
        public bool StreamPropsUseBackgroundGrid = false;
        public bool StreamBakeAllChunks = false;
        public bool StreamKeepGeneratedChunks = true;
        public bool StreamBakeAllChunksOnIdle = true;
        public float StreamIdleSeconds = 2f;
        public float StreamIdleMoveEpsilon = 0.1f;
        [Header("Streaming Performance")]
        public float StreamFrameBudgetMs = 0f;
        public int StreamTargetFps = 60;
        public bool StreamSkipIfOverBudget = true;
        [Header("Streaming Biomes")]
        public float StreamBiomeNoiseScale = 0.02f;
        public float StreamWaterNoiseScale = 0.015f;
        public float StreamRockNoiseScale = 0.02f;
        public float StreamWaterThreshold = 0.1f;
        public float StreamRockThreshold = 0.2f;

        [Header("Grid")]
        public GridLayout.CellLayout CellLayout = GridLayout.CellLayout.Hexagon;
        public Vector3 CellSizeOverride = Vector3.zero;
        public string GridObjectName = "EnvironmentGrid (Auto)";
        public string GroundTilemapName = "Ground";
        public string PropTilemapName = "Props";
        public string BlockerTilemapName = "Blockers";
        public string SortingLayerName = "";
        public int GroundSortingOrder = -10;
        public int PropSortingOrder = -5;
        public int BlockerSortingOrder = -4;

        [Header("Ground")]
        public bool FillGround = true;
        public TileBase[] GroundTiles;
        [Header("Ground Variants")]
        public bool UseGroundNoise = true;
        [Range(0.001f, 1f)]
        public float GroundNoiseScale = 0.04f;
        public Vector2 GroundNoiseOffset = Vector2.zero;
        public bool RandomizeGroundNoiseOffset = true;
        [Tooltip("Optional grouping of ground tiles to keep biomes together. 0 = disabled.")]
        public int GroundGroupSize = 0;
        [Header("Ground Filters")]
        [Tooltip("If enabled, only ground tiles with this name suffix are used (ex: _N).")]
        public bool UseGroundSuffixFilter = false;
        public string GroundSuffixFilter = "_N";
        [Header("Runtime Ground Conversion")]
        public bool ConvertGroundTilesRuntime = false;
        [Min(1)]
        public int GroundTileConversionBatchSize = 4;
        public bool PreconvertGroundTilesRuntime = false;
        public bool UseGroundTileUnskew = false;
        [Tooltip("Use a fixed diamond shape for the top face (override auto-detection).")]
        public bool UseGroundTileManualDiamond = true;
        [Tooltip("If true, diamond points are normalized (0..1) within the sprite rect; otherwise they are pixels.")]
        public bool GroundTileDiamondNormalized = false;
        [Tooltip("If true, diamond Y coordinates are measured from top-left (image space) instead of Unity's bottom-left.")]
        public bool GroundTileDiamondYFromTop = true;
        [Tooltip("Shrink the diamond inward to avoid sampling beveled edges (pixels).")]
        public float GroundTileDiamondInsetPixels = 3f;
        [Tooltip("Clip sampling to the diamond polygon (remove pixels outside the top face).")]
        public bool GroundTileMaskOutsideDiamond = true;
        [Tooltip("Expands sampling to nearest interior pixels to hide seams along the diamond edge.")]
        public int GroundTileEdgeDilatePixels = 2;
        [Tooltip("Trim dark pixels near the output edge to hide seam artifacts.")]
        public int GroundTileEdgeTrimPixels = 1;
        [Range(0f, 1f)]
        public float GroundTileEdgeBlackThreshold = 0.09f;
        [Range(0f, 1f)]
        public float GroundTileEdgeChromaThreshold = 0.09f;
        [Tooltip("Preserve the original top-face pattern (mask only, no warping).")]
        public bool GroundTilePreservePattern = false;
        [Tooltip("Disable all warping/rotation and output the diamond cutout as-is (no rescale).")]
        public bool GroundTileNoTransform = false;
        [Tooltip("Debug: draw only the diamond outline and skip all other conversions.")]
        public bool GroundTileDebugOutlineOnly = false;
        public Color GroundTileDebugOutlineColor = new Color(0.2f, 0.6f, 1f, 1f);
        public int GroundTileDebugOutlineThickness = 1;
        public Vector2 GroundTileDiamondTop = new Vector2(63.5f, 175f);
        public Vector2 GroundTileDiamondRight = new Vector2(127f, 207.5f);
        public Vector2 GroundTileDiamondBottom = new Vector2(63.5f, 240f);
        public Vector2 GroundTileDiamondLeft = new Vector2(0f, 208.5f);
        public bool UseGroundTileAutoCrop = false;
        public int GroundTileAutoCropPadding = 1;
        public Vector2 GroundTileCropMin = new Vector2(0f, 0.5f);
        public Vector2 GroundTileCropMax = new Vector2(1f, 1f);
        [Range(1f, 3f)]
        public float GroundTileIsoRatio = 2f;
        [Range(-0.5f, 0.5f)]
        public float GroundTileCenterYOffset = 0f;
        [Range(0.25f, 4f)]
        public float GroundTileResolutionScale = 1f;
        public bool GroundTileUseGridCellWidth = true;
        [Range(0.25f, 4f)]
        public float GroundTileWorldScaleMultiplier = 1f;
        [Range(0f, 1f)]
        public float GroundTileAlphaThreshold = 0.2f;
        public bool GroundTileFillTransparent = true;
        public FilterMode GroundTileFilterMode = FilterMode.Point;

        [Header("Ground Edge Matching")]
        public bool UseGroundTileEdgeColorMatch = false;
        [Range(0f, 1f)]
        public float GroundTileEdgeGreenRatio = 0.55f;
        [Range(0f, 1f)]
        public float GroundTileEdgeGreenDominance = 0.08f;
        [Range(0f, 1f)]
        public float GroundTileEdgeGreenMin = 0.2f;
        public int GroundTileEdgeSampleInsetPixels = 1;

        [Header("Ground Tile Rotation")]
        public bool UseGroundTileRandomRotation = false;
        public bool GroundTileRotationInclude0 = true;
        public bool GroundTileRotationInclude90 = true;
        public bool GroundTileRotationInclude180 = true;
        public bool GroundTileRotationInclude270 = false;
        public bool GroundTileMirrorX = false;
        public bool GroundTileMirrorY = false;

        [Header("Ground Tile Anti-Repeat")]
        public bool UseGroundTileAntiRepeat = false;
        public bool GroundTileAntiRepeatLeft = true;
        public bool GroundTileAntiRepeatBottom = true;

        [Header("Ground Tile Debug")]
        public bool GroundTileDebugLogDarkEdges = false;
        [Range(0f, 1f)]
        public float GroundTileDebugDarkEdgeRatio = 0.08f;
        public GroundTileOverride[] GroundTileOverrides;

        [Header("Background (Rect)")]
        public bool UseBackgroundTilemap = true;
        public bool FillBackground = true;
        public bool BackgroundUseConvertedTiles = true;
        public string BackgroundGridName = "BackgroundGrid (Auto)";
        public string BackgroundTilemapName = "Background";
        public int BackgroundSortingOrder = -20;
        public Vector2 BackgroundCellSizeOverride = Vector2.zero;
        [Min(0)]
        public int BackgroundMaxCells = 200000;
        [Min(0)]
        public int BackgroundMaxWidth = 0;
        [Min(0)]
        public int BackgroundMaxHeight = 0;
        [Tooltip("Shrinks background cell size in pixels to slightly overlap tiles and hide seams.")]
        public float BackgroundCellOverlapPixels = 0f;

        [Header("Terrain Ruleset")]
        public bool UseTerrainRuleset = false;
        public HexTerrainRuleset TerrainRuleset;
        [Header("Auto Terrain Ruleset")]
        public bool AutoRulesetFromGroundTiles = true;
        [Range(1, 64)]
        public int AutoTerrainLayerSize = 2;
        [Tooltip("Groups auto layers by the letter after 'Ground ' to avoid mixing unrelated biomes.")]
        public bool AutoTerrainGroupByPrefix = true;
        [Range(0.001f, 1f)]
        public float AutoTerrainNoiseScale = 0.01f;
        public int AutoTerrainOctaves = 3;
        [Range(0f, 1f)]
        public float AutoTerrainPersistence = 0.5f;
        public float AutoTerrainLacunarity = 2f;
        public bool AutoTerrainRandomizeNoiseOffset = true;
        public Vector2 AutoTerrainNoiseOffset = Vector2.zero;
        public bool AutoTerrainPreferLowerNeighbors = true;
        public bool AutoTerrainTreatOutOfBoundsAsLower = false;
        [Header("Noise Warp")]
        public bool UseNoiseDomainWarp = true;
        [Range(0.001f, 1f)]
        public float DomainWarpScale = 0.02f;
        [Range(0f, 2f)]
        public float DomainWarpStrength = 0.6f;
        public int DomainWarpOctaves = 2;
        [Range(0f, 1f)]
        public float DomainWarpPersistence = 0.5f;
        public float DomainWarpLacunarity = 2f;
        [Header("Macro Biomes")]
        public bool UseMacroBiomeNoise = true;
        [Range(0.0005f, 0.1f)]
        public float MacroBiomeScale = 0.004f;
        public int MacroBiomeOctaves = 1;
        [Range(0f, 1f)]
        public float MacroBiomePersistence = 0.5f;
        public float MacroBiomeLacunarity = 2f;
        [Range(0f, 1f)]
        public float MacroBiomeBlend = 0.85f;
        [Range(0.5f, 2f)]
        public float MacroBiomeContrast = 1.2f;
        [Header("Layer Quantization")]
        public bool UseLayerQuantization = true;
        [Range(0f, 0.49f)]
        public float LayerQuantizationJitter = 0.12f;
        [Header("Layer Smoothing")]
        public bool UseLayerSmoothing = true;
        [Range(1, 8)]
        public int LayerSmoothingPasses = 1;
        [Range(0.5f, 1f)]
        public float LayerSmoothingMajority = 0.55f;
        public bool LayerSmoothingIncludeDiagonals = false;
        [Header("Layer Cleanup")]
        public bool UseLayerRegionCleanup = true;
        [Min(1)]
        public int LayerMinRegionSize = 20;
        [Range(1, 8)]
        public int LayerCleanupPasses = 1;
        public bool LayerCleanupIncludeDiagonals = false;
        [Header("Shared Ground Tiles")]
        public bool UseSharedGroundTiles = false;
        [Range(0f, 1f)]
        public float SharedGroundTileChance = 0.15f;
        public TileBase[] SharedGroundTiles;
        public bool SharedGroundTilesUseNameFilter = false;
        public string[] SharedGroundTileNameKeywords = Array.Empty<string>();
        [Header("Water/Rock Biomes")]
        public bool UseWaterBiome = false;
        [Range(0f, 1f)]
        public float WaterCoverage = 0.1f;
        public int RiverCount = 3;
        public int RiverWidthMin = 2;
        public int RiverWidthMax = 4;
        public int LakeMinSize = 10;
        public int LakeMaxSize = 30;
        public int LakeAttempts = 6;
        [Range(0.01f, 1f)]
        public float RiverTurnStrength = 0.35f;
        [Header("Water Mask Smoothing")]
        public bool UseWaterMaskSmoothing = true;
        [Range(0, 8)]
        public int WaterMaskSmoothPasses = 2;
        [Range(0, 8)]
        public int WaterMaskSmoothFillNeighbors = 5;
        [Range(0, 8)]
        public int WaterMaskSmoothStayNeighbors = 4;
        public bool WaterMaskSmoothIncludeDiagonals = true;
        public int RockMinThickness = 3;
        public int RockMaxThickness = 5;
        [Range(0.001f, 0.2f)]
        public float RockThicknessNoiseScale = 0.03f;
        public string[] WaterTileNameKeywords = new[] { "Ground A" };
        public string[] WaterTileExcludeKeywords = new[] { "Ground A3_", "Ground A11_", "Ground A12_" };
        [Tooltip("Tiles used for water cells that are fully surrounded by water.")]
        public string[] WaterInteriorTileNameKeywords = new[] { "Ground A2_" };
        [Header("Water/Rock Variants")]
        public bool WaterTilesAllowRotation = false;
        public bool WaterTilesAllowMirroring = false;
        public bool RockTilesAllowRotation = false;
        public bool RockTilesAllowMirroring = false;
        [Header("Water Edge Detection")]
        [Tooltip("Enforce water edge matching even if green edge matching is disabled.")]
        public bool UseWaterEdgeColorMatch = true;
        [Tooltip("Auto-detect interior water tiles by edge color (blue-dominant edges).")]
        public bool UseWaterAutoInteriorByColor = true;
        [Range(0f, 1f)]
        public float WaterInteriorBlueRatio = 0.9f;
        public int WaterInteriorSampleInsetPixels = 4;
        [Min(0)]
        public int WaterInteriorFallbackCount = 1;
        [Range(0f, 1f)]
        public float WaterEdgeBlueRatio = 0.8f;
        [Range(0f, 1f)]
        public float WaterEdgeLandMaxRatio = 0.3f;
        [Range(0f, 1f)]
        public float WaterEdgeBlueDominance = 0.08f;
        [Range(0f, 1f)]
        public float WaterEdgeBlueMin = 0.2f;
        public int WaterEdgeSampleInsetPixels = 1;
        public int WaterEdgeSampleBandPixels = 3;
        [Range(0f, 1f)]
        public float WaterEdgeMismatchTolerance = 0.1f;
        [Range(1, 8)]
        public int WaterEdgeMaskSamples = 8;
        [Range(0f, 1f)]
        public float WaterEdgeMaskRatioThreshold = 0.45f;
        [Range(0f, 2f)]
        public float WaterEdgeMaskMatchWeight = 0.8f;
        [Header("Water Edge Refinement")]
        public bool UseWaterEdgeRefinement = true;
        [Range(0, 4)]
        public int WaterEdgeRefinePasses = 1;
        [Range(0f, 2f)]
        public float WaterEdgeSmoothnessWeight = 0.6f;
        public string[] RockTileNameKeywords = new[] { "Ground E" };
        [Tooltip("Render edge tiles on a separate tilemap (keeps base tile intact).")]
        public bool UseTransitionTilemap = false;
        public string TransitionTilemapName = "Transitions";
        public int TransitionSortingOrder = -8;

        [Header("Props")]
        [Range(0f, 1f)]
        public float PropCoverage = 0.02f;
        public int PropCount = 0;
        public int PropMinHexDistance = 0;
        public TileBase[] PropTiles;
        [Tooltip("Tiles matching these keywords keep base weight when boosting prop variety.")]
        public string[] PropNoBoostKeywords = new[] { "Flora A12_" };
        [Range(0f, 2f)]
        public float PropBoostMultiplier = 0.5f;
        public bool UseOptimizedPropPlacement = true;

        [Header("Trees")]
        [Range(0f, 1f)]
        public float TreeCoverage = 0.02f;
        public int TreeCount = 0;
        public int TreeMinHexDistance = 2;
        public TileBase[] TreeTiles;
        public bool TreesBlockMovement = true;
        [Range(0f, 1f)]
        public float TreeGradientEdge = 0.2f;
        [Range(0.1f, 4f)]
        public float TreeGradientPower = 1.5f;
        [Header("Tree Weighting")]
        public string[] TreeRareKeywords = new[] { "Object42_", "Tree B", "Tree C" };
        [Range(0.05f, 1f)]
        public float TreeRareWeight = 0.3f;
        public string[] TreeVeryRareKeywords = new[] { "Object43_", "Object44_" };
        [Range(0.02f, 1f)]
        public float TreeVeryRareWeight = 0.1f;
        [Header("Tree Accents")]
        [Range(0f, 0.2f)]
        public float TreeAccentCoverage = 0.000125f;
        public int TreeAccentCount = 0;
        public TileBase[] TreeAccentTiles;
        public string[] TreeAccentKeywords = new[] { "Tree B", "Tree C" };
        public bool TreeAccentExcludeFromBase = true;
        [Header("Tree Biomes")]
        public bool UseTreeBiomeNoise = true;
        public float TreeBiomeScale = 0.006f;
        public int TreeBiomeOctaves = 1;
        public float TreeBiomePersistence = 0.5f;
        public float TreeBiomeLacunarity = 2f;
        [Range(0f, 1f)]
        public float TreeBiomeThreshold = 0.5f;
        [Range(0f, 1f)]
        public float TreeBiomeFeather = 0.12f;
        [Range(0.1f, 3f)]
        public float TreeBiomeContrast = 1.1f;
        public bool TreeBiomeRandomizeOffset = true;
        public Vector2 TreeBiomeNoiseOffset = new Vector2(300f, 700f);

        [Range(0f, 1f)]
        public float RockPropCoverage = 0f;
        public int RockPropCount = 0;
        public int RockPropMinHexDistance = 0;
        public TileBase[] RockPropTiles;
        public bool PropsBlockMovement = false;

        [Header("Blocking Props")]
        [Range(0f, 1f)]
        public float BlockingPropCoverage = 0f;
        public int BlockingPropCount = 0;
        public int BlockingMinHexDistance = 1;
        public TileBase[] BlockingPropTiles;
        public bool BlockingPropsBlockMovement = true;
        [Tooltip("Update walkable grid directly for blocking tiles (avoids physics rebake).")]
        public bool UseDirectWalkableUpdates = true;
        [Tooltip("Create colliders for blocking tilemaps (optional).")]
        public bool BuildBlockingColliders = false;
        [Header("Auto Split")]
        [Tooltip("If enabled, tiles in GroundTiles whose name matches PropNameKeywords are treated as props.")]
        public bool AutoSplitGroundByName = false;
        public string[] PropNameKeywords = new[] { "flora" };
        [Tooltip("If enabled, tiles in PropTiles whose name matches BlockingNameKeywords are treated as blockers.")]
        public bool AutoSplitBlockingByName = false;
        public string[] BlockingNameKeywords = new[] { "tree", "rock", "boulder", "stone", "cliff", "pine" };

        public string ObstacleLayerName = "Obstacles";

        [Header("Random")]
        public bool UseRandomSeed = true;
        public int Seed = 12345;

        private HexPathfindingBootstrap _hex;
        private Grid _grid;
        private Grid _backgroundGrid;
        private Tilemap _ground;
        private Tilemap _background;
        private Tilemap _props;
        private Tilemap _blockers;
        private Tilemap _transitions;
        private bool[] _backgroundWaterMask;
        private bool[] _backgroundRockMask;
        private int _backgroundMaskWidth;
        private int _backgroundMaskHeight;
        private int[] _backgroundLandDistance;
        private int _backgroundLandMaxDistance;
        private readonly Dictionary<Texture2D, Texture2D> _readableTextureCache = new Dictionary<Texture2D, Texture2D>();
        private readonly Dictionary<TileBase, TileBase> _runtimeGroundTileCache = new Dictionary<TileBase, TileBase>();
        private readonly Dictionary<Sprite, Sprite> _runtimeGroundSpriteCache = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, EdgeProfile> _edgeProfileCache = new Dictionary<Sprite, EdgeProfile>();
        private readonly Dictionary<Sprite, bool> _waterInteriorCache = new Dictionary<Sprite, bool>();
        private readonly Dictionary<Sprite, float> _waterInteriorScoreCache = new Dictionary<Sprite, float>();
        private readonly HashSet<string> _loggedDarkEdgeSprites = new HashSet<string>();
        private readonly Dictionary<Sprite, FarViewSpriteSample> _farViewBackgroundSpriteCache = new Dictionary<Sprite, FarViewSpriteSample>();
        private readonly List<UnityEngine.Object> _runtimeGroundObjects = new List<UnityEngine.Object>();
        private Material _farViewBackgroundCompositeMaterial;
        private int _runtimeGroundConversionHash = 0;
        private HexTerrainRuleset _autoTerrainRuleset;
        private int _autoTerrainRulesetHash = 0;
        private Coroutine _generateRoutine;
        private bool _skipRuntimeConversionInResolve;
        private readonly BlockBounds _blockBounds = new BlockBounds();
        private readonly List<Vector2Int> _directBlockedCells = new List<Vector2Int>(1024);
        private readonly HashSet<Vector2Int> _directBlockedSet = new HashSet<Vector2Int>();
        private Vector2 _groundNoiseOffset;
        private Vector2 _treeNoiseOffset;
        private GameObject _farViewRoot;
        private SpriteRenderer _farViewRenderer;
        private Camera _farViewCamera;
        private RenderTexture _farViewTexture;
        private Texture2D _farViewTexture2D;
        private Sprite _farViewSprite;
        private bool _farViewActive;
        private bool _farViewDirty;
        private bool _farViewBaking;
        private Bounds _farViewBounds;
        private Coroutine _farViewBakeRoutine;
        private bool _farViewHasContent;
        private readonly List<FarViewChunk> _farViewChunks = new List<FarViewChunk>();
        private readonly List<FarViewReadback> _farViewReadbacks = new List<FarViewReadback>();
        private int _farViewBakeVersion;
        private int _farViewChunkTotal;
        private int _farViewChunkSubmitted;
        private int _farViewChunkCompleted;
        private int _farViewChunkErrors;
        private int _farViewPayloadOnlyChunks;
        private int _farViewBackgroundSliceCacheHits;
        private int _farViewBackgroundSliceCacheMisses;
        private int _farViewTileSliceCacheHits;
        private int _farViewTileSliceCacheMisses;
        private int _farViewBackgroundChunkCacheHits;
        private int _farViewBackgroundChunkCacheMisses;
        private int _farViewTileUnderlayCacheHits;
        private int _farViewTileUnderlayCacheMisses;
        private float _farViewBakeStartTime;
        private GUIStyle _farViewHudStyle;
        private GameObject _farViewHudRoot;
        private Text _farViewHudText;
        private Image _farViewHudOverlay;
        private Text _farViewHudOverlayText;
        private LineRenderer _farViewBoundsRenderer;
        private Bounds _farViewLastBounds;
        private bool _farViewHasBounds;
        private bool _farViewBakeQueuedOnStart;
        private bool _hasBackgroundRenderInput;
        private int _cachedBackgroundRenderWidth;
        private int _cachedBackgroundRenderHeight;
        private int _cachedBackgroundRenderTileSeed;
        private List<TerrainLayer> _cachedBackgroundRenderLayers;
        private int[] _cachedBackgroundRenderLayerIndex;
        private Dictionary<TileBase, TileBase> _cachedBackgroundRenderLookup;
        private BackgroundRenderCellDecision[] _cachedBackgroundFarViewPayload;
        private Bounds _cachedBackgroundFarViewBounds;
        private bool _hasCachedBackgroundFarViewPayload;
        private int _cachedBackgroundFarViewPayloadVersion;
        private GameObject _backgroundPayloadChunkRendererRoot;
        private Material _backgroundPayloadChunkRendererRuntimeMaterial;
        private int _backgroundPayloadChunkRendererVersion;
        private int _backgroundPayloadChunkRendererChunkCount;
        private int _backgroundPayloadChunkRendererQuadCount;
        private int _backgroundPayloadChunkRendererTextureCount;
        private GroundRenderCellDecision[] _cachedGroundRenderPayload;
        private Bounds _cachedGroundRenderBounds;
        private bool _hasCachedGroundRenderPayload;
        private int _cachedGroundRenderWidth;
        private int _cachedGroundRenderHeight;
        private bool _cachedGroundRenderUsesTransition;
        private int _cachedGroundFarViewPayloadVersion;
        private TilemapChunkCellData[] _cachedPropPlacementPayload;
        private BoundsInt _cachedPropPlacementBounds;
        private bool _hasCachedPropPlacementPayload;
        private TilemapChunkCellData[] _cachedBlockerPlacementPayload;
        private BoundsInt _cachedBlockerPlacementBounds;
        private bool _hasCachedBlockerPlacementPayload;
        private int _cachedFarViewTileSnapshotVersion;
        private bool _streamingActive;
        private int _streamSeed;
        private int _streamWidth;
        private int _streamHeight;
        private Vector2Int _streamCenterChunk = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int _streamActiveMinChunk;
        private Vector2Int _streamActiveMaxChunk;
        private Vector2Int _streamPrefetchMinChunk;
        private Vector2Int _streamPrefetchMaxChunk;
        private int _streamRequiredChunkCount;
        private Vector2 _streamWaterOffset;
        private Vector2 _streamRockOffset;
        private TileBase[] _streamLandTiles;
        private TileBase[] _streamWaterTiles;
        private TileBase[] _streamWaterInteriorTiles;
        private TileBase[] _streamRockTiles;
        private TileVariant[] _streamLandVariants;
        private TileVariant[] _streamWaterVariants;
        private TileVariant[] _streamWaterInteriorVariants;
        private TileVariant[] _streamRockVariants;
        private TileVariant[] _streamSharedVariants;
        private HashSet<TileBase> _streamWaterSet;
        private HashSet<TileBase> _streamRockSet;
        private string[] _streamWaterExclude;
        private TileBase[] _streamPropTiles;
        private TileBase[] _streamTreeTiles;
        private TileBase[] _streamTreeAccentTiles;
        private TileBase[] _streamBlockingTiles;
        private readonly Dictionary<Vector2Int, StreamChunkState> _streamChunks = new Dictionary<Vector2Int, StreamChunkState>();
        private readonly Queue<Vector2Int> _streamQueue = new Queue<Vector2Int>();
        private readonly Queue<Vector2Int> _streamAllQueue = new Queue<Vector2Int>();
        private bool _streamAllQueued;
        private Vector3 _streamLastCamPos;
        private float _streamLastCamMoveTime;
        private int _streamGeneratedTotal;
        private int _streamGeneratedSinceSample;
        private float _streamLastSampleTime;
        private float _streamSpeed;
        private GUIStyle _bakeDebugStyle;

        private struct StreamChunkState
        {
            public Vector2Int Coord;
            public BoundsInt HexBounds;
            public BoundsInt BackgroundBounds;
            public bool Generated;
            public int LastTouchedFrame;
            public List<Vector2Int> BlockedCells;
            public BackgroundRenderCellDecision[] BackgroundPayload;
            public int BackgroundCellCount;
            public BoundsInt GroundPayloadBounds;
            public TilemapChunkCellData[] GroundCells;
            public int GroundCellCount;
            public List<Placement> PropPlacements;
            public List<Placement> BlockerPlacements;
        }
        private bool _farViewLoadingApplied;
        private static readonly Vector2[] BiomeSampleOffsets = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0.45f, 0f),
            new Vector2(-0.45f, 0f),
            new Vector2(0f, 0.45f),
            new Vector2(0f, -0.45f),
            new Vector2(0.25f, 0.25f),
            new Vector2(-0.25f, 0.25f),
            new Vector2(0.25f, -0.25f),
            new Vector2(-0.25f, -0.25f)
        };

    }
}
