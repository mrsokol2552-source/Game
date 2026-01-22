using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Game.Presentation.Pathfinding
{
    public class ProceduralEnvironment : MonoBehaviour
    {
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

        [Header("Far View Bake")]
        public bool UseFarViewBake = true;
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
        private readonly List<UnityEngine.Object> _runtimeGroundObjects = new List<UnityEngine.Object>();
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
        private float _farViewBakeStartTime;
        private GUIStyle _farViewHudStyle;
        private GameObject _farViewHudRoot;
        private Text _farViewHudText;
        private Image _farViewHudOverlay;
        private Text _farViewHudOverlayText;
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

        private void Awake()
        {
            if (!GenerateOnAwake) return;
            Generate();
        }

        [ContextMenu("Generate Environment")]
        public void Generate()
        {
            if (_generateRoutine != null)
            {
                StopCoroutine(_generateRoutine);
                _generateRoutine = null;
            }
            _waterInteriorCache.Clear();
            _edgeProfileCache.Clear();
            if (!Enabled) return;
            if (UseAsyncGeneration && UnityEngine.Application.isPlaying)
                _generateRoutine = StartCoroutine(GenerateRoutine());
            else
                GenerateImmediate();
        }

        private void LateUpdate()
        {
            if (!UseFarViewBake)
            {
                if (_farViewActive)
                    ApplyFarViewState(false);
                UpdateFarViewHud();
                return;
            }

            ApplyFarViewLoadingVisibility();
            if (_generateRoutine != null)
            {
                UpdateFarViewHud();
                return;
            }

            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            bool shouldUse = ShouldUseFarView(cam);
            if (!_farViewHasContent && !_farViewDirty && !_farViewBaking && _farViewBakeRoutine == null && UnityEngine.Application.isPlaying)
            {
                if (FarViewBakeOnStart || shouldUse)
                    QueueFarViewBake();
            }
            if (!_farViewBaking && shouldUse != _farViewActive)
                ApplyFarViewState(shouldUse);

            UpdateFarViewHud();
        }

        private IEnumerator GenerateRoutine()
        {
            ApplyFarViewLoadingVisibility();
            if (!PrepareGeneration(out var rng, out var width, out var height, out var occupied, out var occupiedSet, out var bounds))
            {
                _generateRoutine = null;
                yield break;
            }

            int total = width * height;
            bool preconvert = ConvertGroundTilesRuntime && PreconvertGroundTilesRuntime;
            if (ConvertGroundTilesRuntime)
                _skipRuntimeConversionInResolve = true;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);
            _skipRuntimeConversionInResolve = false;
            if (preconvert)
            {
                TileBase[] converted = groundPalette;
                yield return ConvertGroundPaletteRoutine(groundPalette, result => converted = result);
                groundPalette = converted;
            }
            var treePalette = ApplyTreeWeighting(TreeTiles);
            var treeAccentPalette = ResolveTreeAccentTiles();

            bool directBlockBlockers = BlockingPropsBlockMovement && UseDirectWalkableUpdates;

            bool hasRuleset = TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset);
            int[] layerIndex = null;
            if (hasRuleset && FillGround && _ground != null)
            {
                layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
                yield return ApplyRulesetToGroundRoutine(width, height, false, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
            }
            else if (FillGround && groundPalette != null && groundPalette.Length > 0 && _ground != null)
            {
                int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
                for (int row = 0; row < height; row += rowsPerFrame)
                {
                    int rowCount = Mathf.Min(rowsPerFrame, height - row);
                    FillGroundTilesBlock(_ground, width, row, rowCount, rng, groundPalette);
                    yield return null;
                }
            }
            if (UseBackgroundTilemap && FillBackground && _background != null)
            {
                bool needConvert = BackgroundUseConvertedTiles && !ConvertGroundTilesRuntime;
                TileBase[] conversionPalette = hasRuleset ? CollectUniqueLayerTiles(layers) : groundPalette;
                if (hasRuleset && UseWaterBiome && AutoTerrainGroupByPrefix && groundPalette != null)
                {
                    var waterTiles = ResolveBiomeTilesByName(groundPalette, WaterTileNameKeywords);
                    var waterInteriorTiles = ResolveBiomeTilesByName(groundPalette, WaterInteriorTileNameKeywords);
                    var rockTiles = ResolveBiomeTilesByName(groundPalette, RockTileNameKeywords);
                    conversionPalette = CombineTiles(conversionPalette, waterTiles);
                    conversionPalette = CombineTiles(conversionPalette, waterInteriorTiles);
                    conversionPalette = CombineTiles(conversionPalette, rockTiles);
                }
                Dictionary<TileBase, TileBase> backgroundLookup = null;
                TileBase[] backgroundPalette = conversionPalette ?? groundPalette;
                if (needConvert && backgroundPalette != null && backgroundPalette.Length > 0)
                {
                    backgroundLookup = BuildConvertedLookup(backgroundPalette);
                    backgroundPalette = MapPalette(backgroundPalette, backgroundLookup);
                }
                if (TryConfigureBackground(width, height, backgroundPalette, out var bgWidth, out var bgHeight))
                {
                    if (hasRuleset)
                    {
                        Vector2 cellSize = _backgroundGrid != null ? (Vector2)_backgroundGrid.cellSize : ResolveBackgroundCellSize(backgroundPalette);
                        int[] bgLayerIndex = BuildLayerIndexRect(bgWidth, bgHeight, layers, noiseOffset, cellSize, ruleset);
                        yield return ApplyRulesetToBackgroundRoutine(bgWidth, bgHeight, layers, tileSeed, bgLayerIndex, backgroundLookup);
                    }
                    else
                    {
                        int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
                        for (int row = 0; row < bgHeight; row += rowsPerFrame)
                        {
                            int rowCount = Mathf.Min(rowsPerFrame, bgHeight - row);
                            FillGroundTilesBlock(_background, bgWidth, row, rowCount, rng, backgroundPalette);
                            yield return null;
                        }
                    }
                }
            }

            int blockingTargetCount = BlockingPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(BlockingPropCoverage))
                : BlockingPropCount;
            blockingTargetCount = Mathf.Clamp(blockingTargetCount, 0, total);
            int treeTargetCount = TreeCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeCoverage))
                : TreeCount;
            treeTargetCount = Mathf.Clamp(treeTargetCount, 0, total);
            int treeAccentTargetCount = TreeAccentCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeAccentCoverage))
                : TreeAccentCount;
            treeAccentTargetCount = Mathf.Clamp(treeAccentTargetCount, 0, total);
            bool directBlockProps = PropsBlockMovement && UseDirectWalkableUpdates;
            bool directBlockTrees = TreesBlockMovement && UseDirectWalkableUpdates;
            bool useOptimizedProps = UseOptimizedPropPlacement;
            int rockPropTargetCount = RockPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(RockPropCoverage))
                : RockPropCount;
            rockPropTargetCount = Mathf.Clamp(rockPropTargetCount, 0, total);
            int propTargetCount = PropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(PropCoverage))
                : PropCount;
            propTargetCount = Mathf.Clamp(propTargetCount, 0, total);
            if (useOptimizedProps)
            {
                BuildPropCandidateCaches(width, height, out var landCells, out var rockCells, out var anyCells);
                int maxMinDistance = Mathf.Max(BlockingMinHexDistance, TreeMinHexDistance, RockPropMinHexDistance, PropMinHexDistance);
                var hash = maxMinDistance > 0 ? new SpatialHash(maxMinDistance) : null;
                var blockingPlacements = new List<Placement>(blockingTargetCount);
                var treePlacements = new List<Placement>(treeTargetCount);
                var treeAccentPlacements = new List<Placement>(treeAccentTargetCount);
                var rockPlacements = new List<Placement>(rockPropTargetCount);
                var propPlacements = new List<Placement>(propTargetCount);
                var boostPlacements = new List<Placement>();

                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                    yield return BuildPlacementsBatched(blockingPlacements, anyCells, blockingTargetCount, BlockingMinHexDistance, rng, blockingPalette, occupied, occupiedSet, hash, null, directBlockBlockers, bounds);
                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                    yield return BuildPlacementsBatched(treePlacements, landCells, treeTargetCount, TreeMinHexDistance, rng, treePalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                    yield return BuildPlacementsBatched(treeAccentPlacements, landCells, treeAccentTargetCount, TreeMinHexDistance, rng, treeAccentPalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                    yield return BuildPlacementsBatched(rockPlacements, rockCells, rockPropTargetCount, RockPropMinHexDistance, rng, RockPropTiles, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                    yield return BuildPlacementsBatched(propPlacements, landCells, propTargetCount, PropMinHexDistance, rng, propPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            yield return BuildPlacementsBatched(boostPlacements, landCells, boostTargetCount, PropMinHexDistance, rng, boostPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                    }
                }

                ApplyPlacements(_blockers, blockingPlacements);
                Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                ApplyPlacements(treeMap, treePlacements);
                ApplyPlacements(treeMap, treeAccentPlacements);
                ApplyPlacements(_props, rockPlacements);
                ApplyPlacements(_props, propPlacements);
                ApplyPlacements(_props, boostPlacements);
            }
            else
            {
                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                {
                    yield return PlacePropsBatched(_blockers, width, height, rng, blockingPalette, blockingTargetCount, BlockingMinHexDistance, occupied, occupiedSet, bounds, directBlockBlockers, PropBiomeFilter.Any);
                }

                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                {
                    Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                    if (treeMap != null)
                    {
                        yield return PlaceTreesBatched(treeMap, width, height, rng, treePalette, treeTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees);
                        if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                            yield return PlaceTreesBatched(treeMap, width, height, rng, treeAccentPalette, treeAccentTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees);
                    }
                }

                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                {
                    yield return PlacePropsBatched(_props, width, height, rng, RockPropTiles, rockPropTargetCount, RockPropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.RockOnly);
                }

                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    yield return PlacePropsBatched(_props, width, height, rng, propPalette, propTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly);
                }
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            yield return PlacePropsBatched(_props, width, height, rng, boostPalette, boostTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly);
                    }
                }
            }


            int blockerBakeCount = blockingTargetCount + (TreesBlockMovement ? treeTargetCount : 0);
            if (BuildBlockingColliders)
            {
                if (blockerBakeCount > 0 && _blockers != null)
                {
                    EnsurePropCollider(_blockers);
                    ApplyObstacleLayer(_blockers);
                }
                if (propTargetCount > 0 && _props != null && PropsBlockMovement)
                {
                    EnsurePropCollider(_props);
                    ApplyObstacleLayer(_props);
                }
            }
            BakeBlockingIfNeeded(blockerBakeCount, propTargetCount, bounds);
            CenterCameraOnMap(width, height);
            if (UseFarViewBake && FarViewBakeOnStart)
                QueueFarViewBake();
            _generateRoutine = null;
        }

        private void GenerateImmediate()
        {
            if (!PrepareGeneration(out var rng, out var width, out var height, out var occupied, out var occupiedSet, out var bounds))
                return;

            int total = width * height;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);
            var treePalette = ApplyTreeWeighting(TreeTiles);
            var treeAccentPalette = ResolveTreeAccentTiles();

            bool directBlockBlockers = BlockingPropsBlockMovement && UseDirectWalkableUpdates;

            bool hasRuleset = TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset);
            int[] layerIndex = null;
            if (hasRuleset && FillGround && _ground != null)
            {
                layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
                ApplyRulesetToGround(width, height, false, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
            }
            else if (FillGround && groundPalette != null && groundPalette.Length > 0 && _ground != null)
            {
                FillGroundTiles(_ground, width, height, rng, groundPalette);
            }
            if (UseBackgroundTilemap && FillBackground && _background != null)
            {
                bool needConvert = BackgroundUseConvertedTiles && !ConvertGroundTilesRuntime;
                TileBase[] conversionPalette = hasRuleset ? CollectUniqueLayerTiles(layers) : groundPalette;
                if (hasRuleset && UseWaterBiome && AutoTerrainGroupByPrefix && groundPalette != null)
                {
                    var waterTiles = ResolveBiomeTilesByName(groundPalette, WaterTileNameKeywords);
                    var waterInteriorTiles = ResolveBiomeTilesByName(groundPalette, WaterInteriorTileNameKeywords);
                    var rockTiles = ResolveBiomeTilesByName(groundPalette, RockTileNameKeywords);
                    conversionPalette = CombineTiles(conversionPalette, waterTiles);
                    conversionPalette = CombineTiles(conversionPalette, waterInteriorTiles);
                    conversionPalette = CombineTiles(conversionPalette, rockTiles);
                }
                Dictionary<TileBase, TileBase> backgroundLookup = null;
                TileBase[] backgroundPalette = conversionPalette ?? groundPalette;
                if (needConvert && backgroundPalette != null && backgroundPalette.Length > 0)
                {
                    backgroundLookup = BuildConvertedLookup(backgroundPalette);
                    backgroundPalette = MapPalette(backgroundPalette, backgroundLookup);
                }
                if (TryConfigureBackground(width, height, backgroundPalette, out var bgWidth, out var bgHeight))
                {
                    if (hasRuleset)
                    {
                        Vector2 cellSize = _backgroundGrid != null ? (Vector2)_backgroundGrid.cellSize : ResolveBackgroundCellSize(backgroundPalette);
                        int[] bgLayerIndex = BuildLayerIndexRect(bgWidth, bgHeight, layers, noiseOffset, cellSize, ruleset);
                        ApplyRulesetToBackground(bgWidth, bgHeight, layers, tileSeed, bgLayerIndex, backgroundLookup);
                    }
                    else
                    {
                        FillGroundTiles(_background, bgWidth, bgHeight, rng, backgroundPalette);
                    }
                }
            }

            int blockingTargetCount = BlockingPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(BlockingPropCoverage))
                : BlockingPropCount;
            blockingTargetCount = Mathf.Clamp(blockingTargetCount, 0, total);
            int treeTargetCount = TreeCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeCoverage))
                : TreeCount;
            treeTargetCount = Mathf.Clamp(treeTargetCount, 0, total);
            int treeAccentTargetCount = TreeAccentCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeAccentCoverage))
                : TreeAccentCount;
            treeAccentTargetCount = Mathf.Clamp(treeAccentTargetCount, 0, total);
            bool directBlockProps = PropsBlockMovement && UseDirectWalkableUpdates;
            bool directBlockTrees = TreesBlockMovement && UseDirectWalkableUpdates;
            bool useOptimizedProps = UseOptimizedPropPlacement;
            int rockPropTargetCount = RockPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(RockPropCoverage))
                : RockPropCount;
            rockPropTargetCount = Mathf.Clamp(rockPropTargetCount, 0, total);
            int propTargetCount = PropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(PropCoverage))
                : PropCount;
            propTargetCount = Mathf.Clamp(propTargetCount, 0, total);
            if (useOptimizedProps)
            {
                BuildPropCandidateCaches(width, height, out var landCells, out var rockCells, out var anyCells);
                int maxMinDistance = Mathf.Max(BlockingMinHexDistance, TreeMinHexDistance, RockPropMinHexDistance, PropMinHexDistance);
                var hash = maxMinDistance > 0 ? new SpatialHash(maxMinDistance) : null;
                var blockingPlacements = new List<Placement>(blockingTargetCount);
                var treePlacements = new List<Placement>(treeTargetCount);
                var treeAccentPlacements = new List<Placement>(treeAccentTargetCount);
                var rockPlacements = new List<Placement>(rockPropTargetCount);
                var propPlacements = new List<Placement>(propTargetCount);
                var boostPlacements = new List<Placement>();

                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                    BuildPlacements(blockingPlacements, anyCells, blockingTargetCount, BlockingMinHexDistance, rng, blockingPalette, occupied, occupiedSet, hash, null, directBlockBlockers, bounds);
                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                    BuildPlacements(treePlacements, landCells, treeTargetCount, TreeMinHexDistance, rng, treePalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                    BuildPlacements(treeAccentPlacements, landCells, treeAccentTargetCount, TreeMinHexDistance, rng, treeAccentPalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                    BuildPlacements(rockPlacements, rockCells, rockPropTargetCount, RockPropMinHexDistance, rng, RockPropTiles, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                    BuildPlacements(propPlacements, landCells, propTargetCount, PropMinHexDistance, rng, propPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            BuildPlacements(boostPlacements, landCells, boostTargetCount, PropMinHexDistance, rng, boostPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                    }
                }

                ApplyPlacements(_blockers, blockingPlacements);
                Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                ApplyPlacements(treeMap, treePlacements);
                ApplyPlacements(treeMap, treeAccentPlacements);
                ApplyPlacements(_props, rockPlacements);
                ApplyPlacements(_props, propPlacements);
                ApplyPlacements(_props, boostPlacements);
            }
            else
            {
                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                {
                    PlaceProps(_blockers, width, height, rng, blockingPalette, blockingTargetCount, BlockingMinHexDistance, occupied, occupiedSet, bounds, directBlockBlockers, PropBiomeFilter.Any);
                }

                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                {
                    Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                    if (treeMap != null)
                    {
                        PlaceTrees(treeMap, width, height, rng, treePalette, treeTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees);
                        if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                            PlaceTrees(treeMap, width, height, rng, treeAccentPalette, treeAccentTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees);
                    }
                }

                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                {
                    PlaceProps(_props, width, height, rng, RockPropTiles, rockPropTargetCount, RockPropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.RockOnly);
                }

                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    PlaceProps(_props, width, height, rng, propPalette, propTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly);
                }
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            PlaceProps(_props, width, height, rng, boostPalette, boostTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly);
                    }
                }
            }



            int blockerBakeCount = blockingTargetCount + (TreesBlockMovement ? treeTargetCount : 0);
            if (BuildBlockingColliders)
            {
                if (blockerBakeCount > 0 && _blockers != null)
                {
                    EnsurePropCollider(_blockers);
                    ApplyObstacleLayer(_blockers);
                }
                if (propTargetCount > 0 && _props != null && PropsBlockMovement)
                {
                    EnsurePropCollider(_props);
                    ApplyObstacleLayer(_props);
                }
            }
            BakeBlockingIfNeeded(blockerBakeCount, propTargetCount, bounds);
            CenterCameraOnMap(width, height);
            if (UseFarViewBake && FarViewBakeOnStart)
                QueueFarViewBake();
        }

        private bool PrepareGeneration(out System.Random rng, out int width, out int height, out List<Vector2Int> occupied,
            out HashSet<Vector2Int> occupiedSet, out BlockBounds bounds)
        {
            rng = UseRandomSeed ? new System.Random() : new System.Random(Seed);
            width = 0;
            height = 0;
            occupied = new List<Vector2Int>(256);
            occupiedSet = new HashSet<Vector2Int>();
            bounds = _blockBounds;

            if (!Enabled) return false;
            EnsureRefs();
            if (_hex == null) return false;

            EnsureGrid();
            EnsureTilemaps();
            if (UseBackgroundTilemap)
            {
                EnsureBackgroundGrid();
                EnsureBackgroundTilemap();
            }
            ApplyDebugOutlineVisibility();
            if (UseFarViewBake)
            {
                if (_farViewBakeRoutine != null)
                {
                    StopCoroutine(_farViewBakeRoutine);
                    _farViewBakeRoutine = null;
                }
                _farViewDirty = false;
                EnsureFarViewObjects();
                ApplyFarViewState(false);
            }

            if (ClearBeforeGenerate)
            {
                _ground?.ClearAllTiles();
                _background?.ClearAllTiles();
                _props?.ClearAllTiles();
                _blockers?.ClearAllTiles();
                _transitions?.ClearAllTiles();
                if (UseDirectWalkableUpdates)
                    ClearDirectBlocks();
            }

            width = Mathf.Max(0, _hex.Width);
            height = Mathf.Max(0, _hex.Height);
            if (width == 0 || height == 0) return false;

            if (UseGroundNoise)
            {
                if (RandomizeGroundNoiseOffset)
                {
                    _groundNoiseOffset = new Vector2(
                        (float)rng.NextDouble() * 1000f,
                        (float)rng.NextDouble() * 1000f);
                }
                else
                {
                    _groundNoiseOffset = GroundNoiseOffset;
                }
            }
            if (UseTreeBiomeNoise)
            {
                if (TreeBiomeRandomizeOffset)
                {
                    _treeNoiseOffset = new Vector2(
                        (float)rng.NextDouble() * 1000f,
                        (float)rng.NextDouble() * 1000f);
                }
                else
                {
                    _treeNoiseOffset = TreeBiomeNoiseOffset;
                }
            }

            bounds.Reset(width, height);
            return true;
        }

        private void EnsureRefs()
        {
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
        }

        private void EnsureGrid()
        {
            if (_grid != null) return;
            var existing = GameObject.Find(GridObjectName);
            if (existing != null)
                _grid = existing.GetComponent<Grid>();
            if (_grid == null)
            {
                var go = existing ?? new GameObject(GridObjectName);
                _grid = go.GetComponent<Grid>();
                if (_grid == null) _grid = go.AddComponent<Grid>();
            }

            _grid.cellLayout = CellLayout;
            _grid.cellSize = ResolveCellSize();
            _grid.transform.position = new Vector3(_hex.Origin.x, _hex.Origin.y, 0f);
        }

        private void EnsureBackgroundGrid()
        {
            if (_backgroundGrid != null) return;
            var existing = GameObject.Find(BackgroundGridName);
            if (existing != null)
                _backgroundGrid = existing.GetComponent<Grid>();
            if (_backgroundGrid == null)
            {
                var go = existing ?? new GameObject(BackgroundGridName);
                _backgroundGrid = go.GetComponent<Grid>();
                if (_backgroundGrid == null) _backgroundGrid = go.AddComponent<Grid>();
            }
            _backgroundGrid.cellLayout = GridLayout.CellLayout.Rectangle;
            _backgroundGrid.cellSize = Vector3.one;
            _backgroundGrid.transform.position = new Vector3(_hex.Origin.x, _hex.Origin.y, 0f);
        }

        private void EnsureTilemaps()
        {
            _ground = FindOrCreateTilemap(_grid.transform, GroundTilemapName, GroundSortingOrder);
            _props = FindOrCreateTilemap(_grid.transform, PropTilemapName, PropSortingOrder);
            _blockers = FindOrCreateTilemap(_grid.transform, BlockerTilemapName, BlockerSortingOrder);
            _transitions = null;
            if (UseTerrainRuleset && UseTransitionTilemap && !string.IsNullOrEmpty(TransitionTilemapName))
                _transitions = FindOrCreateTilemap(_grid.transform, TransitionTilemapName, TransitionSortingOrder);
            ApplyTilemapRendererMode(_ground);
            ApplyTilemapRendererMode(_props);
            ApplyTilemapRendererMode(_blockers);
            ApplyTilemapRendererMode(_transitions);
        }

        private void EnsureBackgroundTilemap()
        {
            if (_backgroundGrid == null || string.IsNullOrEmpty(BackgroundTilemapName)) return;
            _background = FindOrCreateTilemap(_backgroundGrid.transform, BackgroundTilemapName, BackgroundSortingOrder);
            ApplyTilemapRendererMode(_background);
        }

        private void CenterCameraOnMap(int width, int height)
        {
            if (!CenterCameraOnGenerate) return;
            var cam = Camera.main;
            if (cam == null || _hex == null || width <= 0 || height <= 0) return;
            ComputeHexWorldBounds(width, height, out var min, out var max);
            var center = (min + max) * 0.5f;
            var pos = cam.transform.position;
            cam.transform.position = new Vector3(center.x, center.y, pos.z);
        }

        private void ApplyDebugOutlineVisibility()
        {
            bool debug = GroundTileDebugOutlineOnly;
            bool allow = !_farViewActive;
            SetTilemapRendererEnabled(_ground, allow && !debug);
            SetTilemapRendererEnabled(_props, allow && !debug);
            SetTilemapRendererEnabled(_blockers, allow && !debug);
            SetTilemapRendererEnabled(_transitions, allow && !debug);
            SetTilemapRendererEnabled(_background, allow && UseBackgroundTilemap);
        }

        private static void SetTilemapRendererEnabled(Tilemap map, bool enabled)
        {
            if (map == null) return;
            var renderer = map.GetComponent<TilemapRenderer>();
            if (renderer != null)
                renderer.enabled = enabled;
        }

        private void ApplyTilemapRendererMode(Tilemap map)
        {
            if (map == null) return;
            var renderer = map.GetComponent<TilemapRenderer>();
            if (renderer == null) return;
            renderer.mode = UseTilemapChunkMode ? TilemapRenderer.Mode.Chunk : TilemapRenderer.Mode.Individual;
        }

        private void QueueFarViewBake()
        {
            if (!UseFarViewBake) return;
            if (_farViewBaking || _farViewBakeRoutine != null) return;
            if (_farViewDirty) return;
            _farViewDirty = true;
            if (UnityEngine.Application.isPlaying)
            {
                if (_farViewBakeRoutine != null)
                    StopCoroutine(_farViewBakeRoutine);
                _farViewBakeRoutine = StartCoroutine(BakeFarViewNextFrame());
            }
            else
                BakeFarViewTexture();
        }

        private IEnumerator BakeFarViewNextFrame()
        {
            yield return null;
            BakeFarViewTexture();
            _farViewBakeRoutine = null;
        }

        private void BakeFarViewTexture()
        {
            if (!UseFarViewBake)
            {
                _farViewDirty = false;
                _farViewBakeRoutine = null;
                return;
            }

            EnsureFarViewObjects();
            ClearFarViewReadbacks();
            _farViewDirty = false;
            _farViewBakeRoutine = null;

            var renderers = CollectFarViewRenderers();
            if (!TryGetFarViewBounds(renderers, out var bounds))
            {
                _farViewHasContent = false;
                ApplyFarViewState(false);
                return;
            }

            _farViewBounds = bounds;
            var rendererStates = new List<RendererState>(renderers.Count);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                rendererStates.Add(new RendererState(r, r.enabled));
                r.enabled = true;
            }

            bool prevFarViewEnabled = _farViewRenderer != null && _farViewRenderer.enabled;
            if (_farViewRenderer != null)
                _farViewRenderer.enabled = false;

            List<(GameObject go, int layer)> layerRestore = null;
            List<Renderer> disabledRenderers = null;
            int bakeLayer = ResolveFarViewBakeLayer();
            if (bakeLayer >= 0)
            {
                layerRestore = new List<(GameObject, int)>(renderers.Count);
                for (int i = 0; i < renderers.Count; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    layerRestore.Add((r.gameObject, r.gameObject.layer));
                    r.gameObject.layer = bakeLayer;
                }
                _farViewCamera.cullingMask = 1 << bakeLayer;
            }
            else
            {
                var keep = new HashSet<Renderer>(renderers);
                if (_farViewRenderer != null)
                    keep.Add(_farViewRenderer);
                disabledRenderers = DisableNonTilemapRenderers(keep);
                _farViewCamera.cullingMask = ~0;
            }

            if (UseFarViewChunkedBake && UnityEngine.Application.isPlaying)
            {
                if (_farViewBakeRoutine != null)
                    StopCoroutine(_farViewBakeRoutine);
                _farViewBakeRoutine = StartCoroutine(BakeFarViewChunksRoutine(
                    bounds,
                    renderers,
                    rendererStates,
                    layerRestore,
                    bakeLayer,
                    prevFarViewEnabled));
                return;
            }

            if (UseFarViewChunkedBake)
            {
                BakeFarViewChunksSync(bounds, renderers, bakeLayer);
            }
            else
            {
                BeginFarViewBakeStats(1);
                int texWidth = Mathf.CeilToInt(bounds.size.x * Mathf.Max(1, FarViewPixelsPerUnit));
                int texHeight = Mathf.CeilToInt(bounds.size.y * Mathf.Max(1, FarViewPixelsPerUnit));
                texWidth = Mathf.Clamp(texWidth, 64, Mathf.Max(64, FarViewMaxTextureSize));
                texHeight = Mathf.Clamp(texHeight, 64, Mathf.Max(64, FarViewMaxTextureSize));
                EnsureFarViewTexture(texWidth, texHeight);

                ConfigureFarViewCamera(bounds);
                _farViewCamera.targetTexture = _farViewTexture;
                _farViewCamera.Render();
                _farViewCamera.targetTexture = null;
                UpdateFarViewSprite(texWidth, texHeight);
                UpdateFarViewQuad(bounds);
                _farViewHasContent = _farViewSprite != null;
                MarkFarViewChunkSubmitted();
                MarkFarViewChunkCompleted();
            }

            if (layerRestore != null)
            {
                for (int i = 0; i < layerRestore.Count; i++)
                {
                    var entry = layerRestore[i];
                    if (entry.go != null)
                        entry.go.layer = entry.layer;
                }
            }
            if (disabledRenderers != null)
                RestoreRenderers(disabledRenderers);

            for (int i = 0; i < rendererStates.Count; i++)
            {
                var state = rendererStates[i];
                if (state.Renderer != null)
                    state.Renderer.enabled = state.Enabled;
            }

            if (_farViewRenderer != null)
                _farViewRenderer.enabled = prevFarViewEnabled;

            var cam = Camera.main;
            if (cam != null && cam.orthographic)
                ApplyFarViewState(ShouldUseFarView(cam));
            else
                ApplyFarViewState(false);
        }

        private void EnsureFarViewObjects()
        {
            if (_farViewRoot == null)
            {
                var existing = GameObject.Find("FarViewBaked (Auto)");
                _farViewRoot = existing ?? new GameObject("FarViewBaked (Auto)");
                _farViewRoot.transform.SetParent(transform, false);
            }

            var renderer = _farViewRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = _farViewRoot.AddComponent<SpriteRenderer>();
            _farViewRenderer = renderer;
            _farViewRenderer.enabled = false;
            _farViewRenderer.sortingOrder = FarViewSortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                _farViewRenderer.sortingLayerName = SortingLayerName;
            if (UseFarViewChunkedBake)
                _farViewRenderer.enabled = false;

            var existingCam = _farViewRoot.transform.Find("FarViewBakeCamera (Auto)");
            var camGo = existingCam != null ? existingCam.gameObject : new GameObject("FarViewBakeCamera (Auto)");
            camGo.transform.SetParent(_farViewRoot.transform, false);
            var cam = camGo.GetComponent<Camera>();
            if (cam == null)
                cam = camGo.AddComponent<Camera>();
            _farViewCamera = cam;

            if (_farViewCamera == null) return;
            _farViewCamera.enabled = false;
            _farViewCamera.orthographic = true;
            _farViewCamera.clearFlags = CameraClearFlags.SolidColor;
            _farViewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _farViewCamera.nearClipPlane = 0.01f;
            _farViewCamera.farClipPlane = 1000f;
        }

        private void BakeFarViewChunks(Bounds bounds)
        {
            float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
            int maxPixels = Mathf.Max(256, Mathf.Min(FarViewChunkPixels, FarViewMaxTextureSize));
            float chunkWorld = Mathf.Max(1f, maxPixels / ppu);
            int chunksX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / chunkWorld));
            int chunksY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / chunkWorld));
            BeginFarViewBakeStats(chunksX * chunksY);

            EnsureFarViewChunkList(chunksX * chunksY);
            SetFarViewChunkVisibility(false);
            _farViewHasContent = false;

            int idx = 0;
            bool any = false;
            for (int y = 0; y < chunksY; y++)
            {
                for (int x = 0; x < chunksX; x++)
                {
                    float minX = bounds.min.x + (x * chunkWorld);
                    float minY = bounds.min.y + (y * chunkWorld);
                    float sizeX = Mathf.Min(chunkWorld, bounds.max.x - minX);
                    float sizeY = Mathf.Min(chunkWorld, bounds.max.y - minY);
                    if (sizeX <= 0f || sizeY <= 0f) continue;
                    var chunkBounds = new Bounds(
                        new Vector3(minX + (sizeX * 0.5f), minY + (sizeY * 0.5f), 0f),
                        new Vector3(sizeX, sizeY, 0f));

                    int texW = Mathf.Clamp(Mathf.CeilToInt(sizeX * ppu), 64, maxPixels);
                    int texH = Mathf.Clamp(Mathf.CeilToInt(sizeY * ppu), 64, maxPixels);

                    var chunk = _farViewChunks[idx++];
                    if (UseFarViewDirectChunkRender)
                    {
                        var rt = RenderTexture.GetTemporary(
                            texW,
                            texH,
                            16,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Linear);
                        rt.filterMode = FilterMode.Point;
                        rt.wrapMode = TextureWrapMode.Clamp;
                        ConfigureFarViewCamera(chunkBounds);
                        _farViewCamera.targetTexture = rt;
                        _farViewCamera.Render();
                        _farViewCamera.targetTexture = null;
                        UpdateChunkDirectRender(chunk, rt, texW, texH, ppu);
                        RenderTexture.ReleaseTemporary(rt);
                        any = true;
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                    }
                    else
                    {
                        var rt = RenderTexture.GetTemporary(
                            texW,
                            texH,
                            16,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Linear);
                        rt.filterMode = FilterMode.Point;
                        rt.wrapMode = TextureWrapMode.Clamp;

                        ConfigureFarViewCamera(chunkBounds);
                        _farViewCamera.targetTexture = rt;
                        _farViewCamera.Render();
                        _farViewCamera.targetTexture = null;

                        UpdateChunkSprite(chunk, rt, texW, texH, ppu);
                        RenderTexture.ReleaseTemporary(rt);
                        any = true;
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                    }
                    UpdateChunkTransform(chunk, chunkBounds);
                }
            }
            _farViewHasContent = any;
        }

        private void BakeFarViewChunksSync(Bounds bounds, List<Renderer> renderers, int bakeLayer)
        {
            var keep = new HashSet<Renderer>(renderers);
            if (_farViewRenderer != null)
                keep.Add(_farViewRenderer);
            float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
            int maxPixels = Mathf.Max(256, Mathf.Min(FarViewChunkPixels, FarViewMaxTextureSize));
            float chunkWorld = Mathf.Max(1f, maxPixels / ppu);
            int chunksX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / chunkWorld));
            int chunksY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / chunkWorld));
            BeginFarViewBakeStats(chunksX * chunksY);

            EnsureFarViewChunkList(chunksX * chunksY);
            SetFarViewChunkVisibility(false);
            _farViewHasContent = false;

            int idx = 0;
            bool any = false;
            List<Renderer> disabled = null;
            if (bakeLayer < 0)
                disabled = DisableNonTilemapRenderers(keep);
            for (int y = 0; y < chunksY; y++)
            {
                for (int x = 0; x < chunksX; x++)
                {
                    float minX = bounds.min.x + (x * chunkWorld);
                    float minY = bounds.min.y + (y * chunkWorld);
                    float sizeX = Mathf.Min(chunkWorld, bounds.max.x - minX);
                    float sizeY = Mathf.Min(chunkWorld, bounds.max.y - minY);
                    if (sizeX <= 0f || sizeY <= 0f) continue;
                    var chunkBounds = new Bounds(
                        new Vector3(minX + (sizeX * 0.5f), minY + (sizeY * 0.5f), 0f),
                        new Vector3(sizeX, sizeY, 0f));

                    int texW = Mathf.Clamp(Mathf.CeilToInt(sizeX * ppu), 64, maxPixels);
                    int texH = Mathf.Clamp(Mathf.CeilToInt(sizeY * ppu), 64, maxPixels);

                    var chunk = _farViewChunks[idx++];
                    if (UseFarViewDirectChunkRender)
                    {
                        var rt = RenderTexture.GetTemporary(
                            texW,
                            texH,
                            16,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Linear);
                        rt.filterMode = FilterMode.Point;
                        rt.wrapMode = TextureWrapMode.Clamp;
                        ConfigureFarViewCamera(chunkBounds);
                        _farViewCamera.targetTexture = rt;
                        _farViewCamera.Render();
                        _farViewCamera.targetTexture = null;
                        UpdateChunkDirectRender(chunk, rt, texW, texH, ppu);
                        RenderTexture.ReleaseTemporary(rt);
                        any = true;
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                    }
                    else
                    {
                        var rt = RenderTexture.GetTemporary(
                            texW,
                            texH,
                            16,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Linear);
                        rt.filterMode = FilterMode.Point;
                        rt.wrapMode = TextureWrapMode.Clamp;

                        ConfigureFarViewCamera(chunkBounds);
                        _farViewCamera.targetTexture = rt;
                        _farViewCamera.Render();
                        _farViewCamera.targetTexture = null;

                        UpdateChunkSprite(chunk, rt, texW, texH, ppu);
                        RenderTexture.ReleaseTemporary(rt);
                        any = true;
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                    }
                    UpdateChunkTransform(chunk, chunkBounds);
                }
            }
            if (disabled != null)
                RestoreRenderers(disabled);
            _farViewHasContent = any;
        }

        private IEnumerator BakeFarViewChunksRoutine(
            Bounds bounds,
            List<Renderer> renderers,
            List<RendererState> rendererStates,
            List<(GameObject go, int layer)> layerRestore,
            int bakeLayer,
            bool prevFarViewEnabled)
        {
            var keep = new HashSet<Renderer>(renderers);
            if (_farViewRenderer != null)
                keep.Add(_farViewRenderer);
            float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
            int maxPixels = Mathf.Max(256, Mathf.Min(FarViewChunkPixels, FarViewMaxTextureSize));
            float chunkWorld = Mathf.Max(1f, maxPixels / ppu);
            int chunksX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / chunkWorld));
            int chunksY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / chunkWorld));
            bool useDirect = UseFarViewDirectChunkRender;
            bool useAsyncReadback = !useDirect && UseFarViewAsyncReadback && SystemInfo.supportsAsyncGPUReadback;
            float frameBudgetMs = Mathf.Max(0f, FarViewBakeFrameBudgetMs);
            int perFrame = Mathf.Max(1, FarViewChunksPerFrame);
            if (frameBudgetMs > 0f)
                perFrame = int.MaxValue;

            EnsureFarViewChunkList(chunksX * chunksY);
            SetFarViewChunkVisibility(false);
            _farViewHasContent = false;
            _farViewBaking = true;
            ApplyFarViewLoadingVisibility();
            ClearFarViewReadbacks();
            BeginFarViewBakeStats(chunksX * chunksY);

            int idx = 0;
            int processed = 0;
            bool any = false;
            List<Renderer> disabled = null;
            if (bakeLayer < 0)
                disabled = DisableNonTilemapRenderers(keep);
            int maxPending = Mathf.Max(1, FarViewMaxPendingReadbacks);
            float frameStart = Time.realtimeSinceStartup;
            int processedThisFrame = 0;
            for (int y = 0; y < chunksY; y++)
            {
                for (int x = 0; x < chunksX; x++)
                {
                    float minX = bounds.min.x + (x * chunkWorld);
                    float minY = bounds.min.y + (y * chunkWorld);
                    float sizeX = Mathf.Min(chunkWorld, bounds.max.x - minX);
                    float sizeY = Mathf.Min(chunkWorld, bounds.max.y - minY);
                    if (sizeX <= 0f || sizeY <= 0f) continue;
                    var chunkBounds = new Bounds(
                        new Vector3(minX + (sizeX * 0.5f), minY + (sizeY * 0.5f), 0f),
                        new Vector3(sizeX, sizeY, 0f));

                    int texW = Mathf.Clamp(Mathf.CeilToInt(sizeX * ppu), 64, maxPixels);
                    int texH = Mathf.Clamp(Mathf.CeilToInt(sizeY * ppu), 64, maxPixels);

                    if (useAsyncReadback)
                    {
                        while (_farViewReadbacks.Count >= maxPending)
                        {
                            ProcessFarViewReadbacks(ref any);
                            yield return null;
                        }
                    }

                    var chunk = _farViewChunks[idx++];
                    if (useDirect)
                    {
                        var rt = RenderTexture.GetTemporary(
                            texW,
                            texH,
                            16,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Linear);
                        rt.filterMode = FilterMode.Point;
                        rt.wrapMode = TextureWrapMode.Clamp;
                        ConfigureFarViewCamera(chunkBounds);
                        _farViewCamera.targetTexture = rt;
                        _farViewCamera.Render();
                        _farViewCamera.targetTexture = null;
                        UpdateChunkDirectRender(chunk, rt, texW, texH, ppu);
                        RenderTexture.ReleaseTemporary(rt);
                        any = true;
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                    }
                    else
                    {
                        var rt = RenderTexture.GetTemporary(
                            texW,
                            texH,
                            16,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Linear);
                        rt.filterMode = FilterMode.Point;
                        rt.wrapMode = TextureWrapMode.Clamp;

                        ConfigureFarViewCamera(chunkBounds);
                        _farViewCamera.targetTexture = rt;
                        _farViewCamera.Render();
                        _farViewCamera.targetTexture = null;

                        if (useAsyncReadback)
                        {
                            var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
                            _farViewReadbacks.Add(new FarViewReadback
                            {
                                Request = request,
                                RenderTexture = rt,
                                Chunk = chunk,
                                Width = texW,
                                Height = texH,
                                Ppu = ppu,
                                Bounds = chunkBounds,
                                BakeVersion = _farViewBakeVersion,
                                SubmittedTime = Time.realtimeSinceStartup
                            });
                            MarkFarViewChunkSubmitted();
                        }
                        else
                        {
                            UpdateChunkSprite(chunk, rt, texW, texH, ppu);
                            RenderTexture.ReleaseTemporary(rt);
                            any = true;
                            MarkFarViewChunkSubmitted();
                            MarkFarViewChunkCompleted();
                        }
                    }
                    UpdateChunkTransform(chunk, chunkBounds);

                    processed++;
                    processedThisFrame++;
                    if (processedThisFrame >= perFrame || (frameBudgetMs > 0f && (Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs))
                    {
                        ProcessFarViewReadbacks(ref any);
                        yield return null;
                        frameStart = Time.realtimeSinceStartup;
                        processedThisFrame = 0;
                    }
                }
            }

            if (useAsyncReadback)
            {
                while (_farViewReadbacks.Count > 0)
                {
                    ProcessFarViewReadbacks(ref any);
                    yield return null;
                }
            }

            if (disabled != null)
                RestoreRenderers(disabled);
            _farViewHasContent = any;
            _farViewBaking = false;

            if (layerRestore != null)
            {
                for (int i = 0; i < layerRestore.Count; i++)
                {
                    var entry = layerRestore[i];
                    if (entry.go != null)
                        entry.go.layer = entry.layer;
                }
            }

            for (int i = 0; i < rendererStates.Count; i++)
            {
                var state = rendererStates[i];
                if (state.Renderer != null)
                    state.Renderer.enabled = state.Enabled;
            }

            if (_farViewRenderer != null)
                _farViewRenderer.enabled = prevFarViewEnabled;

            ApplyFarViewState(ShouldUseFarView(Camera.main));
            _farViewBakeRoutine = null;
        }

        private void EnsureFarViewChunkList(int count)
        {
            for (int i = _farViewChunks.Count; i < count; i++)
            {
                var go = new GameObject($"FarViewChunk {i}");
                go.transform.SetParent(_farViewRoot.transform, false);
                _farViewChunks.Add(new FarViewChunk
                {
                    Root = go,
                    Renderer = null
                });
            }
            for (int i = 0; i < _farViewChunks.Count; i++)
                _farViewChunks[i].Active = i < count;

            for (int i = 0; i < count; i++)
                EnsureChunkRenderer(_farViewChunks[i]);
        }

        private void EnsureChunkRenderer(FarViewChunk chunk)
        {
            if (chunk.Renderer == null)
            {
                chunk.Renderer = chunk.Root.GetComponent<SpriteRenderer>();
                if (chunk.Renderer == null)
                    chunk.Renderer = chunk.Root.AddComponent<SpriteRenderer>();
            }
            chunk.Renderer.enabled = false;
            chunk.Renderer.sortingOrder = FarViewSortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                chunk.Renderer.sortingLayerName = SortingLayerName;
        }

        private void EnsureChunkTexture(FarViewChunk chunk, int width, int height)
        {
            if (chunk.Texture != null && chunk.Texture.width == width && chunk.Texture.height == height)
                return;
            if (chunk.Texture != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(chunk.Texture);
                else
                    DestroyImmediate(chunk.Texture);
            }
            chunk.Texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private void EnsureChunkSprite(FarViewChunk chunk, int width, int height, float ppu)
        {
            if (chunk.Sprite == null || chunk.Sprite.texture != chunk.Texture)
            {
                if (chunk.Sprite != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Sprite);
                    else
                        DestroyImmediate(chunk.Sprite);
                }
                chunk.Sprite = Sprite.Create(chunk.Texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
            }
            chunk.Renderer.sprite = chunk.Sprite;
        }

        private void UpdateChunkDirectRender(FarViewChunk chunk, RenderTexture rt, int width, int height, float ppu)
        {
            if (!chunk.Active || chunk.Renderer == null) return;
            EnsureChunkTexture(chunk, width, height);
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) != 0)
            {
                Graphics.CopyTexture(rt, chunk.Texture);
            }
            else
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                chunk.Texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                chunk.Texture.Apply();
                RenderTexture.active = prev;
            }
            EnsureChunkSprite(chunk, width, height, ppu);
        }

        private void UpdateChunkSprite(FarViewChunk chunk, RenderTexture rt, int width, int height, float ppu)
        {
            if (!chunk.Active || chunk.Renderer == null) return;
            EnsureChunkTexture(chunk, width, height);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            chunk.Texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            chunk.Texture.Apply();
            RenderTexture.active = prev;

            EnsureChunkSprite(chunk, width, height, ppu);
        }

        private void UpdateChunkSpriteFromReadback(FarViewChunk chunk, AsyncGPUReadbackRequest request, int width, int height, float ppu)
        {
            if (!chunk.Active || chunk.Renderer == null || request.hasError) return;
            EnsureChunkTexture(chunk, width, height);
            var data = request.GetData<byte>();
            if (data.Length != width * height * 4)
                return;
            chunk.Texture.LoadRawTextureData(data);
            chunk.Texture.Apply();
            EnsureChunkSprite(chunk, width, height, ppu);
        }

        private void UpdateChunkTransform(FarViewChunk chunk, Bounds bounds)
        {
            if (chunk.Root == null) return;
            chunk.Root.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            chunk.Root.transform.localScale = Vector3.one;
        }

        private void BeginFarViewBakeStats(int totalChunks)
        {
            _farViewChunkTotal = Mathf.Max(0, totalChunks);
            _farViewChunkSubmitted = 0;
            _farViewChunkCompleted = 0;
            _farViewChunkErrors = 0;
            _farViewBakeStartTime = Time.realtimeSinceStartup;
        }

        private void MarkFarViewChunkSubmitted()
        {
            _farViewChunkSubmitted++;
        }

        private void MarkFarViewChunkCompleted()
        {
            _farViewChunkCompleted++;
        }

        private void MarkFarViewChunkError()
        {
            _farViewChunkErrors++;
            _farViewChunkCompleted++;
        }

        private void ClearFarViewReadbacks()
        {
            for (int i = 0; i < _farViewReadbacks.Count; i++)
            {
                var entry = _farViewReadbacks[i];
                if (entry.RenderTexture != null)
                    RenderTexture.ReleaseTemporary(entry.RenderTexture);
            }
            _farViewReadbacks.Clear();
            _farViewBakeVersion++;
        }

        private void ProcessFarViewReadbacks(ref bool any)
        {
            if (_farViewReadbacks.Count == 0) return;
            int budget = Mathf.Max(1, FarViewReadbacksPerFrame);
            float now = Time.realtimeSinceStartup;
            for (int i = _farViewReadbacks.Count - 1; i >= 0 && budget > 0; i--)
            {
                var entry = _farViewReadbacks[i];
                bool timedOut = FarViewFallbackToSyncReadback
                    && FarViewReadbackTimeout > 0f
                    && (now - entry.SubmittedTime) >= FarViewReadbackTimeout;
                if (!entry.Request.done && !timedOut) continue;
                budget--;
                bool validBake = entry.BakeVersion == _farViewBakeVersion;
                bool requestError = entry.Request.hasError;
                bool useSync = timedOut || requestError || !entry.Request.done;
                if (validBake)
                {
                    if (useSync)
                        UpdateChunkSprite(entry.Chunk, entry.RenderTexture, entry.Width, entry.Height, entry.Ppu);
                    else
                        UpdateChunkSpriteFromReadback(entry.Chunk, entry.Request, entry.Width, entry.Height, entry.Ppu);
                    UpdateChunkTransform(entry.Chunk, entry.Bounds);
                    any = true;
                }
                if (!validBake || requestError || timedOut)
                    MarkFarViewChunkError();
                else
                    MarkFarViewChunkCompleted();
                if (entry.RenderTexture != null)
                    RenderTexture.ReleaseTemporary(entry.RenderTexture);
                _farViewReadbacks.RemoveAt(i);
            }
        }

        private void SetFarViewChunkVisibility(bool visible)
        {
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (chunk.Renderer == null) continue;
                chunk.Renderer.enabled = visible && chunk.Active;
            }
        }

        private void EnsureFarViewTexture(int width, int height)
        {
            if (_farViewTexture != null && _farViewTexture.width == width && _farViewTexture.height == height)
                return;
            ReleaseFarViewTexture();
            _farViewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            _farViewTexture.Create();
        }

        private void ReleaseFarViewTexture()
        {
            ClearFarViewReadbacks();
            if (_farViewTexture != null)
            {
                _farViewTexture.Release();
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewTexture);
                else
                    DestroyImmediate(_farViewTexture);
                _farViewTexture = null;
            }
            if (_farViewSprite != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewSprite);
                else
                    DestroyImmediate(_farViewSprite);
                _farViewSprite = null;
            }
            if (_farViewTexture2D != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewTexture2D);
                else
                    DestroyImmediate(_farViewTexture2D);
                _farViewTexture2D = null;
            }
            if (_farViewRenderer != null)
                _farViewRenderer.sprite = null;
            ClearFarViewChunks();
            _farViewHasContent = false;
            _farViewBaking = false;
        }

        private void ClearFarViewChunks()
        {
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (chunk.Renderer != null)
                    chunk.Renderer.sprite = null;
                if (chunk.Sprite != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Sprite);
                    else
                        DestroyImmediate(chunk.Sprite);
                }
                if (chunk.Texture != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Texture);
                    else
                        DestroyImmediate(chunk.Texture);
                }
                if (chunk.Root != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Root);
                    else
                        DestroyImmediate(chunk.Root);
                }
            }
            _farViewChunks.Clear();
        }

        private List<Renderer> CollectFarViewRenderers()
        {
            var list = new List<Renderer>(6);
            if (FarViewIncludeGround) AddTilemapRenderer(_ground, list);
            if (FarViewIncludeProps) AddTilemapRenderer(_props, list);
            if (FarViewIncludeBlockers) AddTilemapRenderer(_blockers, list);
            if (FarViewIncludeTransitions) AddTilemapRenderer(_transitions, list);
            if (FarViewIncludeBackground && UseBackgroundTilemap) AddTilemapRenderer(_background, list);
            return list;
        }

        private static void AddTilemapRenderer(Tilemap map, List<Renderer> list)
        {
            if (map == null) return;
            var renderer = map.GetComponent<Renderer>();
            if (renderer != null)
                list.Add(renderer);
        }

        private bool TryGetFarViewBounds(List<Renderer> renderers, out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasAny = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var b = r.bounds;
                if (b.size.x <= 0f || b.size.y <= 0f) continue;
                if (!hasAny)
                {
                    bounds = b;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }
            return hasAny;
        }

        private void ConfigureFarViewCamera(Bounds bounds)
        {
            if (_farViewCamera == null) return;
            float sizeY = Mathf.Max(0.01f, bounds.size.y);
            float sizeX = Mathf.Max(0.01f, bounds.size.x);
            _farViewCamera.orthographicSize = sizeY * 0.5f;
            _farViewCamera.aspect = sizeX / sizeY;
            float z = -10f;
            var main = Camera.main;
            if (main != null)
                z = main.transform.position.z;
            _farViewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, z);
        }

        private void UpdateFarViewQuad(Bounds bounds)
        {
            if (_farViewRoot == null) return;
            _farViewRoot.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            if (_farViewSprite != null)
            {
                var size = _farViewSprite.bounds.size;
                if (size.x > 0f && size.y > 0f)
                {
                    _farViewRoot.transform.localScale = new Vector3(
                        bounds.size.x / size.x,
                        bounds.size.y / size.y,
                        1f);
                }
                else
                {
                    _farViewRoot.transform.localScale = Vector3.one;
                }
            }
            else
            {
                _farViewRoot.transform.localScale = Vector3.one;
            }
        }

        private void UpdateFarViewSprite(int width, int height)
        {
            if (_farViewTexture == null || _farViewRenderer == null) return;
            if (_farViewTexture2D == null || _farViewTexture2D.width != width || _farViewTexture2D.height != height)
            {
                if (_farViewTexture2D != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(_farViewTexture2D);
                    else
                        DestroyImmediate(_farViewTexture2D);
                }
                _farViewTexture2D = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            var prev = RenderTexture.active;
            RenderTexture.active = _farViewTexture;
            _farViewTexture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            _farViewTexture2D.Apply();
            RenderTexture.active = prev;

            if (_farViewSprite == null || _farViewSprite.texture != _farViewTexture2D)
            {
                if (_farViewSprite != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(_farViewSprite);
                    else
                        DestroyImmediate(_farViewSprite);
                }
                float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
                _farViewSprite = Sprite.Create(_farViewTexture2D, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
            }
            _farViewRenderer.sprite = _farViewSprite;
            _farViewHasContent = _farViewSprite != null;
        }

        private int ResolveFarViewBakeLayer()
        {
            if (string.IsNullOrEmpty(FarViewBakeLayerName)) return -1;
            int layer = LayerMask.NameToLayer(FarViewBakeLayerName);
            return layer >= 0 ? layer : -1;
        }

        private bool ShouldUseFarView(Camera cam)
        {
            if (cam == null || !cam.orthographic) return false;
            if (FarViewAlwaysActive) return true;
            float threshold = FarViewOrthoThreshold;
            if (UseFarViewThresholdFromCameraZoom)
            {
                var zoom = cam.GetComponent<Game.Presentation.CameraControl.CameraZoom2D>();
                if (zoom != null && zoom.MaxOrthoSize > 0f)
                    threshold = zoom.MaxOrthoSize * Mathf.Clamp(FarViewOrthoThresholdPercent, 0.1f, 1f);
            }
            float hysteresis = Mathf.Max(0f, FarViewOrthoHysteresis);
            if (_farViewActive)
                return cam.orthographicSize >= (threshold - hysteresis);
            return cam.orthographicSize >= threshold;
        }

        private void ApplyFarViewState(bool useBaked)
        {
            bool hasChunks = UseFarViewChunkedBake && HasFarViewChunkSprites();
            _farViewActive = useBaked && _farViewHasContent && !_farViewBaking && (!UseFarViewChunkedBake || hasChunks);
            if (_farViewRenderer != null && !UseFarViewChunkedBake)
                _farViewRenderer.enabled = _farViewActive;
            if (UseFarViewChunkedBake)
                SetFarViewChunkVisibility(_farViewActive);

            if (_farViewActive)
            {
                SetTilemapRendererEnabled(_ground, false);
                SetTilemapRendererEnabled(_props, false);
                SetTilemapRendererEnabled(_blockers, false);
                SetTilemapRendererEnabled(_transitions, false);
                SetTilemapRendererEnabled(_background, false);
            }
            else
            {
                ApplyDebugOutlineVisibility();
            }
        }

        private bool HasFarViewChunkSprites()
        {
            if (_farViewChunks.Count == 0) return false;
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (!chunk.Active || chunk.Renderer == null) continue;
                if (chunk.Renderer.sprite != null)
                    return true;
            }
            return false;
        }

        private bool IsFarViewLoading()
        {
            if (!UseFarViewBake || !FarViewHideMapWhileBaking) return false;
            return _generateRoutine != null || _farViewBaking || _farViewBakeRoutine != null;
        }

        private void ApplyFarViewLoadingVisibility()
        {
            bool loading = IsFarViewLoading();
            bool hideMap = FarViewHideMapWhileBaking && _generateRoutine != null;
            if (loading)
            {
                if (hideMap)
                {
                    _farViewLoadingApplied = true;
                    if (_farViewRenderer != null && !UseFarViewChunkedBake)
                        _farViewRenderer.enabled = false;
                    if (UseFarViewChunkedBake)
                        SetFarViewChunkVisibility(false);

                    SetTilemapRendererEnabled(_ground, false);
                    SetTilemapRendererEnabled(_props, false);
                    SetTilemapRendererEnabled(_blockers, false);
                    SetTilemapRendererEnabled(_transitions, false);
                    SetTilemapRendererEnabled(_background, false);
                }
                return;
            }

            if (_farViewLoadingApplied)
            {
                _farViewLoadingApplied = false;
                var cam = Camera.main;
                if (cam != null && cam.orthographic)
                    ApplyFarViewState(ShouldUseFarView(cam));
                else
                    ApplyFarViewState(false);
                if (!_farViewActive)
                    ForceShowBaseMap();
            }
        }

        private void ForceShowBaseMap()
        {
            SetTilemapRendererEnabled(_ground, true);
            SetTilemapRendererEnabled(_props, true);
            SetTilemapRendererEnabled(_blockers, true);
            SetTilemapRendererEnabled(_transitions, true);
            SetTilemapRendererEnabled(_background, UseBackgroundTilemap);
        }

        private string BuildFarViewHudText()
        {
            string status = _farViewBaking ? "baking" : (_farViewHasContent ? "ready" : "idle");
            float elapsed = _farViewBakeStartTime > 0f ? (Time.realtimeSinceStartup - _farViewBakeStartTime) : 0f;
            float speed = (elapsed > 0f) ? (_farViewChunkCompleted / elapsed) : 0f;
            int remaining = Mathf.Max(0, _farViewChunkTotal - _farViewChunkCompleted);
            float eta = speed > 0f ? (remaining / speed) : 0f;
            int pending = Mathf.Max(0, _farViewChunkSubmitted - _farViewChunkCompleted);

            return $"FarView: {status}\n" +
                         $"Chunks: {_farViewChunkCompleted}/{_farViewChunkTotal} (submitted {_farViewChunkSubmitted}, pending {pending})\n" +
                         $"Readbacks: {_farViewReadbacks.Count} | Errors: {_farViewChunkErrors}\n" +
                         $"Speed: {speed:0.0} chunks/s | ETA: {eta:0.0}s\n" +
                         $"Elapsed: {elapsed:0.0}s";
        }

        private void EnsureFarViewHud()
        {
            if (_farViewHudRoot == null)
            {
                _farViewHudRoot = new GameObject("FarViewBakeHUD (Auto)");
                _farViewHudRoot.transform.SetParent(transform, false);
                var canvas = _farViewHudRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 20000;
                var overlayGo = new GameObject("Overlay");
                overlayGo.transform.SetParent(_farViewHudRoot.transform, false);
                _farViewHudOverlay = overlayGo.AddComponent<Image>();
                _farViewHudOverlay.raycastTarget = false;
                var overlayRt = _farViewHudOverlay.rectTransform;
                overlayRt.anchorMin = Vector2.zero;
                overlayRt.anchorMax = Vector2.one;
                overlayRt.offsetMin = Vector2.zero;
                overlayRt.offsetMax = Vector2.zero;

                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(overlayGo.transform, false);
                _farViewHudOverlayText = titleGo.AddComponent<Text>();
                _farViewHudOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _farViewHudOverlayText.fontSize = 24;
                _farViewHudOverlayText.alignment = TextAnchor.MiddleCenter;
                _farViewHudOverlayText.raycastTarget = false;
                var titleRt = _farViewHudOverlayText.rectTransform;
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.sizeDelta = new Vector2(600f, 60f);
                titleRt.anchoredPosition = new Vector2(0f, 60f);

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(_farViewHudRoot.transform, false);
                _farViewHudText = textGo.AddComponent<Text>();
                _farViewHudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _farViewHudText.fontSize = 14;
                _farViewHudText.alignment = TextAnchor.UpperLeft;
                _farViewHudText.raycastTarget = false;
                var rt = _farViewHudText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(420f, 120f);
            }

            if (_farViewHudOverlay != null)
                _farViewHudOverlay.color = FarViewBakeOverlayColor;
            if (_farViewHudOverlayText != null)
                _farViewHudOverlayText.color = FarViewBakeHUDColor;
            if (_farViewHudText != null)
                _farViewHudText.color = FarViewBakeHUDColor;
            _farViewHudRoot.SetActive(ShowFarViewBakeHUD);
        }

        private void UpdateFarViewHud()
        {
            if (!ShowFarViewBakeHUD)
            {
                if (_farViewHudRoot != null)
                    _farViewHudRoot.SetActive(false);
                return;
            }
            EnsureFarViewHud();
            ApplyFarViewLoadingVisibility();
            bool loading = IsFarViewLoading();
            bool showStats = !FarViewShowHudOnlyWhileBaking || loading;
            if (_farViewHudOverlay != null)
                _farViewHudOverlay.gameObject.SetActive(loading);
            if (_farViewHudOverlayText != null)
            {
                _farViewHudOverlayText.text = FarViewBakeOverlayText;
                _farViewHudOverlayText.gameObject.SetActive(loading);
            }
            if (_farViewHudText != null)
            {
                _farViewHudText.text = BuildFarViewHudText();
                _farViewHudText.gameObject.SetActive(showStats);
                var rt = _farViewHudText.rectTransform;
                if (loading)
                {
                    _farViewHudText.alignment = TextAnchor.MiddleCenter;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(520f, 140f);
                    rt.anchoredPosition = Vector2.zero;
                }
                else
                {
                    _farViewHudText.alignment = TextAnchor.UpperLeft;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(420f, 120f);
                    rt.anchoredPosition = new Vector2(FarViewBakeHUDOffset.x, -FarViewBakeHUDOffset.y);
                }
            }
        }

        private void OnGUI()
        {
            if (!ShowFarViewBakeHUD) return;
            if (FarViewShowHudOnlyWhileBaking && !IsFarViewLoading()) return;
            if (_farViewHudRoot != null) return;
            if (_farViewHudStyle == null)
                _farViewHudStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = FarViewBakeHUDColor }
                };
            GUI.Label(new Rect(FarViewBakeHUDOffset.x, FarViewBakeHUDOffset.y, 420, 100), BuildFarViewHudText(), _farViewHudStyle);
        }

        private static List<Renderer> DisableNonTilemapRenderers(HashSet<Renderer> keep)
        {
            var disabled = new List<Renderer>();
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null || keep.Contains(r) || !r.enabled) continue;
                r.enabled = false;
                disabled.Add(r);
            }
            return disabled;
        }

        private static void RestoreRenderers(List<Renderer> disabled)
        {
            for (int i = 0; i < disabled.Count; i++)
            {
                var r = disabled[i];
                if (r != null)
                    r.enabled = true;
            }
        }

        private readonly struct RendererState
        {
            public readonly Renderer Renderer;
            public readonly bool Enabled;
            public RendererState(Renderer renderer, bool enabled)
            {
                Renderer = renderer;
                Enabled = enabled;
            }
        }

        private sealed class FarViewChunk
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public Texture2D Texture;
            public Sprite Sprite;
            public bool Active;
        }

        private struct FarViewReadback
        {
            public AsyncGPUReadbackRequest Request;
            public RenderTexture RenderTexture;
            public FarViewChunk Chunk;
            public int Width;
            public int Height;
            public float Ppu;
            public Bounds Bounds;
            public int BakeVersion;
            public float SubmittedTime;
        }

        private Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            var child = parent.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name);
            if (child == null)
                go.transform.SetParent(parent, false);
            var tm = go.GetComponent<Tilemap>();
            if (tm == null) tm = go.AddComponent<Tilemap>();
            var tr = go.GetComponent<TilemapRenderer>();
            if (tr == null) tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                tr.sortingLayerName = SortingLayerName;
            return tm;
        }

        private void FillGroundTiles(Tilemap map, int width, int height, System.Random rng, TileBase[] palette)
        {
            int size = width * height;
            var tiles = new TileBase[size];
            for (int row = 0; row < height; row++)
            {
                int rowBase = row * width;
                for (int col = 0; col < width; col++)
                {
                    tiles[rowBase + col] = PickGroundTile(col, row, rng, palette);
                }
            }
            var bounds = new BoundsInt(0, 0, 0, width, height, 1);
            map.SetTilesBlock(bounds, tiles);
        }

        private void FillGroundTilesBlock(Tilemap map, int width, int startRow, int rowCount, System.Random rng, TileBase[] palette)
        {
            int size = width * rowCount;
            var tiles = new TileBase[size];
            for (int r = 0; r < rowCount; r++)
            {
                int rowBase = r * width;
                int row = startRow + r;
                for (int c = 0; c < width; c++)
                {
                    tiles[rowBase + c] = PickGroundTile(c, row, rng, palette);
                }
            }
            var bounds = new BoundsInt(0, startRow, 0, width, rowCount, 1);
            map.SetTilesBlock(bounds, tiles);
        }

        private bool TryConfigureBackground(int hexWidth, int hexHeight, TileBase[] palette, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (_backgroundGrid == null || palette == null || palette.Length == 0 || _hex == null) return false;
            Vector2 cellSize = ResolveBackgroundCellSize(palette);
            if (cellSize.x <= 0f || cellSize.y <= 0f) return false;

            ComputeHexWorldBounds(hexWidth, hexHeight, out var min, out var max);
            float worldW = max.x - min.x;
            float worldH = max.y - min.y;
            width = Mathf.Max(1, Mathf.CeilToInt(worldW / cellSize.x) + 2);
            height = Mathf.Max(1, Mathf.CeilToInt(worldH / cellSize.y) + 2);
            if (BackgroundMaxWidth > 0)
                width = Mathf.Min(width, BackgroundMaxWidth);
            if (BackgroundMaxHeight > 0)
                height = Mathf.Min(height, BackgroundMaxHeight);
            if (BackgroundMaxCells > 0)
            {
                long cells = (long)width * height;
                if (cells > BackgroundMaxCells)
                {
                    float scale = Mathf.Sqrt(cells / (float)BackgroundMaxCells);
                    width = Mathf.Max(1, Mathf.FloorToInt(width / scale));
                    height = Mathf.Max(1, Mathf.FloorToInt(height / scale));
                }
            }

            int usableWidth = Mathf.Max(1, width - 2);
            int usableHeight = Mathf.Max(1, height - 2);
            cellSize = new Vector2(
                Mathf.Max(0.0001f, worldW / usableWidth),
                Mathf.Max(0.0001f, worldH / usableHeight));

            _backgroundGrid.cellLayout = GridLayout.CellLayout.Rectangle;
            _backgroundGrid.cellSize = new Vector3(cellSize.x, cellSize.y, 0f);
            _backgroundGrid.transform.position = new Vector3(min.x + (cellSize.x * 0.5f), min.y + (cellSize.y * 0.5f), 0f);
            return true;
        }

        private Vector2 ResolveBackgroundCellSize(TileBase[] palette)
        {
            if (BackgroundCellSizeOverride != Vector2.zero)
                return BackgroundCellSizeOverride;
            if (palette == null || palette.Length == 0) return Vector2.one;
            var sprite = ExtractTileSprite(palette[0]);
            if (sprite == null) return Vector2.one;
            var size = sprite.bounds.size;
            float s = Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
            if (BackgroundCellOverlapPixels > 0f)
            {
                float ppu = Mathf.Max(0.001f, sprite.pixelsPerUnit);
                float overlapWorld = BackgroundCellOverlapPixels / ppu;
                s = Mathf.Max(0.001f, s - overlapWorld);
            }
            return new Vector2(s, s);
        }

        private void ComputeHexWorldBounds(int width, int height, out Vector2 min, out Vector2 max)
        {
            if (_hex == null || width <= 0 || height <= 0)
            {
                min = Vector2.zero;
                max = Vector2.zero;
                return;
            }
            Vector3 p00 = _hex.GridToWorld(0, 0);
            Vector3 p10 = _hex.GridToWorld(width - 1, 0);
            Vector3 p01 = _hex.GridToWorld(0, height - 1);
            Vector3 p11 = _hex.GridToWorld(width - 1, height - 1);
            float minX = Mathf.Min(Mathf.Min(p00.x, p10.x), Mathf.Min(p01.x, p11.x));
            float maxX = Mathf.Max(Mathf.Max(p00.x, p10.x), Mathf.Max(p01.x, p11.x));
            float minY = Mathf.Min(Mathf.Min(p00.y, p10.y), Mathf.Min(p01.y, p11.y));
            float maxY = Mathf.Max(Mathf.Max(p00.y, p10.y), Mathf.Max(p01.y, p11.y));
            float hexW = Mathf.Sqrt(3f) * _hex.HexSize;
            float hexH = 2f * _hex.HexSize;
            min = new Vector2(minX - (hexW * 0.5f), minY - (hexH * 0.5f));
            max = new Vector2(maxX + (hexW * 0.5f), maxY + (hexH * 0.5f));
        }

        private void GenerateTerrainFromRuleset(int width, int height, bool skipBase, TileBase[] groundPalette)
        {
            if (!TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset))
                return;

            int[] layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
            ApplyRulesetToGround(width, height, skipBase, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
        }

        private IEnumerator GenerateTerrainFromRulesetRoutine(int width, int height, bool skipBase, TileBase[] groundPalette)
        {
            if (!TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset))
                yield break;

            int[] layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
            yield return ApplyRulesetToGroundRoutine(width, height, skipBase, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
        }

        private bool TryPrepareRuleset(
            TileBase[] groundPalette,
            out HexTerrainRuleset ruleset,
            out List<TerrainLayer> layers,
            out Dictionary<int, TileBase>[] edgeLookup,
            out List<HexMaskTile>[] edgeByBits,
            out int tileSeed,
            out Vector2 noiseOffset)
        {
            ruleset = null;
            layers = null;
            edgeLookup = null;
            edgeByBits = null;
            tileSeed = 0;
            noiseOffset = Vector2.zero;

            if (!UseTerrainRuleset)
                return false;

            ruleset = TerrainRuleset != null ? TerrainRuleset : GetAutoTerrainRuleset(groundPalette);
            if (ruleset == null)
                return false;

            if (ruleset.Layers == null || ruleset.Layers.Count == 0)
                return false;

            layers = new List<TerrainLayer>(ruleset.Layers);
            layers.Sort((a, b) => a.MaxHeight.CompareTo(b.MaxHeight));

            var rand = ruleset.UseRandomSeed ? new System.Random() : new System.Random(ruleset.Seed);
            tileSeed = ruleset.UseRandomSeed ? rand.Next() : ruleset.Seed;
            if (ruleset.RandomizeNoiseOffset)
            {
                noiseOffset = new Vector2(
                    (float)rand.NextDouble() * 1000f,
                    (float)rand.NextDouble() * 1000f);
            }
            else
            {
                noiseOffset = ruleset.NoiseOffset;
            }

            edgeLookup = new Dictionary<int, TileBase>[layers.Count];
            edgeByBits = new List<HexMaskTile>[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                BuildEdgeCache(layers[i], out edgeLookup[i], out edgeByBits[i]);
            }

            return true;
        }

        private HexTerrainRuleset GetAutoTerrainRuleset(TileBase[] groundPalette)
        {
            if (!AutoRulesetFromGroundTiles) return null;
            if (groundPalette == null || groundPalette.Length == 0) return null;

            int hash = ComputeAutoTerrainRulesetHash(groundPalette);
            if (_autoTerrainRuleset != null && _autoTerrainRulesetHash == hash)
                return _autoTerrainRuleset;

            ClearAutoTerrainRuleset();

            var ruleset = ScriptableObject.CreateInstance<HexTerrainRuleset>();
            ruleset.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            ruleset.UseRandomSeed = UseRandomSeed;
            ruleset.Seed = Seed;
            ruleset.NoiseScale = AutoTerrainNoiseScale;
            ruleset.Octaves = AutoTerrainOctaves;
            ruleset.Persistence = AutoTerrainPersistence;
            ruleset.Lacunarity = AutoTerrainLacunarity;
            ruleset.NoiseOffset = AutoTerrainNoiseOffset;
            ruleset.RandomizeNoiseOffset = AutoTerrainRandomizeNoiseOffset;
            ruleset.PreferLowerNeighbors = AutoTerrainPreferLowerNeighbors;
            ruleset.TreatOutOfBoundsAsLower = AutoTerrainTreatOutOfBoundsAsLower;

            var palette = groundPalette;
            if (UseWaterBiome)
            {
                palette = ExcludeTilesByNameOrSprite(palette, WaterTileNameKeywords);
                palette = ExcludeTilesByNameOrSprite(palette, RockTileNameKeywords);
                if (palette == null || palette.Length == 0)
                    palette = groundPalette;
            }

            if (AutoTerrainGroupByPrefix)
            {
                var groups = new SortedDictionary<string, List<TileBase>>();
                for (int i = 0; i < palette.Length; i++)
                {
                    var tile = palette[i];
                    if (tile == null) continue;
                    var key = GetAutoTerrainGroupKey(tile);
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = new List<TileBase>();
                        groups[key] = list;
                    }
                    list.Add(tile);
                }

                int layerCount = Mathf.Max(1, groups.Count);
                ruleset.Layers = new List<TerrainLayer>(layerCount);
                int index = 0;
                foreach (var entry in groups)
                {
                    var tiles = entry.Value.ToArray();
                    index++;
                    var layer = new TerrainLayer
                    {
                        Id = $"Auto {entry.Key}",
                        MaxHeight = index / (float)layerCount,
                        BaseTiles = tiles
                    };
                    ruleset.Layers.Add(layer);
                }
            }
            else
            {
                int layerSize = Mathf.Max(1, AutoTerrainLayerSize);
                int layerCount = Mathf.Max(1, Mathf.CeilToInt(palette.Length / (float)layerSize));
                ruleset.Layers = new List<TerrainLayer>(layerCount);
                for (int i = 0; i < layerCount; i++)
                {
                    int start = i * layerSize;
                    int end = Mathf.Min(start + layerSize, palette.Length);
                    int count = Mathf.Max(0, end - start);
                    if (count <= 0) continue;
                    var tiles = new TileBase[count];
                    for (int t = 0; t < count; t++)
                        tiles[t] = palette[start + t];

                    var layer = new TerrainLayer
                    {
                        Id = $"Auto {i + 1}",
                        MaxHeight = (i + 1) / (float)layerCount,
                        BaseTiles = tiles
                    };
                    ruleset.Layers.Add(layer);
                }
            }

            _autoTerrainRuleset = ruleset;
            _autoTerrainRulesetHash = hash;
            return ruleset;
        }

        private int ComputeAutoTerrainRulesetHash(TileBase[] groundPalette)
        {
            unchecked
            {
                int h = 17;
                h = (h * 23) + (AutoRulesetFromGroundTiles ? 1 : 0);
                h = (h * 23) + AutoTerrainLayerSize;
                h = (h * 23) + (AutoTerrainGroupByPrefix ? 1 : 0);
                h = (h * 23) + AutoTerrainNoiseScale.GetHashCode();
                h = (h * 23) + AutoTerrainOctaves;
                h = (h * 23) + AutoTerrainPersistence.GetHashCode();
                h = (h * 23) + AutoTerrainLacunarity.GetHashCode();
                h = (h * 23) + AutoTerrainRandomizeNoiseOffset.GetHashCode();
                h = (h * 23) + AutoTerrainNoiseOffset.GetHashCode();
                h = (h * 23) + AutoTerrainPreferLowerNeighbors.GetHashCode();
                h = (h * 23) + AutoTerrainTreatOutOfBoundsAsLower.GetHashCode();
                h = (h * 23) + (UseWaterBiome ? 1 : 0);
                if (UseWaterBiome)
                {
                    h = (h * 23) + ComputeKeywordHash(WaterTileNameKeywords);
                    h = (h * 23) + ComputeKeywordHash(RockTileNameKeywords);
                }
                h = (h * 23) + UseRandomSeed.GetHashCode();
                h = (h * 23) + Seed;
                h = (h * 23) + groundPalette.Length;
                for (int i = 0; i < groundPalette.Length; i++)
                    h = (h * 23) + (groundPalette[i] == null ? 0 : groundPalette[i].GetInstanceID());
                return h;
            }
        }

        private static int ComputeKeywordHash(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0) return 0;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < keywords.Length; i++)
                {
                    var kw = keywords[i];
                    if (string.IsNullOrEmpty(kw))
                    {
                        h = (h * 23);
                        continue;
                    }
                    for (int c = 0; c < kw.Length; c++)
                        h = (h * 23) + kw[c];
                }
                return h;
            }
        }

        private static string GetAutoTerrainGroupKey(TileBase tile)
        {
            if (tile == null) return "_";
            string name = tile.name;
            if (string.IsNullOrEmpty(name))
            {
                var sprite = ExtractTileSprite(tile);
                if (sprite != null) name = sprite.name;
            }
            if (string.IsNullOrEmpty(name)) return "_";

            int idx = name.IndexOf("Ground ", StringComparison.OrdinalIgnoreCase);
            int start = idx >= 0 ? idx + 7 : 0;
            for (int i = start; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsLetterOrDigit(ch))
                    return char.ToUpperInvariant(ch).ToString();
            }

            return char.ToUpperInvariant(name[0]).ToString();
        }

        private void ClearAutoTerrainRuleset()
        {
            if (_autoTerrainRuleset == null) return;
            if (UnityEngine.Application.isPlaying)
                Destroy(_autoTerrainRuleset);
            else
                DestroyImmediate(_autoTerrainRuleset);
            _autoTerrainRuleset = null;
            _autoTerrainRulesetHash = 0;
        }

        private static TileBase[] CollectUniqueLayerTiles(List<TerrainLayer> layers)
        {
            if (layers == null || layers.Count == 0) return null;
            var unique = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.BaseTiles == null) continue;
                for (int t = 0; t < layer.BaseTiles.Length; t++)
                {
                    var tile = layer.BaseTiles[t];
                    if (tile == null || !seen.Add(tile)) continue;
                    unique.Add(tile);
                }
            }
            return unique.ToArray();
        }

        private TileBase[] ResolveSharedGroundTiles(List<TerrainLayer> layers)
        {
            if (!UseSharedGroundTiles)
                return null;

            var shared = new List<TileBase>();
            var seen = new HashSet<TileBase>();

            if (SharedGroundTiles != null && SharedGroundTiles.Length > 0)
            {
                for (int i = 0; i < SharedGroundTiles.Length; i++)
                {
                    var tile = SharedGroundTiles[i];
                    if (tile != null && seen.Add(tile))
                        shared.Add(tile);
                }
            }

            if (SharedGroundTilesUseNameFilter && SharedGroundTileNameKeywords != null && SharedGroundTileNameKeywords.Length > 0)
            {
                var candidates = CollectUniqueLayerTiles(layers) ?? GroundTiles;
                if (candidates != null)
                {
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var tile = candidates[i];
                        if (tile == null) continue;
                        if (!IsNameMatch(tile.name, SharedGroundTileNameKeywords)) continue;
                        if (seen.Add(tile))
                            shared.Add(tile);
                    }
                }
            }

            return shared.Count > 0 ? shared.ToArray() : null;
        }

        private TileBase[] ResolveBiomeTilesByName(List<TerrainLayer> layers, string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return null;

            var candidates = CollectUniqueLayerTiles(layers) ?? GroundTiles;
            if (candidates == null || candidates.Length == 0) return null;

            var result = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var tile = candidates[i];
                if (tile == null) continue;
                if (!IsNameMatch(tile.name, keywords)) continue;
                if (seen.Add(tile))
                    result.Add(tile);
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        private TileBase[] ResolveBiomeTilesByName(TileBase[] candidates, string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return null;
            if (candidates == null || candidates.Length == 0)
                return null;

            var result = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var tile = candidates[i];
                if (tile == null) continue;
                if (!IsNameMatch(tile.name, keywords)) continue;
                if (seen.Add(tile))
                    result.Add(tile);
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        private TileBase[] ExcludeTilesByName(TileBase[] tiles, string[] excludeKeywords)
        {
            if (tiles == null || tiles.Length == 0)
                return tiles;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return tiles;

            var filtered = new List<TileBase>(tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, excludeKeywords)) continue;
                filtered.Add(tile);
            }

            return filtered.Count > 0 ? filtered.ToArray() : null;
        }

        private TileBase[] ExcludeTilesByNameOrSprite(TileBase[] tiles, string[] excludeKeywords)
        {
            if (tiles == null || tiles.Length == 0)
                return tiles;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return tiles;

            var filtered = new List<TileBase>(tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                bool match = IsNameMatch(tile.name, excludeKeywords);
                if (!match)
                {
                    var sprite = ExtractTileSprite(tile);
                    if (sprite != null)
                        match = IsNameMatch(sprite.name, excludeKeywords);
                }
                if (match) continue;
                filtered.Add(tile);
            }

            return filtered.Count > 0 ? filtered.ToArray() : null;
        }

        private TileVariant[] ExcludeVariantsByNameOrSprite(TileVariant[] variants, string[] excludeKeywords)
        {
            if (variants == null || variants.Length == 0)
                return variants;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return variants;

            var filtered = new List<TileVariant>(variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                var tile = variant.Tile;
                if (tile == null) continue;
                bool match = IsNameMatch(tile.name, excludeKeywords);
                if (!match)
                {
                    var sprite = ExtractTileSprite(tile);
                    if (sprite != null)
                        match = IsNameMatch(sprite.name, excludeKeywords);
                }
                if (match) continue;
                filtered.Add(variant);
            }

            return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<TileVariant>();
        }

        private static bool IsTileExcludedByNameOrSprite(TileBase tile, string[] excludeKeywords)
        {
            if (tile == null || excludeKeywords == null || excludeKeywords.Length == 0)
                return false;
            if (IsNameMatch(tile.name, excludeKeywords))
                return true;
            var sprite = ExtractTileSprite(tile);
            return sprite != null && IsNameMatch(sprite.name, excludeKeywords);
        }

        private string[] ResolveWaterExcludeKeywords()
        {
            if (WaterTileExcludeKeywords != null && WaterTileExcludeKeywords.Length > 0)
                return WaterTileExcludeKeywords;
            return new[] { "Ground A3_", "Ground A11_", "Ground A12_" };
        }

        private static Vector2 RotateVector(Vector2 v, float radians)
        {
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                (v.x * cos) - (v.y * sin),
                (v.x * sin) + (v.y * cos));
        }

        private static void MarkDisk(bool[] mask, int width, int height, int cx, int cy, int radius, ref int count)
        {
            if (mask == null) return;
            int r = Mathf.Max(1, radius);
            int r2 = r * r;
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= height) continue;
                int row = y * width;
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= width) continue;
                    if ((dx * dx) + (dy * dy) > r2) continue;
                    int idx = row + x;
                    if (mask[idx]) continue;
                    mask[idx] = true;
                    count++;
                }
            }
        }

        private bool[] BuildWaterMaskRect(int width, int height, int seed, out int waterCount)
        {
            waterCount = 0;
            if (width <= 0 || height <= 0) return null;

            int total = width * height;
            int target = Mathf.Clamp(Mathf.RoundToInt(total * Mathf.Clamp01(WaterCoverage)), 0, total);
            if (target <= 0) return new bool[total];

            var mask = new bool[total];
            var rng = new System.Random(seed ^ 0x5bd1e995);

            int rivers = Mathf.Max(1, RiverCount);
            int maxSteps = width + height;
            for (int r = 0; r < rivers; r++)
            {
                if (waterCount >= target) break;

                int edge = rng.Next(4);
                Vector2 pos;
                Vector2 dir;
                switch (edge)
                {
                    case 0:
                        pos = new Vector2(0, rng.Next(height));
                        dir = Vector2.right;
                        break;
                    case 1:
                        pos = new Vector2(width - 1, rng.Next(height));
                        dir = Vector2.left;
                        break;
                    case 2:
                        pos = new Vector2(rng.Next(width), 0);
                        dir = Vector2.up;
                        break;
                    default:
                        pos = new Vector2(rng.Next(width), height - 1);
                        dir = Vector2.down;
                        break;
                }

                int minWidth = Mathf.Max(1, Mathf.Min(RiverWidthMin, RiverWidthMax));
                int maxWidth = Mathf.Max(minWidth, Mathf.Max(RiverWidthMin, RiverWidthMax));
                int riverWidth = Mathf.Clamp(rng.Next(minWidth, maxWidth + 1), 1, Mathf.Max(width, height));
                float turnStrength = Mathf.Clamp(RiverTurnStrength, 0.01f, 1f);

                for (int step = 0; step < maxSteps; step++)
                {
                    int cx = Mathf.RoundToInt(pos.x);
                    int cy = Mathf.RoundToInt(pos.y);
                    MarkDisk(mask, width, height, cx, cy, riverWidth / 2, ref waterCount);
                    if (waterCount >= target) break;

                    float turn = ((float)rng.NextDouble() * 2f - 1f) * turnStrength;
                    dir = RotateVector(dir, turn);
                    dir = dir.normalized;
                    pos += dir;
                    if (pos.x < -1 || pos.y < -1 || pos.x > width || pos.y > height)
                        break;
                }
            }

            for (int attempt = 0; attempt < LakeAttempts && waterCount < target; attempt++)
            {
                int startX = rng.Next(width);
                int startY = rng.Next(height);
                int startIdx = (startY * width) + startX;
                if (mask[startIdx]) continue;

                int minLake = Mathf.Max(1, Mathf.Min(LakeMinSize, LakeMaxSize));
                int maxLake = Mathf.Max(minLake, Mathf.Max(LakeMinSize, LakeMaxSize));
                int lakeSize = Mathf.Clamp(rng.Next(minLake, maxLake + 1), 1, total);
                int lakeTarget = Mathf.Min(lakeSize, target - waterCount);
                if (lakeTarget <= 0) break;

                var frontier = new List<Vector2Int>(lakeTarget * 2);
                frontier.Add(new Vector2Int(startX, startY));
                mask[startIdx] = true;
                waterCount++;
                int lakePlaced = 1;

                int cursor = 0;
                while (lakePlaced < lakeTarget && waterCount < target && cursor < frontier.Count)
                {
                    var current = frontier[cursor++];
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                        int ny = current.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int idx = (ny * width) + nx;
                        if (mask[idx]) continue;
                        if (rng.NextDouble() < 0.55)
                        {
                            mask[idx] = true;
                            waterCount++;
                            lakePlaced++;
                            frontier.Add(new Vector2Int(nx, ny));
                            if (lakePlaced >= lakeTarget || waterCount >= target) break;
                        }
                    }
                }
            }

            if (waterCount < target)
            {
                var waterCells = new List<int>(Mathf.Max(16, waterCount));
                for (int i = 0; i < mask.Length; i++)
                {
                    if (mask[i])
                        waterCells.Add(i);
                }
                if (waterCells.Count == 0)
                {
                    int sx = rng.Next(width);
                    int sy = rng.Next(height);
                    int idx = (sy * width) + sx;
                    mask[idx] = true;
                    waterCount++;
                    waterCells.Add(idx);
                }

                int attempts = 0;
                int maxAttempts = total * 4;
                while (waterCount < target && attempts < maxAttempts)
                {
                    int baseIdx = waterCells[rng.Next(waterCells.Count)];
                    int bx = baseIdx % width;
                    int by = baseIdx / width;
                    int dir = rng.Next(4);
                    int nx = bx + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                    int ny = by + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        attempts++;
                        continue;
                    }
                    int nidx = (ny * width) + nx;
                    if (!mask[nidx])
                    {
                        mask[nidx] = true;
                        waterCount++;
                        waterCells.Add(nidx);
                    }
                    attempts++;
                }
            }

            RemoveIsolatedMaskCells(mask, width, height);
            SmoothMask(mask, width, height);
            return mask;
        }

        private bool[] BuildRockMaskFromWater(int width, int height, bool[] waterMask, int seed)
        {
            if (waterMask == null || width <= 0 || height <= 0) return null;
            int size = width * height;
            var rockMask = new bool[size];
            var dist = new int[size];
            for (int i = 0; i < size; i++) dist[i] = -1;

            var queue = new Queue<int>(size);
            for (int i = 0; i < size; i++)
            {
                if (!waterMask[i]) continue;
                dist[i] = 0;
                queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % width;
                int y = idx / width;
                int d = dist[idx];

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int nidx = (ny * width) + nx;
                    if (dist[nidx] >= 0) continue;
                    dist[nidx] = d + 1;
                    queue.Enqueue(nidx);
                }
            }

            int min = Mathf.Max(0, RockMinThickness);
            int max = Mathf.Max(min, RockMaxThickness);
            float scale = Mathf.Max(0.0001f, RockThicknessNoiseScale);
            float ox = (seed % 1000) * 0.01f;
            float oy = (seed % 1000) * 0.02f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width) + x;
                    int d = dist[idx];
                    if (d <= 0) continue;
                    float n = Mathf.PerlinNoise((x + ox) * scale, (y + oy) * scale);
                    float thickness = Mathf.Lerp(min, max, n);
                    if (d <= thickness)
                        rockMask[idx] = true;
                }
            }

            return rockMask;
        }

        private static int[] BuildLandDistanceField(int width, int height, bool[] waterMask, bool[] rockMask, out int maxDistance)
        {
            maxDistance = 0;
            if (width <= 0 || height <= 0)
                return null;

            int size = width * height;
            var dist = new int[size];
            for (int i = 0; i < size; i++) dist[i] = -1;

            var queue = new Queue<int>(size);
            bool hasSeed = false;
            for (int i = 0; i < size; i++)
            {
                bool isWater = waterMask != null && waterMask[i];
                bool isRock = rockMask != null && rockMask[i];
                if (!isWater && !isRock) continue;
                dist[i] = 0;
                queue.Enqueue(i);
                hasSeed = true;
            }

            if (!hasSeed)
            {
                for (int i = 0; i < size; i++) dist[i] = 0;
                return dist;
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % width;
                int y = idx / width;
                int d = dist[idx];
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int nidx = (ny * width) + nx;
                    if (dist[nidx] >= 0) continue;
                    dist[nidx] = d + 1;
                    queue.Enqueue(nidx);
                    bool isLand = (waterMask == null || !waterMask[nidx]) && (rockMask == null || !rockMask[nidx]);
                    if (isLand && dist[nidx] > maxDistance)
                        maxDistance = dist[nidx];
                }
            }

            return dist;
        }

        private Dictionary<TileBase, TileBase> BuildConvertedLookup(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            var converted = ConvertGroundPalette(palette);
            var lookup = new Dictionary<TileBase, TileBase>(palette.Length);
            for (int i = 0; i < palette.Length; i++)
                lookup[palette[i]] = converted[i];
            return lookup;
        }

        private static TileBase[] MapPalette(TileBase[] palette, Dictionary<TileBase, TileBase> lookup)
        {
            if (palette == null || palette.Length == 0) return palette;
            if (lookup == null || lookup.Count == 0) return palette;
            var mapped = new TileBase[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile != null && lookup.TryGetValue(tile, out var converted) && converted != null)
                    mapped[i] = converted;
                else
                    mapped[i] = tile;
            }
            return mapped;
        }

        private TileBase MapBackgroundTile(TileBase tile, Dictionary<TileBase, TileBase> lookup)
        {
            if (tile == null) return null;
            if (lookup != null && lookup.TryGetValue(tile, out var mapped) && mapped != null)
                return mapped;
            if (ConvertGroundTilesRuntime)
                return GetOrCreateRuntimeGroundTile(tile);
            return tile;
        }

        private static HashSet<TileBase> BuildTileSet(TileBase[] tiles)
        {
            if (tiles == null || tiles.Length == 0) return null;
            var set = new HashSet<TileBase>();
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile != null)
                    set.Add(tile);
            }
            return set.Count > 0 ? set : null;
        }

        private static TileBase[] CombineTiles(TileBase[] primary, TileBase[] secondary)
        {
            if ((primary == null || primary.Length == 0) && (secondary == null || secondary.Length == 0))
                return null;
            var list = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            if (primary != null)
            {
                for (int i = 0; i < primary.Length; i++)
                {
                    var tile = primary[i];
                    if (tile != null && seen.Add(tile))
                        list.Add(tile);
                }
            }
            if (secondary != null)
            {
                for (int i = 0; i < secondary.Length; i++)
                {
                    var tile = secondary[i];
                    if (tile != null && seen.Add(tile))
                        list.Add(tile);
                }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private int[] BuildLayerIndex(int width, int height, List<TerrainLayer> layers, Vector2 noiseOffset, HexTerrainRuleset ruleset)
        {
            int size = width * height;
            var layerIndex = new int[size];

            int layerCount = layers?.Count ?? 0;
            float scale = Mathf.Max(0.0001f, ruleset.NoiseScale);
            int octaves = Mathf.Max(1, ruleset.Octaves);
            float persistence = Mathf.Clamp01(ruleset.Persistence);
            float lacunarity = Mathf.Max(0.01f, ruleset.Lacunarity);
            float warpScale = Mathf.Max(0.0001f, DomainWarpScale);
            float warpStrength = Mathf.Max(0f, DomainWarpStrength);
            int warpOctaves = Mathf.Max(1, DomainWarpOctaves);
            float warpPersistence = Mathf.Clamp01(DomainWarpPersistence);
            float warpLacunarity = Mathf.Max(0.01f, DomainWarpLacunarity);
            float macroScale = Mathf.Max(0.0001f, MacroBiomeScale);
            int macroOctaves = Mathf.Max(1, MacroBiomeOctaves);
            float macroPersistence = Mathf.Clamp01(MacroBiomePersistence);
            float macroLacunarity = Mathf.Max(0.01f, MacroBiomeLacunarity);
            float macroBlend = Mathf.Clamp01(MacroBiomeBlend);

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int q = col - (row - (row & 1)) / 2;
                    int r = row;
                    const float sqrt3Over2 = 0.8660254f;
                    float wx = q + (r * 0.5f);
                    float wy = r * sqrt3Over2;
                    if (UseNoiseDomainWarp)
                    {
                        float warpX = FractalNoise((wx + noiseOffset.x + 127.1f) * warpScale, (wy + noiseOffset.y + 311.7f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        float warpY = FractalNoise((wx + noiseOffset.x + 269.5f) * warpScale, (wy + noiseOffset.y + 183.3f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        warpX = (warpX - 0.5f) * 2f;
                        warpY = (warpY - 0.5f) * 2f;
                        wx += warpX * warpStrength;
                        wy += warpY * warpStrength;
                    }

                    float nx = (wx + noiseOffset.x) * scale;
                    float ny = (wy + noiseOffset.y) * scale;
                    float h = FractalNoise(nx, ny, octaves, persistence, lacunarity);
                    if (UseMacroBiomeNoise)
                    {
                        float mx = (wx + noiseOffset.x) * macroScale;
                        float my = (wy + noiseOffset.y) * macroScale;
                        float macro = FractalNoise(mx, my, macroOctaves, macroPersistence, macroLacunarity);
                        macro = AdjustContrast(macro, MacroBiomeContrast);
                        h = Mathf.Lerp(h, macro, macroBlend);
                    }
                    h = ApplyLayerQuantization(h, col, row, layerCount, ruleset.Seed);
                    int idx = (row * width) + col;
                    layerIndex[idx] = FindLayerIndex(h, layers);
                }
            }

            var smoothed = ApplyLayerSmoothingHex(layerIndex, width, height, layers);
            return ApplyLayerRegionCleanupHex(smoothed, width, height, layers);
        }

        private int[] BuildLayerIndexRect(int width, int height, List<TerrainLayer> layers, Vector2 noiseOffset, Vector2 cellSize, HexTerrainRuleset ruleset)
        {
            int size = width * height;
            var layerIndex = new int[size];

            int layerCount = layers?.Count ?? 0;
            float scale = Mathf.Max(0.0001f, ruleset.NoiseScale);
            int octaves = Mathf.Max(1, ruleset.Octaves);
            float persistence = Mathf.Clamp01(ruleset.Persistence);
            float lacunarity = Mathf.Max(0.01f, ruleset.Lacunarity);
            float stepX = Mathf.Max(0.0001f, cellSize.x);
            float stepY = Mathf.Max(0.0001f, cellSize.y);
            float warpScale = Mathf.Max(0.0001f, DomainWarpScale);
            float warpStrength = Mathf.Max(0f, DomainWarpStrength);
            int warpOctaves = Mathf.Max(1, DomainWarpOctaves);
            float warpPersistence = Mathf.Clamp01(DomainWarpPersistence);
            float warpLacunarity = Mathf.Max(0.01f, DomainWarpLacunarity);
            float macroScale = Mathf.Max(0.0001f, MacroBiomeScale);
            int macroOctaves = Mathf.Max(1, MacroBiomeOctaves);
            float macroPersistence = Mathf.Clamp01(MacroBiomePersistence);
            float macroLacunarity = Mathf.Max(0.01f, MacroBiomeLacunarity);
            float macroBlend = Mathf.Clamp01(MacroBiomeBlend);

            for (int row = 0; row < height; row++)
            {
                float wy = row * stepY;
                for (int col = 0; col < width; col++)
                {
                    float wx = col * stepX;
                    if (UseNoiseDomainWarp)
                    {
                        float warpX = FractalNoise((wx + noiseOffset.x + 127.1f) * warpScale, (wy + noiseOffset.y + 311.7f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        float warpY = FractalNoise((wx + noiseOffset.x + 269.5f) * warpScale, (wy + noiseOffset.y + 183.3f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        warpX = (warpX - 0.5f) * 2f;
                        warpY = (warpY - 0.5f) * 2f;
                        wx += warpX * warpStrength;
                        wy += warpY * warpStrength;
                    }
                    float nx = (wx + noiseOffset.x) * scale;
                    float ny = (wy + noiseOffset.y) * scale;
                    float h = FractalNoise(nx, ny, octaves, persistence, lacunarity);
                    if (UseMacroBiomeNoise)
                    {
                        float mx = (wx + noiseOffset.x) * macroScale;
                        float my = (wy + noiseOffset.y) * macroScale;
                        float macro = FractalNoise(mx, my, macroOctaves, macroPersistence, macroLacunarity);
                        macro = AdjustContrast(macro, MacroBiomeContrast);
                        h = Mathf.Lerp(h, macro, macroBlend);
                    }
                    h = ApplyLayerQuantization(h, col, row, layerCount, ruleset.Seed);
                    int idx = (row * width) + col;
                    layerIndex[idx] = FindLayerIndex(h, layers);
                }
            }

            var smoothed = ApplyLayerSmoothingRect(layerIndex, width, height, layers);
            return ApplyLayerRegionCleanupRect(smoothed, width, height, layers);
        }

        private int[] ApplyLayerSmoothingHex(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerSmoothing || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerSmoothingPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;

            int[] src = layerIndex;
            int[] dst = new int[src.Length];
            int[] counts = new int[layerCount];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    var offsets = (row & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        Array.Clear(counts, 0, layerCount);
                        int idx = rowBase + col;
                        int current = src[idx];
                        counts[current]++;
                        int neighborCount = 1;

                        for (int i = 0; i < 6; i++)
                        {
                            int nc = col + offsets[i].x;
                            int nr = row + offsets[i].y;
                            if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                            int nIdx = (nr * width) + nc;
                            counts[src[nIdx]]++;
                            neighborCount++;
                        }

                        int bestLayer = current;
                        int bestCount = counts[current];
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (counts[i] > bestCount)
                            {
                                bestLayer = i;
                                bestCount = counts[i];
                            }
                        }

                        int required = Mathf.Max(2, Mathf.CeilToInt(neighborCount * Mathf.Clamp01(LayerSmoothingMajority)));
                        dst[idx] = (bestLayer != current && bestCount >= required) ? bestLayer : current;
                    }
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, layerIndex))
                Array.Copy(src, layerIndex, layerIndex.Length);
            return layerIndex;
        }

        private int[] ApplyLayerSmoothingRect(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerSmoothing || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerSmoothingPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;

            var offsets = LayerSmoothingIncludeDiagonals ? RectNeighborOffsets8 : RectNeighborOffsets4;
            int[] src = layerIndex;
            int[] dst = new int[src.Length];
            int[] counts = new int[layerCount];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        Array.Clear(counts, 0, layerCount);
                        int idx = rowBase + col;
                        int current = src[idx];
                        counts[current]++;
                        int neighborCount = 1;

                        for (int i = 0; i < offsets.Length; i++)
                        {
                            int nc = col + offsets[i].x;
                            int nr = row + offsets[i].y;
                            if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                            int nIdx = (nr * width) + nc;
                            counts[src[nIdx]]++;
                            neighborCount++;
                        }

                        int bestLayer = current;
                        int bestCount = counts[current];
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (counts[i] > bestCount)
                            {
                                bestLayer = i;
                                bestCount = counts[i];
                            }
                        }

                        int required = Mathf.Max(2, Mathf.CeilToInt(neighborCount * Mathf.Clamp01(LayerSmoothingMajority)));
                        dst[idx] = (bestLayer != current && bestCount >= required) ? bestLayer : current;
                    }
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, layerIndex))
                Array.Copy(src, layerIndex, layerIndex.Length);
            return layerIndex;
        }

        private int[] ApplyLayerRegionCleanupHex(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerRegionCleanup || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerCleanupPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;
            int size = width * height;
            int[] queue = new int[size];
            int[] counts = new int[layerCount];

            for (int pass = 0; pass < passes; pass++)
            {
                var visited = new bool[size];
                for (int row = 0; row < height; row++)
                {
                    var offsets = (row & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        int idx = rowBase + col;
                        if (visited[idx]) continue;
                        int layer = layerIndex[idx];
                        int head = 0;
                        int tail = 0;
                        queue[tail++] = idx;
                        visited[idx] = true;
                        int regionCount = 0;
                        Array.Clear(counts, 0, layerCount);

                        while (head < tail)
                        {
                            int current = queue[head++];
                            regionCount++;
                            int c = current % width;
                            int r = current / width;
                            var localOffsets = (r & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
                            for (int i = 0; i < 6; i++)
                            {
                                int nc = c + localOffsets[i].x;
                                int nr = r + localOffsets[i].y;
                                if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                                int nIdx = (nr * width) + nc;
                                int nLayer = layerIndex[nIdx];
                                if (nLayer == layer)
                                {
                                    if (!visited[nIdx])
                                    {
                                        visited[nIdx] = true;
                                        queue[tail++] = nIdx;
                                    }
                                }
                                else
                                {
                                    if (nLayer >= 0 && nLayer < layerCount)
                                        counts[nLayer]++;
                                }
                            }
                        }

                        if (regionCount < LayerMinRegionSize)
                        {
                            int bestLayer = layer;
                            int bestCount = -1;
                            for (int i = 0; i < layerCount; i++)
                            {
                                if (counts[i] > bestCount)
                                {
                                    bestLayer = i;
                                    bestCount = counts[i];
                                }
                            }
                            if (bestLayer != layer && bestCount > 0)
                            {
                                for (int i = 0; i < tail; i++)
                                    layerIndex[queue[i]] = bestLayer;
                            }
                        }
                    }
                }
            }

            return layerIndex;
        }

        private int[] ApplyLayerRegionCleanupRect(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerRegionCleanup || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerCleanupPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;
            int size = width * height;
            int[] queue = new int[size];
            int[] counts = new int[layerCount];
            var offsets = LayerCleanupIncludeDiagonals ? RectNeighborOffsets8 : RectNeighborOffsets4;

            for (int pass = 0; pass < passes; pass++)
            {
                var visited = new bool[size];
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        int idx = rowBase + col;
                        if (visited[idx]) continue;
                        int layer = layerIndex[idx];
                        int head = 0;
                        int tail = 0;
                        queue[tail++] = idx;
                        visited[idx] = true;
                        int regionCount = 0;
                        Array.Clear(counts, 0, layerCount);

                        while (head < tail)
                        {
                            int current = queue[head++];
                            regionCount++;
                            int c = current % width;
                            int r = current / width;
                            for (int i = 0; i < offsets.Length; i++)
                            {
                                int nc = c + offsets[i].x;
                                int nr = r + offsets[i].y;
                                if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                                int nIdx = (nr * width) + nc;
                                int nLayer = layerIndex[nIdx];
                                if (nLayer == layer)
                                {
                                    if (!visited[nIdx])
                                    {
                                        visited[nIdx] = true;
                                        queue[tail++] = nIdx;
                                    }
                                }
                                else
                                {
                                    if (nLayer >= 0 && nLayer < layerCount)
                                        counts[nLayer]++;
                                }
                            }
                        }

                        if (regionCount < LayerMinRegionSize)
                        {
                            int bestLayer = layer;
                            int bestCount = -1;
                            for (int i = 0; i < layerCount; i++)
                            {
                                if (counts[i] > bestCount)
                                {
                                    bestLayer = i;
                                    bestCount = counts[i];
                                }
                            }
                            if (bestLayer != layer && bestCount > 0)
                            {
                                for (int i = 0; i < tail; i++)
                                    layerIndex[queue[i]] = bestLayer;
                            }
                        }
                    }
                }
            }

            return layerIndex;
        }

        private static float FractalNoise(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float sx = x * frequency;
                float sy = y * frequency;
                float perlin = Mathf.PerlinNoise(sx, sy) * 2f - 1f;
                noiseHeight += perlin * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            if (maxValue <= 0f) return 0f;
            float normalized = (noiseHeight / maxValue + 1f) * 0.5f;
            return Mathf.Clamp01(normalized);
        }

        private static float AdjustContrast(float value, float contrast)
        {
            if (contrast <= 0f) return Mathf.Clamp01(value);
            return Mathf.Clamp01(0.5f + (value - 0.5f) * contrast);
        }

        private float ApplyLayerQuantization(float value, int col, int row, int layerCount, int seed)
        {
            if (!UseLayerQuantization || layerCount <= 1) return Mathf.Clamp01(value);
            float step = 1f / layerCount;
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            float jitter = (LayerQuantizationJitter > 0f)
                ? ((hash % 1000) / 999f - 0.5f) * 2f * LayerQuantizationJitter
                : 0f;
            float quantized = Mathf.Round(value / step) * step + jitter * step;
            return Mathf.Clamp01(quantized);
        }

        private static int FindLayerIndex(float heightValue, List<TerrainLayer> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (heightValue <= layers[i].MaxHeight)
                    return i;
            }
            return layers.Count - 1;
        }

        private static void BuildEdgeCache(
            TerrainLayer layer,
            out Dictionary<int, TileBase> edgeLookup,
            out List<HexMaskTile> edgeByBits)
        {
            edgeLookup = null;
            edgeByBits = null;
            if (layer == null || layer.EdgeTiles == null || layer.EdgeTiles.Count == 0)
                return;

            edgeLookup = new Dictionary<int, TileBase>();
            edgeByBits = new List<HexMaskTile>(layer.EdgeTiles.Count);
            for (int i = 0; i < layer.EdgeTiles.Count; i++)
            {
                var entry = layer.EdgeTiles[i];
                if (entry == null || entry.Tile == null) continue;
                int mask = Mathf.Clamp(entry.Mask, 0, 63);
                if (!edgeLookup.ContainsKey(mask))
                    edgeLookup.Add(mask, entry.Tile);
                edgeByBits.Add(entry);
            }
            edgeByBits.Sort((a, b) => CountBits(b.Mask).CompareTo(CountBits(a.Mask)));
        }

        private static TileBase PickEdgeTile(
            int mask,
            TerrainLayer layer,
            Dictionary<int, TileBase> edgeLookup,
            List<HexMaskTile> edgeByBits)
        {
            if (edgeLookup != null && edgeLookup.TryGetValue(mask, out var exact) && exact != null)
                return exact;

            if (edgeByBits != null)
            {
                for (int i = 0; i < edgeByBits.Count; i++)
                {
                    var entry = edgeByBits[i];
                    if (entry == null || entry.Tile == null) continue;
                    if ((mask & entry.Mask) == entry.Mask)
                        return entry.Tile;
                }
            }

            return layer != null ? layer.DefaultEdgeTile : null;
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                if ((mask & (1 << i)) != 0)
                    count++;
            }
            return count;
        }

        private static TileBase PickTileDeterministic(TileBase[] palette, int col, int row, int seed)
        {
            if (palette == null || palette.Length == 0) return null;
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            return palette[hash % palette.Length];
        }

        private static TileBase PickTileDeterministicExcluding(TileBase[] palette, int col, int row, int seed, string[] excludeKeywords)
        {
            if (palette == null || palette.Length == 0) return null;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return PickTileDeterministic(palette, col, row, seed);
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            int start = hash % palette.Length;
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[(start + i) % palette.Length];
                if (!IsTileExcludedByNameOrSprite(tile, excludeKeywords))
                    return tile;
            }
            return palette[start];
        }

        // Mask bit order: 0=E, 1=NE, 2=NW, 3=W, 4=SW, 5=SE (odd-r layout).
        private static readonly Vector2Int[] EvenRowNeighborOffsets =
        {
            new Vector2Int(1, 0),   // E
            new Vector2Int(0, -1),  // NE
            new Vector2Int(-1, -1), // NW
            new Vector2Int(-1, 0),  // W
            new Vector2Int(-1, 1),  // SW
            new Vector2Int(0, 1),   // SE
        };

        private static readonly Vector2Int[] OddRowNeighborOffsets =
        {
            new Vector2Int(1, 0),   // E
            new Vector2Int(1, -1),  // NE
            new Vector2Int(0, -1),  // NW
            new Vector2Int(-1, 0),  // W
            new Vector2Int(0, 1),   // SW
            new Vector2Int(1, 1),   // SE
        };

        private static readonly Vector2Int[] RectNeighborOffsets4 =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private static readonly Vector2Int[] RectNeighborOffsets8 =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
        };

        private static int BuildNeighborMask(
            int col,
            int row,
            int width,
            int height,
            int layerIdx,
            int[] layerIndex,
            HexTerrainRuleset ruleset)
        {
            var offsets = (row & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
            int mask = 0;
            for (int i = 0; i < 6; i++)
            {
                int nc = col + offsets[i].x;
                int nr = row + offsets[i].y;
                if (nc < 0 || nr < 0 || nc >= width || nr >= height)
                {
                    if (ruleset.TreatOutOfBoundsAsLower)
                        mask |= (1 << i);
                    continue;
                }

                int nIdx = (nr * width) + nc;
                int neighborLayer = layerIndex[nIdx];
                bool diff = ruleset.PreferLowerNeighbors ? neighborLayer < layerIdx : neighborLayer != layerIdx;
                if (diff)
                    mask |= (1 << i);
            }
            return mask;
        }

        private struct EdgeProfile
        {
            public bool TopGreen;
            public bool RightGreen;
            public bool BottomGreen;
            public bool LeftGreen;
            public bool TopWater;
            public bool RightWater;
            public bool BottomWater;
            public bool LeftWater;
            public float TopWaterRatio;
            public float RightWaterRatio;
            public float BottomWaterRatio;
            public float LeftWaterRatio;
            public byte TopWaterMask;
            public byte RightWaterMask;
            public byte BottomWaterMask;
            public byte LeftWaterMask;
            public byte WaterMaskSamples;
            public byte TopWaterTransitions;
            public byte RightWaterTransitions;
            public byte BottomWaterTransitions;
            public byte LeftWaterTransitions;
        }

        private struct TileVariant
        {
            public TileBase Tile;
            public Matrix4x4 Transform;
            public EdgeProfile Profile;
            public int Id;
        }

        [Serializable]
        public class GroundTileOverride
        {
            public string NameContains;
            public float ExtraInsetPixels = 0f;
            public int ExtraEdgeTrimPixels = 0;
            public bool OverrideEdgeBlackThreshold = false;
            public float EdgeBlackThresholdOverride = 0f;
            public bool OverrideEdgeChromaThreshold = false;
            public float EdgeChromaThresholdOverride = 0f;
            public float ExtraTopInsetPixels = 0f;
            public float ExtraRightInsetPixels = 0f;
            public float ExtraBottomInsetPixels = 0f;
            public float ExtraLeftInsetPixels = 0f;
        }

        private readonly struct MatrixKey : IEquatable<MatrixKey>
        {
            public readonly int M00;
            public readonly int M01;
            public readonly int M10;
            public readonly int M11;

            public MatrixKey(int m00, int m01, int m10, int m11)
            {
                M00 = m00;
                M01 = m01;
                M10 = m10;
                M11 = m11;
            }

            public static MatrixKey From(Matrix4x4 m)
            {
                return new MatrixKey(
                    Mathf.RoundToInt(m.m00 * 1000f),
                    Mathf.RoundToInt(m.m01 * 1000f),
                    Mathf.RoundToInt(m.m10 * 1000f),
                    Mathf.RoundToInt(m.m11 * 1000f));
            }

            public bool Equals(MatrixKey other)
            {
                return M00 == other.M00 && M01 == other.M01 && M10 == other.M10 && M11 == other.M11;
            }

            public override bool Equals(object obj)
            {
                return obj is MatrixKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = (h * 23) + M00;
                    h = (h * 23) + M01;
                    h = (h * 23) + M10;
                    h = (h * 23) + M11;
                    return h;
                }
            }
        }

        private EdgeProfile GetEdgeProfile(TileBase tile)
        {
            if (tile == null) return default;
            var sprite = ExtractTileSprite(tile);
            if (sprite == null) return default;
            if (_edgeProfileCache.TryGetValue(sprite, out var cached))
                return cached;

            var profile = BuildEdgeProfile(sprite);
            _edgeProfileCache[sprite] = profile;
            return profile;
        }

        private EdgeProfile BuildEdgeProfile(Sprite sprite)
        {
            var profile = new EdgeProfile();
            if (sprite == null) return profile;
            var texture = sprite.texture;
            if (texture == null) return profile;
            var readable = GetReadableTexture(texture);
            if (readable == null) return profile;

            var rect = sprite.textureRect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return profile;

            var pixels = readable.GetPixels(x0, y0, width, height);
            if (pixels == null || pixels.Length == 0) return profile;

            int inset = Mathf.Max(0, GroundTileEdgeSampleInsetPixels);
            int minX = Mathf.Clamp(inset, 0, width - 1);
            int maxX = Mathf.Clamp(width - 1 - inset, 0, width - 1);
            int minY = Mathf.Clamp(inset, 0, height - 1);
            int maxY = Mathf.Clamp(height - 1 - inset, 0, height - 1);

            profile.BottomGreen = IsEdgeGreen(pixels, width, minY, minX, maxX, true);
            profile.TopGreen = IsEdgeGreen(pixels, width, maxY, minX, maxX, true);
            profile.LeftGreen = IsEdgeGreen(pixels, width, minX, minY, maxY, false);
            profile.RightGreen = IsEdgeGreen(pixels, width, maxX, minY, maxY, false);

            int waterInset = Mathf.Max(0, WaterEdgeSampleInsetPixels);
            int waterMinX = Mathf.Clamp(waterInset, 0, width - 1);
            int waterMaxX = Mathf.Clamp(width - 1 - waterInset, 0, width - 1);
            int waterMinY = Mathf.Clamp(waterInset, 0, height - 1);
            int waterMaxY = Mathf.Clamp(height - 1 - waterInset, 0, height - 1);
            if (waterMaxX >= waterMinX && waterMaxY >= waterMinY)
            {
                int band = Mathf.Max(1, WaterEdgeSampleBandPixels);
                int bottomMaxY = Mathf.Min(waterMinY + band - 1, waterMaxY);
                int topMinY = Mathf.Max(waterMaxY - band + 1, waterMinY);
                int leftMaxX = Mathf.Min(waterMinX + band - 1, waterMaxX);
                int rightMinX = Mathf.Max(waterMaxX - band + 1, waterMinX);

                profile.BottomWaterRatio = GetEdgeWaterRatioBand(pixels, width, waterMinX, waterMaxX, waterMinY, bottomMaxY);
                profile.TopWaterRatio = GetEdgeWaterRatioBand(pixels, width, waterMinX, waterMaxX, topMinY, waterMaxY);
                profile.LeftWaterRatio = GetEdgeWaterRatioBand(pixels, width, waterMinX, leftMaxX, waterMinY, waterMaxY);
                profile.RightWaterRatio = GetEdgeWaterRatioBand(pixels, width, rightMinX, waterMaxX, waterMinY, waterMaxY);

                profile.BottomWater = profile.BottomWaterRatio >= WaterEdgeBlueRatio;
                profile.TopWater = profile.TopWaterRatio >= WaterEdgeBlueRatio;
                profile.LeftWater = profile.LeftWaterRatio >= WaterEdgeBlueRatio;
                profile.RightWater = profile.RightWaterRatio >= WaterEdgeBlueRatio;

                int samples = Mathf.Clamp(WaterEdgeMaskSamples, 1, 8);
                profile.WaterMaskSamples = (byte)samples;
                profile.BottomWaterMask = BuildWaterEdgeMask(pixels, width, waterMinX, waterMaxX, waterMinY, bottomMaxY, samples, true);
                profile.TopWaterMask = BuildWaterEdgeMask(pixels, width, waterMinX, waterMaxX, topMinY, waterMaxY, samples, true);
                profile.LeftWaterMask = BuildWaterEdgeMask(pixels, width, waterMinX, leftMaxX, waterMinY, waterMaxY, samples, false);
                profile.RightWaterMask = BuildWaterEdgeMask(pixels, width, rightMinX, waterMaxX, waterMinY, waterMaxY, samples, false);
                profile.BottomWaterTransitions = CountMaskTransitions(profile.BottomWaterMask, samples);
                profile.TopWaterTransitions = CountMaskTransitions(profile.TopWaterMask, samples);
                profile.LeftWaterTransitions = CountMaskTransitions(profile.LeftWaterMask, samples);
                profile.RightWaterTransitions = CountMaskTransitions(profile.RightWaterMask, samples);
            }

            return profile;
        }

        private static EdgeProfile RotateProfileCCW(EdgeProfile profile, int steps)
        {
            int s = ((steps % 4) + 4) % 4;
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            if (s == 0) return profile;
            if (s == 1)
            {
                return new EdgeProfile
                {
                    TopGreen = profile.RightGreen,
                    RightGreen = profile.BottomGreen,
                    BottomGreen = profile.LeftGreen,
                    LeftGreen = profile.TopGreen,
                    TopWater = profile.RightWater,
                    RightWater = profile.BottomWater,
                    BottomWater = profile.LeftWater,
                    LeftWater = profile.TopWater,
                    TopWaterRatio = profile.RightWaterRatio,
                    RightWaterRatio = profile.BottomWaterRatio,
                    BottomWaterRatio = profile.LeftWaterRatio,
                    LeftWaterRatio = profile.TopWaterRatio,
                    TopWaterMask = ReverseMaskBits(profile.RightWaterMask, samples),
                    RightWaterMask = profile.BottomWaterMask,
                    BottomWaterMask = ReverseMaskBits(profile.LeftWaterMask, samples),
                    LeftWaterMask = profile.TopWaterMask,
                    WaterMaskSamples = profile.WaterMaskSamples
                    ,TopWaterTransitions = profile.RightWaterTransitions
                    ,RightWaterTransitions = profile.BottomWaterTransitions
                    ,BottomWaterTransitions = profile.LeftWaterTransitions
                    ,LeftWaterTransitions = profile.TopWaterTransitions
                };
            }
            if (s == 2)
            {
                return new EdgeProfile
                {
                    TopGreen = profile.BottomGreen,
                    RightGreen = profile.LeftGreen,
                    BottomGreen = profile.TopGreen,
                    LeftGreen = profile.RightGreen,
                    TopWater = profile.BottomWater,
                    RightWater = profile.LeftWater,
                    BottomWater = profile.TopWater,
                    LeftWater = profile.RightWater,
                    TopWaterRatio = profile.BottomWaterRatio,
                    RightWaterRatio = profile.LeftWaterRatio,
                    BottomWaterRatio = profile.TopWaterRatio,
                    LeftWaterRatio = profile.RightWaterRatio,
                    TopWaterMask = ReverseMaskBits(profile.BottomWaterMask, samples),
                    RightWaterMask = ReverseMaskBits(profile.LeftWaterMask, samples),
                    BottomWaterMask = ReverseMaskBits(profile.TopWaterMask, samples),
                    LeftWaterMask = ReverseMaskBits(profile.RightWaterMask, samples),
                    WaterMaskSamples = profile.WaterMaskSamples
                    ,TopWaterTransitions = profile.BottomWaterTransitions
                    ,RightWaterTransitions = profile.LeftWaterTransitions
                    ,BottomWaterTransitions = profile.TopWaterTransitions
                    ,LeftWaterTransitions = profile.RightWaterTransitions
                };
            }
            return new EdgeProfile
            {
                TopGreen = profile.LeftGreen,
                RightGreen = profile.TopGreen,
                BottomGreen = profile.RightGreen,
                LeftGreen = profile.BottomGreen,
                TopWater = profile.LeftWater,
                RightWater = profile.TopWater,
                BottomWater = profile.RightWater,
                LeftWater = profile.BottomWater,
                TopWaterRatio = profile.LeftWaterRatio,
                RightWaterRatio = profile.TopWaterRatio,
                BottomWaterRatio = profile.RightWaterRatio,
                LeftWaterRatio = profile.BottomWaterRatio,
                TopWaterMask = profile.LeftWaterMask,
                RightWaterMask = ReverseMaskBits(profile.TopWaterMask, samples),
                BottomWaterMask = profile.RightWaterMask,
                LeftWaterMask = ReverseMaskBits(profile.BottomWaterMask, samples),
                WaterMaskSamples = profile.WaterMaskSamples
                ,TopWaterTransitions = profile.LeftWaterTransitions
                ,RightWaterTransitions = profile.TopWaterTransitions
                ,BottomWaterTransitions = profile.RightWaterTransitions
                ,LeftWaterTransitions = profile.BottomWaterTransitions
            };
        }

        private static EdgeProfile MirrorProfileX(EdgeProfile profile)
        {
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            return new EdgeProfile
            {
                TopGreen = profile.TopGreen,
                RightGreen = profile.LeftGreen,
                BottomGreen = profile.BottomGreen,
                LeftGreen = profile.RightGreen,
                TopWater = profile.TopWater,
                RightWater = profile.LeftWater,
                BottomWater = profile.BottomWater,
                LeftWater = profile.RightWater,
                TopWaterRatio = profile.TopWaterRatio,
                RightWaterRatio = profile.LeftWaterRatio,
                BottomWaterRatio = profile.BottomWaterRatio,
                LeftWaterRatio = profile.RightWaterRatio,
                TopWaterMask = ReverseMaskBits(profile.TopWaterMask, samples),
                RightWaterMask = profile.LeftWaterMask,
                BottomWaterMask = ReverseMaskBits(profile.BottomWaterMask, samples),
                LeftWaterMask = profile.RightWaterMask,
                WaterMaskSamples = profile.WaterMaskSamples
                ,TopWaterTransitions = profile.TopWaterTransitions
                ,RightWaterTransitions = profile.LeftWaterTransitions
                ,BottomWaterTransitions = profile.BottomWaterTransitions
                ,LeftWaterTransitions = profile.RightWaterTransitions
            };
        }

        private static EdgeProfile MirrorProfileY(EdgeProfile profile)
        {
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            return new EdgeProfile
            {
                TopGreen = profile.BottomGreen,
                RightGreen = profile.RightGreen,
                BottomGreen = profile.TopGreen,
                LeftGreen = profile.LeftGreen,
                TopWater = profile.BottomWater,
                RightWater = profile.RightWater,
                BottomWater = profile.TopWater,
                LeftWater = profile.LeftWater,
                TopWaterRatio = profile.BottomWaterRatio,
                RightWaterRatio = profile.RightWaterRatio,
                BottomWaterRatio = profile.TopWaterRatio,
                LeftWaterRatio = profile.LeftWaterRatio,
                TopWaterMask = profile.BottomWaterMask,
                RightWaterMask = ReverseMaskBits(profile.RightWaterMask, samples),
                BottomWaterMask = profile.TopWaterMask,
                LeftWaterMask = ReverseMaskBits(profile.LeftWaterMask, samples),
                WaterMaskSamples = profile.WaterMaskSamples
                ,TopWaterTransitions = profile.BottomWaterTransitions
                ,RightWaterTransitions = profile.RightWaterTransitions
                ,BottomWaterTransitions = profile.TopWaterTransitions
                ,LeftWaterTransitions = profile.LeftWaterTransitions
            };
        }

        private void ApplyGroundTileOverrides(
            string spriteName,
            ref float insetPixels,
            ref int edgeTrimPixels,
            ref float edgeBlackThreshold,
            ref float edgeChromaThreshold,
            ref float extraTopInsetPixels,
            ref float extraRightInsetPixels,
            ref float extraBottomInsetPixels,
            ref float extraLeftInsetPixels)
        {
            if (GroundTileOverrides == null || GroundTileOverrides.Length == 0 || string.IsNullOrEmpty(spriteName))
                return;

            for (int i = 0; i < GroundTileOverrides.Length; i++)
            {
                var ov = GroundTileOverrides[i];
                if (ov == null || string.IsNullOrEmpty(ov.NameContains)) continue;
                if (spriteName.IndexOf(ov.NameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                insetPixels = Mathf.Max(0f, insetPixels + ov.ExtraInsetPixels);
                edgeTrimPixels = Mathf.Max(0, edgeTrimPixels + ov.ExtraEdgeTrimPixels);
                if (ov.OverrideEdgeBlackThreshold)
                    edgeBlackThreshold = Mathf.Clamp01(ov.EdgeBlackThresholdOverride);
                if (ov.OverrideEdgeChromaThreshold)
                    edgeChromaThreshold = ov.EdgeChromaThresholdOverride;
                extraTopInsetPixels = Mathf.Max(0f, extraTopInsetPixels + ov.ExtraTopInsetPixels);
                extraRightInsetPixels = Mathf.Max(0f, extraRightInsetPixels + ov.ExtraRightInsetPixels);
                extraBottomInsetPixels = Mathf.Max(0f, extraBottomInsetPixels + ov.ExtraBottomInsetPixels);
                extraLeftInsetPixels = Mathf.Max(0f, extraLeftInsetPixels + ov.ExtraLeftInsetPixels);
            }
        }

        private static bool ShouldUseSharedTiles(int col, int row, int layerIdx, int seed, float chance)
        {
            if (chance <= 0f) return false;
            unchecked
            {
                uint hash = (uint)(col * 73856093) ^ (uint)(row * 19349663) ^ (uint)(layerIdx * 83492791) ^ (uint)seed;
                float v = (hash & 0xFFFFFF) / 16777215f;
                return v < chance;
            }
        }

        private int[] GetRotationSteps()
        {
            if (!UseGroundTileRandomRotation)
                return new[] { 0 };

            var steps = new List<int>(4);
            if (GroundTileRotationInclude0) steps.Add(0);
            if (GroundTileRotationInclude90) steps.Add(1);
            if (GroundTileRotationInclude180) steps.Add(2);
            if (GroundTileRotationInclude270) steps.Add(3);
            if (steps.Count == 0)
                steps.Add(0);
            return steps.ToArray();
        }

        private (bool MirrorX, bool MirrorY)[] GetMirrorOptions()
        {
            if (!GroundTileMirrorX && !GroundTileMirrorY)
                return new[] { (false, false) };

            var options = new List<(bool, bool)>(4) { (false, false) };
            if (GroundTileMirrorX)
                options.Add((true, false));
            if (GroundTileMirrorY)
                options.Add((false, true));
            if (GroundTileMirrorX && GroundTileMirrorY)
                options.Add((true, true));
            return options.ToArray();
        }

        private TileVariant[] BuildTileVariants(TileBase[] tiles)
        {
            bool allowRotation = UseGroundTileRandomRotation;
            bool allowMirroring = GroundTileMirrorX || GroundTileMirrorY;
            return BuildTileVariants(tiles, allowRotation, allowMirroring);
        }

        private TileVariant[] BuildTileVariants(TileBase[] tiles, bool allowRotation, bool allowMirroring)
        {
            if (tiles == null || tiles.Length == 0)
                return Array.Empty<TileVariant>();

            int[] steps = allowRotation ? GetRotationSteps() : new[] { 0 };
            var mirrorOptions = allowMirroring ? GetMirrorOptions() : new[] { (false, false) };
            var variants = new List<TileVariant>(tiles.Length * steps.Length * mirrorOptions.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                var baseProfile = GetEdgeProfile(tile);
                var unique = new HashSet<MatrixKey>();
                for (int m = 0; m < mirrorOptions.Length; m++)
                {
                    var (mirrorX, mirrorY) = mirrorOptions[m];
                    var mirroredProfile = baseProfile;
                    if (mirrorX)
                        mirroredProfile = MirrorProfileX(mirroredProfile);
                    if (mirrorY)
                        mirroredProfile = MirrorProfileY(mirroredProfile);

                    for (int s = 0; s < steps.Length; s++)
                    {
                        int step = steps[s];
                        float angle = step * 90f;
                        var scale = new Vector3(mirrorX ? -1f : 1f, mirrorY ? -1f : 1f, 1f);
                        var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, angle), scale);
                        var key = MatrixKey.From(transform);
                        if (!unique.Add(key)) continue;

                        var profile = RotateProfileCCW(mirroredProfile, step);
                        int id = ((tile.GetInstanceID() * 397) ^ key.GetHashCode());
                        if (id == int.MinValue)
                            id = int.MaxValue;
                        variants.Add(new TileVariant
                        {
                            Tile = tile,
                            Transform = transform,
                            Profile = profile,
                            Id = id
                        });
                    }
                }
            }
            return variants.ToArray();
        }

        private bool IsEdgeGreen(Color[] pixels, int width, int fixedIndex, int from, int to, bool horizontal)
        {
            int total = 0;
            int green = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int i = from; i <= to; i++)
            {
                int x = horizontal ? i : fixedIndex;
                int y = horizontal ? fixedIndex : i;
                int idx = (y * width) + x;
                var c = pixels[idx];
                if (c.a <= alphaThreshold) continue;
                total++;
                if (IsGreenDominant(c))
                    green++;
            }

            if (total <= 0) return false;
            float ratio = green / (float)total;
            return ratio >= GroundTileEdgeGreenRatio;
        }

        private bool IsGreenDominant(Color c)
        {
            float g = c.g;
            if (g < GroundTileEdgeGreenMin) return false;
            float maxOther = Mathf.Max(c.r, c.b);
            return (g - maxOther) >= GroundTileEdgeGreenDominance;
        }

        private bool IsWaterInteriorTile(TileBase tile)
        {
            if (tile == null) return false;
            var sprite = ExtractTileSprite(tile);
            if (sprite == null) return false;
            if (_waterInteriorCache.TryGetValue(sprite, out var cached))
                return cached;

            bool result = BuildWaterInteriorProfile(sprite);
            _waterInteriorCache[sprite] = result;
            return result;
        }

        private bool BuildWaterInteriorProfile(Sprite sprite)
        {
            if (sprite == null) return false;
            var texture = sprite.texture;
            if (texture == null) return false;
            var readable = GetReadableTexture(texture);
            if (readable == null) return false;

            var rect = sprite.textureRect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return false;

            var pixels = readable.GetPixels(x0, y0, width, height);
            if (pixels == null || pixels.Length == 0) return false;

            int inset = Mathf.Max(0, WaterEdgeSampleInsetPixels);
            int minX = Mathf.Clamp(inset, 0, width - 1);
            int maxX = Mathf.Clamp(width - 1 - inset, 0, width - 1);
            int minY = Mathf.Clamp(inset, 0, height - 1);
            int maxY = Mathf.Clamp(height - 1 - inset, 0, height - 1);
            if (maxX < minX || maxY < minY) return false;

            int band = Mathf.Max(1, WaterEdgeSampleBandPixels);
            int bottomMaxY = Mathf.Min(minY + band - 1, maxY);
            int topMinY = Mathf.Max(maxY - band + 1, minY);
            int leftMaxX = Mathf.Min(minX + band - 1, maxX);
            int rightMinX = Mathf.Max(maxX - band + 1, minX);

            bool bottomWater = GetEdgeWaterRatioBand(pixels, width, minX, maxX, minY, bottomMaxY) >= WaterEdgeBlueRatio;
            bool topWater = GetEdgeWaterRatioBand(pixels, width, minX, maxX, topMinY, maxY) >= WaterEdgeBlueRatio;
            bool leftWater = GetEdgeWaterRatioBand(pixels, width, minX, leftMaxX, minY, maxY) >= WaterEdgeBlueRatio;
            bool rightWater = GetEdgeWaterRatioBand(pixels, width, rightMinX, maxX, minY, maxY) >= WaterEdgeBlueRatio;
            if (!(bottomWater && topWater && leftWater && rightWater)) return false;

            int interiorInset = Mathf.Max(0, WaterInteriorSampleInsetPixels);
            int ix0 = Mathf.Clamp(interiorInset, 0, width - 1);
            int ix1 = Mathf.Clamp(width - 1 - interiorInset, 0, width - 1);
            int iy0 = Mathf.Clamp(interiorInset, 0, height - 1);
            int iy1 = Mathf.Clamp(height - 1 - interiorInset, 0, height - 1);
            if (ix1 < ix0 || iy1 < iy0) return false;

            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int y = iy0; y <= iy1; y++)
            {
                int row = y * width;
                for (int x = ix0; x <= ix1; x++)
                {
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsWaterDominant(c))
                        water++;
                }
            }

            if (total <= 0) return false;
            float ratio = water / (float)total;
            return ratio >= WaterInteriorBlueRatio;
        }

        private bool IsEdgeWater(Color[] pixels, int width, int fixedIndex, int from, int to, bool horizontal)
        {
            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int i = from; i <= to; i++)
            {
                int x = horizontal ? i : fixedIndex;
                int y = horizontal ? fixedIndex : i;
                int idx = (y * width) + x;
                var c = pixels[idx];
                if (c.a <= alphaThreshold) continue;
                total++;
                if (IsWaterDominant(c))
                    water++;
            }

            if (total <= 0) return false;
            float ratio = water / (float)total;
            return ratio >= WaterEdgeBlueRatio;
        }

        private float GetEdgeWaterRatioBand(Color[] pixels, int width, int minX, int maxX, int minY, int maxY)
        {
            if (minX > maxX || minY > maxY) return 0f;
            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsWaterDominant(c))
                        water++;
                }
            }

            if (total <= 0) return 0f;
            float ratio = water / (float)total;
            return ratio;
        }

        private byte BuildWaterEdgeMask(Color[] pixels, int width, int minX, int maxX, int minY, int maxY, int samples, bool horizontal)
        {
            if (minX > maxX || minY > maxY) return 0;
            samples = Mathf.Clamp(samples, 1, 8);
            int length = horizontal ? (maxX - minX + 1) : (maxY - minY + 1);
            if (length <= 0) return 0;
            float threshold = Mathf.Clamp01(WaterEdgeMaskRatioThreshold);
            byte mask = 0;
            for (int s = 0; s < samples; s++)
            {
                float t0 = s / (float)samples;
                float t1 = (s + 1) / (float)samples;
                int segStart = Mathf.FloorToInt(t0 * length);
                int segEnd = Mathf.FloorToInt(t1 * length) - 1;
                if (segEnd < segStart) segEnd = segStart;

                float ratio;
                if (horizontal)
                {
                    int x0 = Mathf.Clamp(minX + segStart, minX, maxX);
                    int x1 = Mathf.Clamp(minX + segEnd, minX, maxX);
                    ratio = GetEdgeWaterRatioBand(pixels, width, x0, x1, minY, maxY);
                }
                else
                {
                    int y0 = Mathf.Clamp(minY + segStart, minY, maxY);
                    int y1 = Mathf.Clamp(minY + segEnd, minY, maxY);
                    ratio = GetEdgeWaterRatioBand(pixels, width, minX, maxX, y0, y1);
                }

                if (ratio >= threshold)
                    mask |= (byte)(1 << s);
            }
            return mask;
        }

        private static byte ReverseMaskBits(byte mask, int samples)
        {
            samples = Mathf.Clamp(samples, 1, 8);
            byte reversed = 0;
            for (int i = 0; i < samples; i++)
            {
                if ((mask & (1 << i)) != 0)
                    reversed |= (byte)(1 << (samples - 1 - i));
            }
            return reversed;
        }

        private static int CountMaskDifference(byte a, byte b, int samples)
        {
            samples = Mathf.Clamp(samples, 1, 8);
            byte diff = (byte)(a ^ b);
            int count = 0;
            for (int i = 0; i < samples; i++)
            {
                if ((diff & (1 << i)) != 0)
                    count++;
            }
            return count;
        }

        private static byte CountMaskTransitions(byte mask, int samples)
        {
            samples = Mathf.Clamp(samples, 1, 8);
            int transitions = 0;
            int prev = (mask & 1) != 0 ? 1 : 0;
            for (int i = 1; i < samples; i++)
            {
                int bit = (mask & (1 << i)) != 0 ? 1 : 0;
                if (bit != prev)
                    transitions++;
                prev = bit;
            }
            return (byte)transitions;
        }

        private bool IsWaterDominant(Color c)
        {
            float b = c.b;
            if (b < WaterEdgeBlueMin) return false;
            float maxOther = Mathf.Max(c.r, c.g);
            return (b - maxOther) >= WaterEdgeBlueDominance;
        }

        private float GetWaterInteriorScore(TileBase tile)
        {
            if (tile == null) return 0f;
            var sprite = ExtractTileSprite(tile);
            if (sprite == null) return 0f;
            if (_waterInteriorScoreCache.TryGetValue(sprite, out var cached))
                return cached;
            float score = BuildWaterInteriorScore(sprite);
            _waterInteriorScoreCache[sprite] = score;
            return score;
        }

        private float BuildWaterInteriorScore(Sprite sprite)
        {
            if (sprite == null) return 0f;
            var texture = sprite.texture;
            if (texture == null) return 0f;
            var readable = GetReadableTexture(texture);
            if (readable == null) return 0f;

            var rect = sprite.textureRect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return 0f;

            var pixels = readable.GetPixels(x0, y0, width, height);
            if (pixels == null || pixels.Length == 0) return 0f;

            int inset = Mathf.Max(0, WaterInteriorSampleInsetPixels);
            int ix0 = Mathf.Clamp(inset, 0, width - 1);
            int ix1 = Mathf.Clamp(width - 1 - inset, 0, width - 1);
            int iy0 = Mathf.Clamp(inset, 0, height - 1);
            int iy1 = Mathf.Clamp(height - 1 - inset, 0, height - 1);
            if (ix1 < ix0 || iy1 < iy0) return 0f;

            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int y = iy0; y <= iy1; y++)
            {
                int row = y * width;
                for (int x = ix0; x <= ix1; x++)
                {
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsWaterDominant(c))
                        water++;
                }
            }

            if (total <= 0) return 0f;
            return water / (float)total;
        }

        private static TileBase PickLayerBaseTile(TerrainLayer layer, int col, int row, int seed)
        {
            if (layer == null || layer.BaseTiles == null || layer.BaseTiles.Length == 0)
                return null;
            return PickTileDeterministic(layer.BaseTiles, col, row, seed);
        }

        private TileVariant PickVariantDeterministic(TileVariant[] variants, int col, int row, int seed)
        {
            if (variants == null || variants.Length == 0)
                return default;
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            return variants[hash % variants.Length];
        }

        private TileVariant PickVariantDeterministicExcluding(
            TileVariant[] variants,
            int col,
            int row,
            int seed,
            string[] excludeKeywords)
        {
            if (variants == null || variants.Length == 0)
                return default;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return PickVariantDeterministic(variants, col, row, seed);
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            int start = hash % variants.Length;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[(start + i) % variants.Length];
                if (variant.Tile == null) continue;
                if (!IsTileExcludedByNameOrSprite(variant.Tile, excludeKeywords))
                    return variant;
            }
            return variants[start];
        }

        private TileVariant PickVariantWithConstraints(
            TileVariant[] variants,
            bool requireLeftGreen,
            bool requireBottomGreen,
            int requireLeftWater,
            int requireBottomWater,
            int requireRightWater,
            int requireTopWater,
            bool enforceEdge,
            bool enforceWaterEdge,
            bool enforceAntiRepeat,
            int leftId,
            int bottomId,
            byte leftWaterMask,
            bool hasLeftWaterMask,
            byte bottomWaterMask,
            bool hasBottomWaterMask,
            int col,
            int row,
            int seed)
        {
            if (variants == null || variants.Length == 0)
                return default;

            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            bool needEdge = enforceEdge && (requireLeftGreen || requireBottomGreen);
            bool needWaterEdge = enforceWaterEdge && (requireLeftWater != 0 || requireBottomWater != 0 || requireRightWater != 0 || requireTopWater != 0);
            bool needWaterEdgeLeftBottom = enforceWaterEdge && (requireLeftWater != 0 || requireBottomWater != 0);
            bool needAnti = enforceAntiRepeat && ((GroundTileAntiRepeatLeft && leftId != int.MinValue) || (GroundTileAntiRepeatBottom && bottomId != int.MinValue));

            var variant = TryPickVariant(variants, hash, needEdge, needWaterEdge, needAnti, requireLeftGreen, requireBottomGreen, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
            if (variant.Tile != null) return variant;

            if (needAnti)
            {
                variant = TryPickVariant(variants, hash, needEdge, needWaterEdge, false, requireLeftGreen, requireBottomGreen, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            if (needWaterEdge)
            {
                variant = TryPickVariant(variants, hash, false, needWaterEdge, false, false, false, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            if (needWaterEdge && (requireRightWater != 0 || requireTopWater != 0))
            {
                variant = TryPickVariant(variants, hash, false, needWaterEdgeLeftBottom, false, false, false, requireLeftWater, requireBottomWater, 0, 0, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            if (needEdge)
            {
                variant = TryPickVariant(variants, hash, needEdge, false, false, requireLeftGreen, requireBottomGreen, 0, 0, 0, 0, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            return PickVariantDeterministic(variants, col, row, seed);
        }

        private TileVariant TryPickVariant(
            TileVariant[] variants,
            int hash,
            bool enforceEdge,
            bool enforceWaterEdge,
            bool enforceAntiRepeat,
            bool requireLeftGreen,
            bool requireBottomGreen,
            int requireLeftWater,
            int requireBottomWater,
            int requireRightWater,
            int requireTopWater,
            int leftId,
            int bottomId,
            byte leftWaterMask,
            bool hasLeftWaterMask,
            byte bottomWaterMask,
            bool hasBottomWaterMask)
        {
            if (enforceWaterEdge)
            {
                float bestScore = float.MaxValue;
                for (int i = 0; i < variants.Length; i++)
                {
                    var variant = variants[i];
                    if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                        continue;
                    if (enforceEdge)
                    {
                        if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                        if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                    }
                    if (enforceAntiRepeat)
                    {
                        if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                        if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                    }

                    float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater)
                        + GetWaterMaskMismatchScore(variant.Profile, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                    if (score < bestScore)
                        bestScore = score;
                }

                if (bestScore < float.MaxValue)
                {
                    float tolerance = Mathf.Clamp01(WaterEdgeMismatchTolerance);
                    float threshold = bestScore + tolerance;
                    int candidateCount = 0;
                    for (int i = 0; i < variants.Length; i++)
                    {
                        var variant = variants[i];
                        if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                            continue;
                        if (enforceEdge)
                        {
                            if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                            if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                        }
                        if (enforceAntiRepeat)
                        {
                            if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                            if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                        }

                        float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater)
                            + GetWaterMaskMismatchScore(variant.Profile, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                        if (score <= threshold)
                            candidateCount++;
                    }

                    if (candidateCount > 0)
                    {
                        int candidatePick = hash % candidateCount;
                        for (int i = 0; i < variants.Length; i++)
                        {
                            var variant = variants[i];
                            if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                continue;
                            if (enforceEdge)
                            {
                                if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                            }
                            if (enforceAntiRepeat)
                            {
                                if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                            }

                            float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater)
                                + GetWaterMaskMismatchScore(variant.Profile, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                            if (score > threshold) continue;
                            if (candidatePick-- == 0) return variant;
                        }
                    }
                }

                return default;
            }

            int matchCount = 0;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (enforceEdge)
                {
                    if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                    if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                }
                if (enforceAntiRepeat)
                {
                    if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                    if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                }
                matchCount++;
            }

            if (matchCount == 0)
                return default;

            int pick = hash % matchCount;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (enforceEdge)
                {
                    if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                    if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                }
                if (enforceAntiRepeat)
                {
                    if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                    if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                }
                if (pick-- == 0) return variant;
            }

            return default;
        }

        private float GetWaterMismatchScore(EdgeProfile profile, int requireLeftWater, int requireBottomWater, int requireRightWater, int requireTopWater)
        {
            float score = 0f;
            if (requireLeftWater != 0)
                score += requireLeftWater > 0 ? (1f - profile.LeftWaterRatio) : profile.LeftWaterRatio;
            if (requireBottomWater != 0)
                score += requireBottomWater > 0 ? (1f - profile.BottomWaterRatio) : profile.BottomWaterRatio;
            if (requireRightWater != 0)
                score += requireRightWater > 0 ? (1f - profile.RightWaterRatio) : profile.RightWaterRatio;
            if (requireTopWater != 0)
                score += requireTopWater > 0 ? (1f - profile.TopWaterRatio) : profile.TopWaterRatio;
            return score;
        }

        private bool MatchesWaterEdgeRequirement(EdgeProfile profile, int requireLeftWater, int requireBottomWater, int requireRightWater, int requireTopWater)
        {
            float waterMin = Mathf.Clamp01(WaterEdgeBlueRatio);
            float landMax = Mathf.Clamp01(WaterEdgeLandMaxRatio);
            if (landMax > waterMin) landMax = waterMin;

            if (requireLeftWater > 0 && profile.LeftWaterRatio < waterMin) return false;
            if (requireLeftWater < 0 && profile.LeftWaterRatio > landMax) return false;
            if (requireBottomWater > 0 && profile.BottomWaterRatio < waterMin) return false;
            if (requireBottomWater < 0 && profile.BottomWaterRatio > landMax) return false;
            if (requireRightWater > 0 && profile.RightWaterRatio < waterMin) return false;
            if (requireRightWater < 0 && profile.RightWaterRatio > landMax) return false;
            if (requireTopWater > 0 && profile.TopWaterRatio < waterMin) return false;
            if (requireTopWater < 0 && profile.TopWaterRatio > landMax) return false;
            return true;
        }

        private float GetWaterMaskMismatchScore(EdgeProfile profile, byte leftMask, bool hasLeftMask, byte bottomMask, bool hasBottomMask)
        {
            float weight = Mathf.Max(0f, WaterEdgeMaskMatchWeight);
            if (weight <= 0f) return 0f;
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            float score = 0f;
            if (hasLeftMask)
                score += CountMaskDifference(profile.LeftWaterMask, leftMask, samples) / (float)samples;
            if (hasBottomMask)
                score += CountMaskDifference(profile.BottomWaterMask, bottomMask, samples) / (float)samples;
            score *= weight;

            float smoothWeight = Mathf.Max(0f, WaterEdgeSmoothnessWeight);
            if (smoothWeight > 0f)
            {
                float denom = Mathf.Max(1f, samples - 1f);
                if (hasLeftMask)
                    score += (profile.LeftWaterTransitions / denom) * smoothWeight;
                if (hasBottomMask)
                    score += (profile.BottomWaterTransitions / denom) * smoothWeight;
            }
            return score;
        }

        private void RefineWaterEdgesRect(
            int width,
            int height,
            bool[] waterMask,
            bool[] rockMask,
            int[] layerIndex,
            TileVariant[][] layerVariants,
            TileVariant[] waterVariants,
            TileVariant[] waterInteriorVariants,
            TileVariant[] rockVariants,
            TileVariant[] sharedVariants,
            int tileSeed,
            TileBase[] tiles,
            Matrix4x4[] transforms,
            EdgeProfile[] placedProfiles,
            bool[] placedValid,
            int[] placedVariantIds)
        {
            if (!UseWaterEdgeRefinement || !UseWaterEdgeColorMatch) return;
            if (waterMask == null || layerIndex == null || layerVariants == null) return;
            if (placedProfiles == null || placedValid == null || placedVariantIds == null) return;
            if (tiles == null || tiles.Length == 0) return;
            int passes = Mathf.Clamp(WaterEdgeRefinePasses, 0, 4);
            if (passes <= 0) return;

            bool useSharedTiles = UseSharedGroundTiles && SharedGroundTileChance > 0f;
            Matrix4x4 identity = Matrix4x4.identity;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        int idx = (row * width) + col;
                        if (!placedValid[idx]) continue;

                        bool isWater = waterMask[idx];
                        bool isWaterHole = !isWater && IsMaskHole(waterMask, width, height, col, row);
                        if (isWaterHole) isWater = true;
                        bool isRock = !isWater && rockMask != null && rockMask[idx];
                        bool isWaterInterior = isWater && (isWaterHole || IsMaskInterior(waterMask, width, height, col, row));

                        bool leftWater = col > 0 && IsWaterCell(waterMask, width, height, col - 1, row);
                        bool rightWater = col < width - 1 && IsWaterCell(waterMask, width, height, col + 1, row);
                        bool bottomWater = row > 0 && IsWaterCell(waterMask, width, height, col, row - 1);
                        bool topWater = row < height - 1 && IsWaterCell(waterMask, width, height, col, row + 1);
                        bool hasWaterNeighbor = leftWater || rightWater || bottomWater || topWater;
                        bool hasLandNeighbor = !leftWater || !rightWater || !bottomWater || !topWater;
                        if (!hasWaterNeighbor) continue;
                        if (isWater && !hasLandNeighbor) continue;

                        int layerIdx = layerIndex[idx];
                        if (layerIdx < 0 || layerIdx >= layerVariants.Length) continue;

                        bool useSharedNow = !isWater && !isRock && useSharedTiles && sharedVariants != null && sharedVariants.Length > 0
                            && ShouldUseSharedTiles(col, row, layerIdx, tileSeed, SharedGroundTileChance);

                        TileVariant[] variants;
                        if (isWater && waterVariants != null && waterVariants.Length > 0)
                            variants = (isWaterInterior && waterInteriorVariants != null && waterInteriorVariants.Length > 0)
                                ? waterInteriorVariants
                                : waterVariants;
                        else if (isRock && rockVariants != null && rockVariants.Length > 0)
                            variants = rockVariants;
                        else
                            variants = useSharedNow && sharedVariants != null && sharedVariants.Length > 0
                                ? sharedVariants
                                : layerVariants[layerIdx];
                        if (variants == null || variants.Length == 0)
                            continue;

                        bool requireLeftGreen = false;
                        bool requireBottomGreen = false;
                        int requireLeftWater = 0;
                        int requireBottomWater = 0;
                        int requireRightWater = 0;
                        int requireTopWater = 0;
                        byte leftMask = 0;
                        byte rightMask = 0;
                        byte bottomMask = 0;
                        byte topMask = 0;
                        bool hasLeftMask = false;
                        bool hasRightMask = false;
                        bool hasBottomMask = false;
                        bool hasTopMask = false;
                        int leftId = int.MinValue;
                        int bottomId = int.MinValue;

                        if (col > 0 && placedValid[idx - 1])
                        {
                            var leftProfile = placedProfiles[idx - 1];
                            requireLeftGreen = leftProfile.RightGreen;
                            leftId = placedVariantIds[idx - 1];
                            if (isWater != leftWater)
                            {
                                hasLeftMask = true;
                                leftMask = leftProfile.RightWaterMask;
                            }
                        }
                        if (col < width - 1 && placedValid[idx + 1])
                        {
                            var rightProfile = placedProfiles[idx + 1];
                            if (isWater != rightWater)
                            {
                                hasRightMask = true;
                                rightMask = rightProfile.LeftWaterMask;
                            }
                        }
                        if (row > 0 && placedValid[idx - width])
                        {
                            var bottomProfile = placedProfiles[idx - width];
                            requireBottomGreen = bottomProfile.TopGreen;
                            bottomId = placedVariantIds[idx - width];
                            if (isWater != bottomWater)
                            {
                                hasBottomMask = true;
                                bottomMask = bottomProfile.TopWaterMask;
                            }
                        }
                        if (row < height - 1 && placedValid[idx + width])
                        {
                            var topProfile = placedProfiles[idx + width];
                            if (isWater != topWater)
                            {
                                hasTopMask = true;
                                topMask = topProfile.BottomWaterMask;
                            }
                        }

                        if (col > 0) requireLeftWater = leftWater ? 1 : -1;
                        if (row > 0) requireBottomWater = bottomWater ? 1 : -1;
                        if (col < width - 1) requireRightWater = rightWater ? 1 : -1;
                        if (row < height - 1) requireTopWater = topWater ? 1 : -1;

                        bool enforceEdge = UseGroundTileEdgeColorMatch && !isWater && !isRock;
                        bool enforceAntiRepeat = UseGroundTileAntiRepeat;

                        float bestScore = float.MaxValue;
                        TileVariant best = default;
                        for (int i = 0; i < variants.Length; i++)
                        {
                            var variant = variants[i];
                            if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                continue;
                            if (enforceEdge)
                            {
                                if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                            }
                            if (enforceAntiRepeat)
                            {
                                if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                            }

                            float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater);
                            score += GetWaterMaskMismatchScoreAll(
                                variant.Profile,
                                leftMask,
                                hasLeftMask,
                                rightMask,
                                hasRightMask,
                                bottomMask,
                                hasBottomMask,
                                topMask,
                                hasTopMask);

                            if (score < bestScore)
                            {
                                bestScore = score;
                                best = variant;
                                if (bestScore <= 0f)
                                    break;
                            }
                        }

                        if (best.Tile != null)
                        {
                            float tolerance = Mathf.Clamp01(WaterEdgeMismatchTolerance);
                            float threshold = bestScore + tolerance;
                            int hash = (col * 73856093) ^ (row * 19349663) ^ tileSeed ^ (pass * 83492791);
                            if (hash < 0) hash = -hash;
                            int candidateCount = 0;
                            for (int i = 0; i < variants.Length; i++)
                            {
                                var variant = variants[i];
                                if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                    continue;
                                if (enforceEdge)
                                {
                                    if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                    if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                                }
                                if (enforceAntiRepeat)
                                {
                                    if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                    if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                                }

                                float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater);
                                score += GetWaterMaskMismatchScoreAll(
                                    variant.Profile,
                                    leftMask,
                                    hasLeftMask,
                                    rightMask,
                                    hasRightMask,
                                    bottomMask,
                                    hasBottomMask,
                                    topMask,
                                    hasTopMask);

                                if (score <= threshold)
                                    candidateCount++;
                            }

                            if (candidateCount > 0)
                            {
                                int pick = hash % candidateCount;
                                for (int i = 0; i < variants.Length; i++)
                                {
                                    var variant = variants[i];
                                    if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                        continue;
                                    if (enforceEdge)
                                    {
                                        if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                        if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                                    }
                                    if (enforceAntiRepeat)
                                    {
                                        if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                        if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                                    }

                                    float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater);
                                    score += GetWaterMaskMismatchScoreAll(
                                        variant.Profile,
                                        leftMask,
                                        hasLeftMask,
                                        rightMask,
                                        hasRightMask,
                                        bottomMask,
                                        hasBottomMask,
                                        topMask,
                                        hasTopMask);

                                    if (score > threshold) continue;
                                    if (pick-- == 0)
                                    {
                                        best = variant;
                                        break;
                                    }
                                }
                            }

                            if (best.Tile != null && best.Id != placedVariantIds[idx])
                            {
                                tiles[idx] = best.Tile;
                                if (transforms != null)
                                    transforms[idx] = best.Transform == default ? identity : best.Transform;
                                placedProfiles[idx] = best.Profile;
                                placedVariantIds[idx] = best.Id;
                            }
                        }
                    }
                }
            }
        }

        private float GetWaterMaskMismatchScoreAll(
            EdgeProfile profile,
            byte leftMask,
            bool hasLeftMask,
            byte rightMask,
            bool hasRightMask,
            byte bottomMask,
            bool hasBottomMask,
            byte topMask,
            bool hasTopMask)
        {
            float weight = Mathf.Max(0f, WaterEdgeMaskMatchWeight);
            if (weight <= 0f) return 0f;
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            float score = 0f;
            if (hasLeftMask)
                score += CountMaskDifference(profile.LeftWaterMask, leftMask, samples) / (float)samples;
            if (hasRightMask)
                score += CountMaskDifference(profile.RightWaterMask, rightMask, samples) / (float)samples;
            if (hasBottomMask)
                score += CountMaskDifference(profile.BottomWaterMask, bottomMask, samples) / (float)samples;
            if (hasTopMask)
                score += CountMaskDifference(profile.TopWaterMask, topMask, samples) / (float)samples;
            score *= weight;

            float smoothWeight = Mathf.Max(0f, WaterEdgeSmoothnessWeight);
            if (smoothWeight > 0f)
            {
                float denom = Mathf.Max(1f, samples - 1f);
                if (hasLeftMask)
                    score += (profile.LeftWaterTransitions / denom) * smoothWeight;
                if (hasRightMask)
                    score += (profile.RightWaterTransitions / denom) * smoothWeight;
                if (hasBottomMask)
                    score += (profile.BottomWaterTransitions / denom) * smoothWeight;
                if (hasTopMask)
                    score += (profile.TopWaterTransitions / denom) * smoothWeight;
            }
            return score;
        }

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
            if (_ground == null || layers == null || layerIndex == null) return;

            bool useTransition = UseTransitionTilemap && _transitions != null;
            if (skipBase && !useTransition)
            {
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        int idx = (row * width) + col;
                        int layerIdx = layerIndex[idx];
                        if (layerIdx < 0 || layerIdx >= layers.Count) continue;
                        var layer = layers[layerIdx];
                        if (layer == null || layer.EdgeTiles == null || layer.EdgeTiles.Count == 0) continue;
                        int mask = BuildNeighborMask(col, row, width, height, layerIdx, layerIndex, ruleset);
                        if (mask == 0) continue;
                        var edgeTile = PickEdgeTile(mask, layer, edgeLookup[layerIdx], edgeByBits[layerIdx]);
                        if (edgeTile == null) continue;
                        _ground.SetTile(new Vector3Int(col, row, 0), edgeTile);
                    }
                }
                return;
            }

            int size = width * height;
            TileBase[] groundTiles = (!skipBase || !useTransition) ? new TileBase[size] : null;
            TileBase[] edgeTiles = useTransition ? new TileBase[size] : null;

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int idx = (row * width) + col;
                    int layerIdx = layerIndex[idx];
                    if (layerIdx < 0 || layerIdx >= layers.Count) continue;
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
                        if (mask != 0)
                            edgeTile = PickEdgeTile(mask, layer, edgeLookup[layerIdx], edgeByBits[layerIdx]);
                    }

                    if (useTransition)
                    {
                        if (!skipBase && groundTiles != null)
                            groundTiles[idx] = baseTile;
                        if (edgeTiles != null)
                            edgeTiles[idx] = edgeTile;
                    }
                    else if (groundTiles != null)
                    {
                        groundTiles[idx] = edgeTile ?? baseTile;
                    }
                }
            }

            var bounds = new BoundsInt(0, 0, 0, width, height, 1);
            if (groundTiles != null && (!skipBase || !useTransition))
                _ground.SetTilesBlock(bounds, groundTiles);
            if (edgeTiles != null)
                _transitions.SetTilesBlock(bounds, edgeTiles);
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
            if (_ground == null || layers == null || layerIndex == null) yield break;

            bool useTransition = UseTransitionTilemap && _transitions != null;
            int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);

            if (skipBase && !useTransition)
            {
                for (int row = 0; row < height; row += rowsPerFrame)
                {
                    int rowCount = Mathf.Min(rowsPerFrame, height - row);
                    for (int r = 0; r < rowCount; r++)
                    {
                        int rowIdx = row + r;
                        for (int col = 0; col < width; col++)
                        {
                            int idx = (rowIdx * width) + col;
                            int layerIdx = layerIndex[idx];
                            if (layerIdx < 0 || layerIdx >= layers.Count) continue;
                            var layer = layers[layerIdx];
                            if (layer == null || layer.EdgeTiles == null || layer.EdgeTiles.Count == 0) continue;
                            int mask = BuildNeighborMask(col, rowIdx, width, height, layerIdx, layerIndex, ruleset);
                            if (mask == 0) continue;
                            var edgeTile = PickEdgeTile(mask, layer, edgeLookup[layerIdx], edgeByBits[layerIdx]);
                            if (edgeTile == null) continue;
                            _ground.SetTile(new Vector3Int(col, rowIdx, 0), edgeTile);
                        }
                    }
                    yield return null;
                }
                yield break;
            }

            for (int row = 0; row < height; row += rowsPerFrame)
            {
                int rowCount = Mathf.Min(rowsPerFrame, height - row);
                int blockSize = width * rowCount;
                TileBase[] groundTiles = (!skipBase || !useTransition) ? new TileBase[blockSize] : null;
                TileBase[] edgeTiles = useTransition ? new TileBase[blockSize] : null;

                for (int r = 0; r < rowCount; r++)
                {
                    int rowIdx = row + r;
                    int rowBase = r * width;
                    for (int col = 0; col < width; col++)
                    {
                        int globalIdx = (rowIdx * width) + col;
                        int layerIdx = layerIndex[globalIdx];
                        if (layerIdx < 0 || layerIdx >= layers.Count) continue;
                        var layer = layers[layerIdx];

                        TileBase baseTile = null;
                        if (!skipBase)
                        {
                            baseTile = PickLayerBaseTile(layer, col, rowIdx, tileSeed);
                            if (ConvertGroundTilesRuntime)
                                baseTile = GetOrCreateRuntimeGroundTile(baseTile);
                        }

                        TileBase edgeTile = null;
                        if (layer != null && layer.EdgeTiles != null && layer.EdgeTiles.Count > 0)
                        {
                            int mask = BuildNeighborMask(col, rowIdx, width, height, layerIdx, layerIndex, ruleset);
                            if (mask != 0)
                                edgeTile = PickEdgeTile(mask, layer, edgeLookup[layerIdx], edgeByBits[layerIdx]);
                        }

                        int blockIdx = rowBase + col;
                        if (useTransition)
                        {
                            if (!skipBase && groundTiles != null)
                                groundTiles[blockIdx] = baseTile;
                            if (edgeTiles != null)
                                edgeTiles[blockIdx] = edgeTile;
                        }
                        else if (groundTiles != null)
                        {
                            groundTiles[blockIdx] = edgeTile ?? baseTile;
                        }
                    }
                }

                var bounds = new BoundsInt(0, row, 0, width, rowCount, 1);
                if (groundTiles != null && (!skipBase || !useTransition))
                    _ground.SetTilesBlock(bounds, groundTiles);
                if (edgeTiles != null)
                    _transitions.SetTilesBlock(bounds, edgeTiles);

                yield return null;
            }
        }

        private void ApplyRulesetToBackground(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            Dictionary<TileBase, TileBase> backgroundLookup)
        {
            if (_background == null || layers == null || layerIndex == null) return;
            int size = width * height;
            var tiles = new TileBase[size];
            bool useVariants = UseGroundTileRandomRotation
                || GroundTileMirrorX
                || GroundTileMirrorY
                || UseGroundTileEdgeColorMatch
                || UseWaterEdgeColorMatch
                || UseGroundTileAntiRepeat;
            EdgeProfile[] placedProfiles = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch) ? new EdgeProfile[size] : null;
            bool[] placedValid = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new bool[size] : null;
            int[] placedVariantIds = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new int[size] : null;
            bool useTransform = UseGroundTileRandomRotation || GroundTileMirrorX || GroundTileMirrorY;
            Matrix4x4[] transforms = useTransform ? new Matrix4x4[size] : null;
            Matrix4x4 identity = Matrix4x4.identity;

            TileVariant[][] layerVariants = useVariants ? new TileVariant[layers.Count][] : null;
            TileBase[][] layerTiles = useVariants ? null : new TileBase[layers.Count][];
            bool useSharedTiles = UseSharedGroundTiles && SharedGroundTileChance > 0f;
            TileVariant[] sharedVariants = null;
            TileBase[] sharedTiles = null;
            bool useWaterBiome = UseWaterBiome;
            string[] waterExclude = useWaterBiome ? ResolveWaterExcludeKeywords() : null;
            bool[] waterMask = null;
            bool[] rockMask = null;
            TileVariant[] waterVariants = null;
            TileVariant[] rockVariants = null;
            TileBase[] waterTiles = null;
            TileBase[] rockTiles = null;
            TileVariant[] waterInteriorVariants = null;
            TileBase[] waterInteriorTiles = null;
            TileVariant[] landVariants = null;
            TileBase[] landTiles = null;
            HashSet<TileBase> waterSet = null;
            HashSet<TileBase> rockSet = null;

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.BaseTiles == null || layer.BaseTiles.Length == 0)
                {
                    if (layerVariants != null) layerVariants[i] = Array.Empty<TileVariant>();
                    if (layerTiles != null) layerTiles[i] = Array.Empty<TileBase>();
                    continue;
                }

                var baseTiles = layer.BaseTiles;
                var mappedTiles = new TileBase[baseTiles.Length];
                for (int t = 0; t < baseTiles.Length; t++)
                    mappedTiles[t] = MapBackgroundTile(baseTiles[t], backgroundLookup);

                if (useVariants)
                    layerVariants[i] = BuildTileVariants(mappedTiles);
                else
                    layerTiles[i] = mappedTiles;
            }

            if (useWaterBiome)
            {
                var biomeCandidates = (AutoTerrainGroupByPrefix && GroundTiles != null && GroundTiles.Length > 0)
                    ? GroundTiles
                    : CollectUniqueLayerTiles(layers);
                waterTiles = ResolveBiomeTilesByName(biomeCandidates, WaterTileNameKeywords);
                if (waterTiles != null && waterTiles.Length > 0)
                    waterTiles = ExcludeTilesByNameOrSprite(waterTiles, waterExclude);
                rockTiles = ResolveBiomeTilesByName(biomeCandidates, RockTileNameKeywords);
                waterInteriorTiles = ResolveBiomeTilesByName(biomeCandidates, WaterInteriorTileNameKeywords);
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                    waterInteriorTiles = ExcludeTilesByNameOrSprite(waterInteriorTiles, waterExclude);
                if (waterTiles != null && waterTiles.Length > 0)
                {
                    for (int t = 0; t < waterTiles.Length; t++)
                        waterTiles[t] = MapBackgroundTile(waterTiles[t], backgroundLookup);
                    waterTiles = ExcludeTilesByNameOrSprite(waterTiles, waterExclude);
                }
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                {
                    for (int t = 0; t < waterInteriorTiles.Length; t++)
                        waterInteriorTiles[t] = MapBackgroundTile(waterInteriorTiles[t], backgroundLookup);
                    waterInteriorTiles = ExcludeTilesByNameOrSprite(waterInteriorTiles, waterExclude);
                }
                if (UseWaterAutoInteriorByColor)
                {
                    var allWaterTiles = CombineTiles(waterTiles, waterInteriorTiles);
                    if (allWaterTiles != null && allWaterTiles.Length > 0)
                    {
                        var autoInterior = new List<TileBase>();
                        for (int t = 0; t < allWaterTiles.Length; t++)
                        {
                            var tile = allWaterTiles[t];
                            if (tile != null && IsWaterInteriorTile(tile))
                                autoInterior.Add(tile);
                        }
                        if (autoInterior.Count == 0 && WaterInteriorFallbackCount > 0)
                        {
                            var scored = new List<TileScore>(allWaterTiles.Length);
                            for (int t = 0; t < allWaterTiles.Length; t++)
                            {
                                var tile = allWaterTiles[t];
                                if (tile == null) continue;
                                scored.Add(new TileScore(tile, GetWaterInteriorScore(tile)));
                            }
                            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
                            int take = Mathf.Clamp(WaterInteriorFallbackCount, 0, scored.Count);
                            for (int i = 0; i < take; i++)
                            {
                                if (scored[i].Tile != null)
                                    autoInterior.Add(scored[i].Tile);
                            }
                        }
                        if (autoInterior.Count > 0)
                            waterInteriorTiles = CombineTiles(waterInteriorTiles, autoInterior.ToArray());
                    }
                }
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0 && waterTiles != null && waterTiles.Length > 0)
                {
                    var interiorSet = new HashSet<TileBase>(waterInteriorTiles);
                    var filtered = new List<TileBase>(waterTiles.Length);
                    for (int t = 0; t < waterTiles.Length; t++)
                    {
                        var tile = waterTiles[t];
                        if (tile != null && !interiorSet.Contains(tile))
                            filtered.Add(tile);
                    }
                    waterTiles = filtered.Count > 0 ? filtered.ToArray() : null;
                }
                if (useVariants)
                {
                    if (waterTiles != null && waterTiles.Length > 0)
                    {
                        waterVariants = BuildTileVariants(waterTiles, UseGroundTileRandomRotation && WaterTilesAllowRotation, (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                        waterVariants = ExcludeVariantsByNameOrSprite(waterVariants, waterExclude);
                    }
                    if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                    {
                        waterInteriorVariants = BuildTileVariants(waterInteriorTiles, UseGroundTileRandomRotation && WaterTilesAllowRotation, (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                        waterInteriorVariants = ExcludeVariantsByNameOrSprite(waterInteriorVariants, waterExclude);
                    }
                }
                if ((waterTiles != null && waterTiles.Length > 0) || (waterInteriorTiles != null && waterInteriorTiles.Length > 0))
                {
                    waterMask = BuildWaterMaskRect(width, height, tileSeed, out _);
                    var waterAllTiles = CombineTiles(waterTiles, waterInteriorTiles);
                    waterSet = BuildTileSet(waterAllTiles);
                    if (waterTiles == null || waterTiles.Length == 0)
                    {
                        waterTiles = waterInteriorTiles;
                        waterVariants = waterInteriorVariants;
                    }
                }
                if (rockTiles != null && rockTiles.Length > 0 && waterMask != null)
                {
                    for (int t = 0; t < rockTiles.Length; t++)
                        rockTiles[t] = MapBackgroundTile(rockTiles[t], backgroundLookup);
                    if (useVariants)
                        rockVariants = BuildTileVariants(rockTiles, UseGroundTileRandomRotation && RockTilesAllowRotation, (GroundTileMirrorX || GroundTileMirrorY) && RockTilesAllowMirroring);
                    rockMask = BuildRockMaskFromWater(width, height, waterMask, tileSeed);
                    rockSet = BuildTileSet(rockTiles);
                }
                if (waterMask == null)
                    useWaterBiome = false;
            }

            if (useWaterBiome)
            {
                var allTiles = CollectUniqueLayerTiles(layers) ?? GroundTiles;
                if (allTiles != null && allTiles.Length > 0)
                {
                    var unique = new HashSet<TileBase>();
                    var list = new List<TileBase>();
                    for (int i = 0; i < allTiles.Length; i++)
                    {
                        var mapped = MapBackgroundTile(allTiles[i], backgroundLookup);
                        if (mapped == null) continue;
                        if (waterSet != null && waterSet.Contains(mapped)) continue;
                        if (rockSet != null && rockSet.Contains(mapped)) continue;
                        if (unique.Add(mapped))
                            list.Add(mapped);
                    }
                    landTiles = list.Count > 0 ? list.ToArray() : null;
                    if (waterExclude != null && waterExclude.Length > 0)
                        landTiles = ExcludeTilesByNameOrSprite(landTiles, waterExclude);
                    if (useVariants && landTiles != null)
                        landVariants = BuildTileVariants(landTiles);
                }
            }

            if (useSharedTiles)
            {
                sharedTiles = ResolveSharedGroundTiles(layers);
                if (sharedTiles == null || sharedTiles.Length == 0)
                {
                    useSharedTiles = false;
                }
                else
                {
                    for (int t = 0; t < sharedTiles.Length; t++)
                    {
                        var tile = sharedTiles[t];
                        if (tile != null && backgroundLookup != null && backgroundLookup.TryGetValue(tile, out var mapped) && mapped != null)
                            sharedTiles[t] = mapped;
                        else if (ConvertGroundTilesRuntime)
                            sharedTiles[t] = GetOrCreateRuntimeGroundTile(tile);
                    }
                    if (useVariants)
                        sharedVariants = BuildTileVariants(sharedTiles);
                }
            }

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int idx = (row * width) + col;
                    int layerIdx = layerIndex[idx];
                    if (layerIdx < 0 || layerIdx >= layers.Count) continue;
                    bool isWater = useWaterBiome && waterMask != null && waterMask[idx];
                    bool isWaterHole = !isWater && useWaterBiome && IsMaskHole(waterMask, width, height, col, row);
                    if (isWaterHole) isWater = true;
                    bool isRock = !isWater && rockMask != null && rockMask[idx];
                    bool useSharedNow = !isWater && !isRock && useSharedTiles && sharedTiles != null && sharedTiles.Length > 0
                        && ShouldUseSharedTiles(col, row, layerIdx, tileSeed, SharedGroundTileChance);
                    bool isWaterInterior = isWater && (isWaterHole || IsMaskInterior(waterMask, width, height, col, row));

                    if (!useVariants)
                    {
                        TileBase[] palette;
                        if (isWater && waterTiles != null && waterTiles.Length > 0)
                            palette = (isWaterInterior && waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                                ? waterInteriorTiles
                                : waterTiles;
                        else if (isRock && rockTiles != null && rockTiles.Length > 0)
                            palette = rockTiles;
                        else
                            palette = useSharedNow ? sharedTiles : layerTiles[layerIdx];
                        if (palette == null || palette.Length == 0) continue;
                        var picked = PickTileDeterministic(palette, col, row, tileSeed);
                        if (!isWater && !isRock && (waterSet != null || rockSet != null))
                        {
                            bool disallowed = (waterSet != null && waterSet.Contains(picked)) || (rockSet != null && rockSet.Contains(picked));
                            if (disallowed && landTiles != null && landTiles.Length > 0)
                                picked = PickTileDeterministic(landTiles, col, row, tileSeed);
                        }
                        if (!isWater && !isRock && IsTileExcludedByNameOrSprite(picked, waterExclude))
                        {
                            if (landTiles != null && landTiles.Length > 0)
                                picked = PickTileDeterministic(landTiles, col, row, tileSeed);
                            else
                                picked = PickTileDeterministicExcluding(palette, col, row, tileSeed, waterExclude);
                        }
                        tiles[idx] = picked;
                        continue;
                    }

                    TileVariant[] variants;
                    if (isWater && waterVariants != null && waterVariants.Length > 0)
                        variants = (isWaterInterior && waterInteriorVariants != null && waterInteriorVariants.Length > 0)
                            ? waterInteriorVariants
                            : waterVariants;
                    else if (isRock && rockVariants != null && rockVariants.Length > 0)
                        variants = rockVariants;
                    else
                        variants = useSharedNow && sharedVariants != null && sharedVariants.Length > 0
                            ? sharedVariants
                            : layerVariants[layerIdx];
                    if (variants == null || variants.Length == 0)
                        continue;

                    bool requireLeftGreen = false;
                    bool requireBottomGreen = false;
                    int requireLeftWater = 0;
                    int requireBottomWater = 0;
                    int requireRightWater = 0;
                    int requireTopWater = 0;
                    byte leftWaterMask = 0;
                    byte bottomWaterMask = 0;
                    bool hasLeftWaterMask = false;
                    bool hasBottomWaterMask = false;
                    int leftId = int.MinValue;
                    int bottomId = int.MinValue;
                    if (placedValid != null)
                    {
                        if (col > 0)
                        {
                            int leftIdx = idx - 1;
                            if (placedValid[leftIdx])
                            {
                                if (placedProfiles != null && placedProfiles[leftIdx].RightGreen)
                                    requireLeftGreen = true;
                                if (placedProfiles != null)
                                {
                                    hasLeftWaterMask = true;
                                    leftWaterMask = placedProfiles[leftIdx].RightWaterMask;
                                }
                                if (placedVariantIds != null)
                                    leftId = placedVariantIds[leftIdx];
                            }
                        }
                        if (row > 0)
                        {
                            int bottomIdx = idx - width;
                            if (placedValid[bottomIdx])
                            {
                                if (placedProfiles != null && placedProfiles[bottomIdx].TopGreen)
                                    requireBottomGreen = true;
                                if (placedProfiles != null)
                                {
                                    hasBottomWaterMask = true;
                                    bottomWaterMask = placedProfiles[bottomIdx].TopWaterMask;
                                }
                                if (placedVariantIds != null)
                                    bottomId = placedVariantIds[bottomIdx];
                            }
                        }
                    }

                    bool enforceEdge = UseGroundTileEdgeColorMatch && !isWater && !isRock;
                    bool enforceWaterEdge = false;
                    if (UseWaterEdgeColorMatch && waterMask != null)
                    {
                        bool leftWater = col > 0 && IsWaterCell(waterMask, width, height, col - 1, row);
                        bool bottomWater = row > 0 && IsWaterCell(waterMask, width, height, col, row - 1);
                        bool rightWater = col < width - 1 && IsWaterCell(waterMask, width, height, col + 1, row);
                        bool topWater = row < height - 1 && IsWaterCell(waterMask, width, height, col, row + 1);
                        bool hasWaterNeighbor = leftWater || bottomWater || rightWater || topWater;

                        enforceWaterEdge = isWater || isRock || hasWaterNeighbor;
                        if (enforceWaterEdge)
                        {
                            if (col > 0)
                                requireLeftWater = leftWater ? 1 : -1;
                            if (row > 0)
                                requireBottomWater = bottomWater ? 1 : -1;
                            if (col < width - 1)
                                requireRightWater = rightWater ? 1 : -1;
                            if (row < height - 1)
                                requireTopWater = topWater ? 1 : -1;
                        }
                    }
                    TileVariant variant = PickVariantWithConstraints(
                        variants,
                        requireLeftGreen,
                        requireBottomGreen,
                        requireLeftWater,
                        requireBottomWater,
                        requireRightWater,
                        requireTopWater,
                        enforceEdge,
                        enforceWaterEdge,
                        UseGroundTileAntiRepeat,
                        leftId,
                        bottomId,
                        leftWaterMask,
                        hasLeftWaterMask,
                        bottomWaterMask,
                        hasBottomWaterMask,
                        col,
                        row,
                        tileSeed);

                    if (variant.Tile == null && useSharedNow)
                    {
                        var fallback = layerVariants[layerIdx];
                        if (fallback != null && fallback.Length > 0)
                        {
                            variant = PickVariantWithConstraints(
                                fallback,
                                requireLeftGreen,
                                requireBottomGreen,
                                requireLeftWater,
                                requireBottomWater,
                                requireRightWater,
                                requireTopWater,
                            enforceEdge,
                            enforceWaterEdge,
                            UseGroundTileAntiRepeat,
                            leftId,
                            bottomId,
                            leftWaterMask,
                            hasLeftWaterMask,
                            bottomWaterMask,
                            hasBottomWaterMask,
                            col,
                            row,
                            tileSeed);
                        }
                    }

                    if (!isWater && !isRock && IsTileExcludedByNameOrSprite(variant.Tile, waterExclude))
                    {
                        if (landVariants != null && landVariants.Length > 0)
                        {
                            variant = PickVariantWithConstraints(
                                landVariants,
                                requireLeftGreen,
                                requireBottomGreen,
                                requireLeftWater,
                                requireBottomWater,
                                requireRightWater,
                                requireTopWater,
                                enforceEdge,
                                enforceWaterEdge,
                                UseGroundTileAntiRepeat,
                                leftId,
                                bottomId,
                                leftWaterMask,
                                hasLeftWaterMask,
                                bottomWaterMask,
                                hasBottomWaterMask,
                                col,
                                row,
                                tileSeed);
                        }
                        else
                        {
                            variant = PickVariantDeterministicExcluding(variants, col, row, tileSeed, waterExclude);
                        }
                    }

                    tiles[idx] = variant.Tile;
                    if (transforms != null)
                        transforms[idx] = variant.Transform;
                    if (placedValid != null)
                    {
                        if (placedProfiles != null)
                            placedProfiles[idx] = variant.Profile;
                        placedValid[idx] = variant.Tile != null;
                        if (placedVariantIds != null)
                            placedVariantIds[idx] = variant.Id;
                    }

                    if (!isWater && !isRock && variant.Tile != null && (waterSet != null || rockSet != null))
                    {
                        bool disallowed = (waterSet != null && waterSet.Contains(variant.Tile)) || (rockSet != null && rockSet.Contains(variant.Tile));
                        if (disallowed && landVariants != null && landVariants.Length > 0)
                        {
                            var landVariant = PickVariantWithConstraints(
                                landVariants,
                                requireLeftGreen,
                                requireBottomGreen,
                                requireLeftWater,
                                requireBottomWater,
                                requireRightWater,
                                requireTopWater,
                                enforceEdge,
                                enforceWaterEdge,
                                UseGroundTileAntiRepeat,
                                leftId,
                                bottomId,
                                leftWaterMask,
                                hasLeftWaterMask,
                                bottomWaterMask,
                                hasBottomWaterMask,
                                col,
                                row,
                                tileSeed);
                            tiles[idx] = landVariant.Tile;
                            if (transforms != null)
                                transforms[idx] = landVariant.Transform;
                            if (placedValid != null)
                            {
                                if (placedProfiles != null)
                                    placedProfiles[idx] = landVariant.Profile;
                                placedValid[idx] = landVariant.Tile != null;
                                if (placedVariantIds != null)
                                    placedVariantIds[idx] = landVariant.Id;
                            }
                        }
                    }
                }
            }

            RefineWaterEdgesRect(
                width,
                height,
                waterMask,
                rockMask,
                layerIndex,
                layerVariants,
                waterVariants,
                waterInteriorVariants,
                rockVariants,
                sharedVariants,
                tileSeed,
                tiles,
                transforms,
                placedProfiles,
                placedValid,
                placedVariantIds);

            var bounds = new BoundsInt(0, 0, 0, width, height, 1);
            _background.SetTilesBlock(bounds, tiles);
            if (transforms != null)
            {
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        int idx = (row * width) + col;
                        if (tiles[idx] == null) continue;
                        var transform = transforms[idx];
                        if (transform == default || transform == identity)
                            transform = GetTileTransform(tiles[idx]);
                        _background.SetTransformMatrix(new Vector3Int(col, row, 0), transform);
                    }
                }
            }
            CacheBackgroundBiomeMasks(width, height, waterMask, rockMask);
        }

        private IEnumerator ApplyRulesetToBackgroundRoutine(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            Dictionary<TileBase, TileBase> backgroundLookup)
        {
            if (_background == null || layers == null || layerIndex == null) yield break;
            int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
            int size = width * height;
            bool useVariants = UseGroundTileRandomRotation
                || GroundTileMirrorX
                || GroundTileMirrorY
                || UseGroundTileEdgeColorMatch
                || UseWaterEdgeColorMatch
                || UseGroundTileAntiRepeat;
            EdgeProfile[] placedProfiles = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch) ? new EdgeProfile[size] : null;
            bool[] placedValid = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new bool[size] : null;
            int[] placedVariantIds = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new int[size] : null;
            bool useTransform = UseGroundTileRandomRotation || GroundTileMirrorX || GroundTileMirrorY;
            Matrix4x4 identity = Matrix4x4.identity;
            bool refineEdges = UseWaterEdgeRefinement && UseWaterEdgeColorMatch;
            TileBase[] refineTiles = refineEdges ? new TileBase[size] : null;
            Matrix4x4[] refineTransforms = (refineEdges && useTransform) ? new Matrix4x4[size] : null;

            TileVariant[][] layerVariants = useVariants ? new TileVariant[layers.Count][] : null;
            TileBase[][] layerTiles = useVariants ? null : new TileBase[layers.Count][];
            bool useSharedTiles = UseSharedGroundTiles && SharedGroundTileChance > 0f;
            TileVariant[] sharedVariants = null;
            TileBase[] sharedTiles = null;
            bool useWaterBiome = UseWaterBiome;
            string[] waterExclude = useWaterBiome ? ResolveWaterExcludeKeywords() : null;
            bool[] waterMask = null;
            bool[] rockMask = null;
            TileVariant[] waterVariants = null;
            TileVariant[] rockVariants = null;
            TileBase[] waterTiles = null;
            TileBase[] rockTiles = null;
            TileVariant[] waterInteriorVariants = null;
            TileBase[] waterInteriorTiles = null;
            TileVariant[] landVariants = null;
            TileBase[] landTiles = null;
            HashSet<TileBase> waterSet = null;
            HashSet<TileBase> rockSet = null;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.BaseTiles == null || layer.BaseTiles.Length == 0)
                {
                    if (layerVariants != null) layerVariants[i] = Array.Empty<TileVariant>();
                    if (layerTiles != null) layerTiles[i] = Array.Empty<TileBase>();
                    continue;
                }

                var baseTiles = layer.BaseTiles;
                var mappedTiles = new TileBase[baseTiles.Length];
                for (int t = 0; t < baseTiles.Length; t++)
                    mappedTiles[t] = MapBackgroundTile(baseTiles[t], backgroundLookup);

                if (useVariants)
                    layerVariants[i] = BuildTileVariants(mappedTiles);
                else
                    layerTiles[i] = mappedTiles;
            }

            if (useWaterBiome)
            {
                var biomeCandidates = (AutoTerrainGroupByPrefix && GroundTiles != null && GroundTiles.Length > 0)
                    ? GroundTiles
                    : CollectUniqueLayerTiles(layers);
                waterTiles = ResolveBiomeTilesByName(biomeCandidates, WaterTileNameKeywords);
                if (waterTiles != null && waterTiles.Length > 0)
                    waterTiles = ExcludeTilesByNameOrSprite(waterTiles, waterExclude);
                rockTiles = ResolveBiomeTilesByName(biomeCandidates, RockTileNameKeywords);
                waterInteriorTiles = ResolveBiomeTilesByName(biomeCandidates, WaterInteriorTileNameKeywords);
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                    waterInteriorTiles = ExcludeTilesByNameOrSprite(waterInteriorTiles, waterExclude);
                if (waterTiles != null && waterTiles.Length > 0)
                {
                    for (int t = 0; t < waterTiles.Length; t++)
                        waterTiles[t] = MapBackgroundTile(waterTiles[t], backgroundLookup);
                    waterTiles = ExcludeTilesByNameOrSprite(waterTiles, waterExclude);
                }
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                {
                    for (int t = 0; t < waterInteriorTiles.Length; t++)
                        waterInteriorTiles[t] = MapBackgroundTile(waterInteriorTiles[t], backgroundLookup);
                    waterInteriorTiles = ExcludeTilesByNameOrSprite(waterInteriorTiles, waterExclude);
                }
                if (UseWaterAutoInteriorByColor)
                {
                    var allWaterTiles = CombineTiles(waterTiles, waterInteriorTiles);
                    if (allWaterTiles != null && allWaterTiles.Length > 0)
                    {
                        var autoInterior = new List<TileBase>();
                        for (int t = 0; t < allWaterTiles.Length; t++)
                        {
                            var tile = allWaterTiles[t];
                            if (tile != null && IsWaterInteriorTile(tile))
                                autoInterior.Add(tile);
                        }
                        if (autoInterior.Count == 0 && WaterInteriorFallbackCount > 0)
                        {
                            var scored = new List<TileScore>(allWaterTiles.Length);
                            for (int t = 0; t < allWaterTiles.Length; t++)
                            {
                                var tile = allWaterTiles[t];
                                if (tile == null) continue;
                                scored.Add(new TileScore(tile, GetWaterInteriorScore(tile)));
                            }
                            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
                            int take = Mathf.Clamp(WaterInteriorFallbackCount, 0, scored.Count);
                            for (int i = 0; i < take; i++)
                            {
                                if (scored[i].Tile != null)
                                    autoInterior.Add(scored[i].Tile);
                            }
                        }
                        if (autoInterior.Count > 0)
                            waterInteriorTiles = CombineTiles(waterInteriorTiles, autoInterior.ToArray());
                    }
                }
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0 && waterTiles != null && waterTiles.Length > 0)
                {
                    var interiorSet = new HashSet<TileBase>(waterInteriorTiles);
                    var filtered = new List<TileBase>(waterTiles.Length);
                    for (int t = 0; t < waterTiles.Length; t++)
                    {
                        var tile = waterTiles[t];
                        if (tile != null && !interiorSet.Contains(tile))
                            filtered.Add(tile);
                    }
                    waterTiles = filtered.Count > 0 ? filtered.ToArray() : null;
                }
                if (useVariants)
                {
                    if (waterTiles != null && waterTiles.Length > 0)
                    {
                        waterVariants = BuildTileVariants(waterTiles, UseGroundTileRandomRotation && WaterTilesAllowRotation, (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                        waterVariants = ExcludeVariantsByNameOrSprite(waterVariants, waterExclude);
                    }
                    if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                    {
                        waterInteriorVariants = BuildTileVariants(waterInteriorTiles, UseGroundTileRandomRotation && WaterTilesAllowRotation, (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                        waterInteriorVariants = ExcludeVariantsByNameOrSprite(waterInteriorVariants, waterExclude);
                    }
                }
                if ((waterTiles != null && waterTiles.Length > 0) || (waterInteriorTiles != null && waterInteriorTiles.Length > 0))
                {
                    waterMask = BuildWaterMaskRect(width, height, tileSeed, out _);
                    var waterAllTiles = CombineTiles(waterTiles, waterInteriorTiles);
                    waterSet = BuildTileSet(waterAllTiles);
                    if (waterTiles == null || waterTiles.Length == 0)
                    {
                        waterTiles = waterInteriorTiles;
                        waterVariants = waterInteriorVariants;
                    }
                }
                if (rockTiles != null && rockTiles.Length > 0 && waterMask != null)
                {
                    for (int t = 0; t < rockTiles.Length; t++)
                        rockTiles[t] = MapBackgroundTile(rockTiles[t], backgroundLookup);
                    if (useVariants)
                        rockVariants = BuildTileVariants(rockTiles, UseGroundTileRandomRotation && RockTilesAllowRotation, (GroundTileMirrorX || GroundTileMirrorY) && RockTilesAllowMirroring);
                    rockMask = BuildRockMaskFromWater(width, height, waterMask, tileSeed);
                    rockSet = BuildTileSet(rockTiles);
                }
                if (waterMask == null)
                    useWaterBiome = false;
            }

            if (useWaterBiome)
            {
                var allTiles = CollectUniqueLayerTiles(layers) ?? GroundTiles;
                if (allTiles != null && allTiles.Length > 0)
                {
                    var unique = new HashSet<TileBase>();
                    var list = new List<TileBase>();
                    for (int i = 0; i < allTiles.Length; i++)
                    {
                        var mapped = MapBackgroundTile(allTiles[i], backgroundLookup);
                        if (mapped == null) continue;
                        if (waterSet != null && waterSet.Contains(mapped)) continue;
                        if (rockSet != null && rockSet.Contains(mapped)) continue;
                        if (unique.Add(mapped))
                            list.Add(mapped);
                    }
                    landTiles = list.Count > 0 ? list.ToArray() : null;
                    if (waterExclude != null && waterExclude.Length > 0)
                        landTiles = ExcludeTilesByNameOrSprite(landTiles, waterExclude);
                    if (useVariants && landTiles != null)
                        landVariants = BuildTileVariants(landTiles);
                }
            }

            if (useSharedTiles)
            {
                sharedTiles = ResolveSharedGroundTiles(layers);
                if (sharedTiles == null || sharedTiles.Length == 0)
                {
                    useSharedTiles = false;
                }
                else
                {
                    for (int t = 0; t < sharedTiles.Length; t++)
                    {
                        var tile = sharedTiles[t];
                        if (tile != null && backgroundLookup != null && backgroundLookup.TryGetValue(tile, out var mapped) && mapped != null)
                            sharedTiles[t] = mapped;
                        else if (ConvertGroundTilesRuntime)
                            sharedTiles[t] = GetOrCreateRuntimeGroundTile(tile);
                    }
                    if (useVariants)
                        sharedVariants = BuildTileVariants(sharedTiles);
                }
            }

            for (int row = 0; row < height; row += rowsPerFrame)
            {
                int rowCount = Mathf.Min(rowsPerFrame, height - row);
                int blockSize = width * rowCount;
                var tiles = new TileBase[blockSize];
                Matrix4x4[] transforms = useTransform ? new Matrix4x4[blockSize] : null;

                for (int r = 0; r < rowCount; r++)
                {
                    int rowIdx = row + r;
                    int rowBase = r * width;
                    for (int col = 0; col < width; col++)
                    {
                        int globalIdx = (rowIdx * width) + col;
                        int layerIdx = layerIndex[globalIdx];
                        if (layerIdx < 0 || layerIdx >= layers.Count) continue;
                        if (!useVariants)
                        {
                            bool isWater = useWaterBiome && waterMask != null && waterMask[globalIdx];
                            bool isWaterHole = !isWater && useWaterBiome && IsMaskHole(waterMask, width, height, col, rowIdx);
                            if (isWaterHole) isWater = true;
                            bool isRock = !isWater && rockMask != null && rockMask[globalIdx];
                            bool isWaterInterior = isWater && (isWaterHole || IsMaskInterior(waterMask, width, height, col, rowIdx));
                            bool useSharedNow = !isWater && !isRock && useSharedTiles && sharedTiles != null && sharedTiles.Length > 0
                                && ShouldUseSharedTiles(col, rowIdx, layerIdx, tileSeed, SharedGroundTileChance);
                            TileBase[] palette;
                            if (isWater && waterTiles != null && waterTiles.Length > 0)
                                palette = (isWaterInterior && waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                                    ? waterInteriorTiles
                                    : waterTiles;
                            else if (isRock && rockTiles != null && rockTiles.Length > 0)
                                palette = rockTiles;
                            else
                                palette = useSharedNow ? sharedTiles : layerTiles[layerIdx];
                            if (palette == null || palette.Length == 0) continue;
                            var picked = PickTileDeterministic(palette, col, rowIdx, tileSeed);
                            if (!isWater && !isRock && (waterSet != null || rockSet != null))
                            {
                                bool disallowed = (waterSet != null && waterSet.Contains(picked)) || (rockSet != null && rockSet.Contains(picked));
                            if (disallowed && landTiles != null && landTiles.Length > 0)
                                picked = PickTileDeterministic(landTiles, col, rowIdx, tileSeed);
                        }
                        if (!isWater && !isRock && IsTileExcludedByNameOrSprite(picked, waterExclude))
                        {
                            if (landTiles != null && landTiles.Length > 0)
                                picked = PickTileDeterministic(landTiles, col, rowIdx, tileSeed);
                            else
                                picked = PickTileDeterministicExcluding(palette, col, rowIdx, tileSeed, waterExclude);
                        }
                        tiles[rowBase + col] = picked;
                        continue;
                    }

                        bool isWaterVariants = useWaterBiome && waterMask != null && waterMask[globalIdx];
                        bool isWaterHoleVariants = !isWaterVariants && useWaterBiome && IsMaskHole(waterMask, width, height, col, rowIdx);
                        if (isWaterHoleVariants) isWaterVariants = true;
                        bool isRockVariants = !isWaterVariants && rockMask != null && rockMask[globalIdx];
                        bool useSharedNowVariants = !isWaterVariants && !isRockVariants && useSharedTiles && sharedVariants != null && sharedVariants.Length > 0
                            && ShouldUseSharedTiles(col, rowIdx, layerIdx, tileSeed, SharedGroundTileChance);
                        bool isWaterInteriorVariants = isWaterVariants && (isWaterHoleVariants || IsMaskInterior(waterMask, width, height, col, rowIdx));
                        TileVariant[] variants;
                        if (isWaterVariants && waterVariants != null && waterVariants.Length > 0)
                            variants = (isWaterInteriorVariants && waterInteriorVariants != null && waterInteriorVariants.Length > 0)
                                ? waterInteriorVariants
                                : waterVariants;
                        else if (isRockVariants && rockVariants != null && rockVariants.Length > 0)
                            variants = rockVariants;
                        else
                            variants = useSharedNowVariants ? sharedVariants : layerVariants[layerIdx];
                        if (variants == null || variants.Length == 0)
                            continue;

                        bool requireLeftGreen = false;
                        bool requireBottomGreen = false;
                        int requireLeftWater = 0;
                        int requireBottomWater = 0;
                        int requireRightWater = 0;
                        int requireTopWater = 0;
                        byte leftWaterMask = 0;
                        byte bottomWaterMask = 0;
                        bool hasLeftWaterMask = false;
                        bool hasBottomWaterMask = false;
                        int leftId = int.MinValue;
                        int bottomId = int.MinValue;
                        if (placedValid != null)
                        {
                            if (col > 0)
                            {
                                int leftIdx = globalIdx - 1;
                                if (placedValid[leftIdx])
                                {
                                    if (placedProfiles != null && placedProfiles[leftIdx].RightGreen)
                                        requireLeftGreen = true;
                                    if (placedProfiles != null)
                                    {
                                        hasLeftWaterMask = true;
                                        leftWaterMask = placedProfiles[leftIdx].RightWaterMask;
                                    }
                                    if (placedVariantIds != null)
                                        leftId = placedVariantIds[leftIdx];
                                }
                            }
                            if (rowIdx > 0)
                            {
                                int bottomIdx = globalIdx - width;
                                if (placedValid[bottomIdx])
                                {
                                    if (placedProfiles != null && placedProfiles[bottomIdx].TopGreen)
                                        requireBottomGreen = true;
                                    if (placedProfiles != null)
                                    {
                                        hasBottomWaterMask = true;
                                        bottomWaterMask = placedProfiles[bottomIdx].TopWaterMask;
                                    }
                                    if (placedVariantIds != null)
                                        bottomId = placedVariantIds[bottomIdx];
                                }
                            }
                        }

                        bool enforceEdge = UseGroundTileEdgeColorMatch && !isWaterVariants && !isRockVariants;
                        bool enforceWaterEdge = false;
                        if (UseWaterEdgeColorMatch && waterMask != null)
                        {
                            bool leftWater = col > 0 && IsWaterCell(waterMask, width, height, col - 1, rowIdx);
                            bool bottomWater = rowIdx > 0 && IsWaterCell(waterMask, width, height, col, rowIdx - 1);
                            bool rightWater = col < width - 1 && IsWaterCell(waterMask, width, height, col + 1, rowIdx);
                            bool topWater = rowIdx < height - 1 && IsWaterCell(waterMask, width, height, col, rowIdx + 1);
                            bool hasWaterNeighbor = leftWater || bottomWater || rightWater || topWater;

                            enforceWaterEdge = isWaterVariants || isRockVariants || hasWaterNeighbor;
                            if (enforceWaterEdge)
                            {
                                if (col > 0)
                                    requireLeftWater = leftWater ? 1 : -1;
                                if (rowIdx > 0)
                                    requireBottomWater = bottomWater ? 1 : -1;
                                if (col < width - 1)
                                    requireRightWater = rightWater ? 1 : -1;
                                if (rowIdx < height - 1)
                                    requireTopWater = topWater ? 1 : -1;
                            }
                        }
                        TileVariant variant = PickVariantWithConstraints(
                            variants,
                            requireLeftGreen,
                            requireBottomGreen,
                            requireLeftWater,
                            requireBottomWater,
                            requireRightWater,
                            requireTopWater,
                            enforceEdge,
                            enforceWaterEdge,
                            UseGroundTileAntiRepeat,
                            leftId,
                            bottomId,
                            leftWaterMask,
                            hasLeftWaterMask,
                            bottomWaterMask,
                            hasBottomWaterMask,
                            col,
                            rowIdx,
                            tileSeed);

                        if (variant.Tile == null && useSharedNowVariants)
                        {
                            var fallback = layerVariants[layerIdx];
                            if (fallback != null && fallback.Length > 0)
                            {
                                variant = PickVariantWithConstraints(
                                    fallback,
                                    requireLeftGreen,
                                    requireBottomGreen,
                                    requireLeftWater,
                                    requireBottomWater,
                                    requireRightWater,
                                    requireTopWater,
                                    enforceEdge,
                                    enforceWaterEdge,
                                    UseGroundTileAntiRepeat,
                                    leftId,
                                    bottomId,
                                    leftWaterMask,
                                    hasLeftWaterMask,
                                bottomWaterMask,
                                hasBottomWaterMask,
                                col,
                                rowIdx,
                                tileSeed);
                            }
                        }

                        if (!isWaterVariants && !isRockVariants && IsTileExcludedByNameOrSprite(variant.Tile, waterExclude))
                        {
                            if (landVariants != null && landVariants.Length > 0)
                            {
                                variant = PickVariantWithConstraints(
                                    landVariants,
                                    requireLeftGreen,
                                    requireBottomGreen,
                                    requireLeftWater,
                                    requireBottomWater,
                                    requireRightWater,
                                    requireTopWater,
                                    enforceEdge,
                                    enforceWaterEdge,
                                    UseGroundTileAntiRepeat,
                                    leftId,
                                    bottomId,
                                    leftWaterMask,
                                    hasLeftWaterMask,
                                    bottomWaterMask,
                                    hasBottomWaterMask,
                                    col,
                                    rowIdx,
                                    tileSeed);
                            }
                            else
                            {
                                variant = PickVariantDeterministicExcluding(variants, col, rowIdx, tileSeed, waterExclude);
                            }
                        }

                        tiles[rowBase + col] = variant.Tile;
                        if (transforms != null)
                            transforms[rowBase + col] = variant.Transform;
                        if (refineTiles != null)
                            refineTiles[globalIdx] = variant.Tile;
                        if (refineTransforms != null)
                            refineTransforms[globalIdx] = variant.Transform;
                        if (placedValid != null)
                        {
                            if (placedProfiles != null)
                                placedProfiles[globalIdx] = variant.Profile;
                            placedValid[globalIdx] = variant.Tile != null;
                            if (placedVariantIds != null)
                                placedVariantIds[globalIdx] = variant.Id;
                        }

                        if (!isWaterVariants && !isRockVariants && variant.Tile != null && (waterSet != null || rockSet != null))
                        {
                            bool disallowed = (waterSet != null && waterSet.Contains(variant.Tile)) || (rockSet != null && rockSet.Contains(variant.Tile));
                            if (disallowed && landVariants != null && landVariants.Length > 0)
                            {
                                var landVariant = PickVariantWithConstraints(
                                    landVariants,
                                    requireLeftGreen,
                                    requireBottomGreen,
                                    requireLeftWater,
                                    requireBottomWater,
                                    requireRightWater,
                                    requireTopWater,
                                    enforceEdge,
                                    enforceWaterEdge,
                                    UseGroundTileAntiRepeat,
                                    leftId,
                                    bottomId,
                                    leftWaterMask,
                                    hasLeftWaterMask,
                                    bottomWaterMask,
                                    hasBottomWaterMask,
                                    col,
                                    rowIdx,
                                    tileSeed);
                                tiles[rowBase + col] = landVariant.Tile;
                                if (transforms != null)
                                    transforms[rowBase + col] = landVariant.Transform;
                                if (placedValid != null)
                                {
                                    if (placedProfiles != null)
                                        placedProfiles[globalIdx] = landVariant.Profile;
                                    placedValid[globalIdx] = landVariant.Tile != null;
                                    if (placedVariantIds != null)
                                        placedVariantIds[globalIdx] = landVariant.Id;
                                }
                            }
                        }
                    }
                }

                var bounds = new BoundsInt(0, row, 0, width, rowCount, 1);
                _background.SetTilesBlock(bounds, tiles);
                if (transforms != null)
                {
                    for (int r = 0; r < rowCount; r++)
                    {
                        int rowIdx = row + r;
                        int rowBase = r * width;
                        for (int col = 0; col < width; col++)
                        {
                            int blockIdx = rowBase + col;
                            if (tiles[blockIdx] == null) continue;
                            var transform = transforms[blockIdx];
                            if (transform == default || transform == identity)
                                transform = GetTileTransform(tiles[blockIdx]);
                            _background.SetTransformMatrix(new Vector3Int(col, rowIdx, 0), transform);
                        }
                    }
                }
                yield return null;
            }

            if (refineTiles != null)
            {
                RefineWaterEdgesRect(
                    width,
                    height,
                    waterMask,
                    rockMask,
                    layerIndex,
                    layerVariants,
                    waterVariants,
                    waterInteriorVariants,
                    rockVariants,
                    sharedVariants,
                    tileSeed,
                    refineTiles,
                    refineTransforms,
                    placedProfiles,
                    placedValid,
                    placedVariantIds);

                var bounds = new BoundsInt(0, 0, 0, width, height, 1);
                _background.SetTilesBlock(bounds, refineTiles);
                if (refineTransforms != null)
                {
                    for (int row = 0; row < height; row++)
                    {
                        for (int col = 0; col < width; col++)
                        {
                            int idx = (row * width) + col;
                            if (refineTiles[idx] == null) continue;
                            var transform = refineTransforms[idx];
                            if (transform == default || transform == identity)
                                transform = GetTileTransform(refineTiles[idx]);
                            _background.SetTransformMatrix(new Vector3Int(col, row, 0), transform);
                        }
                    }
                }
            }
            CacheBackgroundBiomeMasks(width, height, waterMask, rockMask);
        }

        private void EnsurePropCollider(Tilemap map)
        {
            if (map == null) return;
            var rb = map.GetComponent<Rigidbody2D>();
            if (rb == null) rb = map.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var collider = map.GetComponent<TilemapCollider2D>();
            if (collider == null) collider = map.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var composite = map.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = map.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        private Texture2D GetReadableTexture(Texture2D source)
        {
            if (source == null) return null;
            if (source.isReadable) return source;
            if (_readableTextureCache.TryGetValue(source, out var cached) && cached != null)
                return cached;

            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
            }
            finally
            {
                if (RenderTexture.active == rt)
                    RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            _readableTextureCache[source] = readable;
            return readable;
        }

        private void ClearRuntimeGroundConversion()
        {
            if (_runtimeGroundObjects.Count == 0)
            {
                _runtimeGroundTileCache.Clear();
                _runtimeGroundSpriteCache.Clear();
                _runtimeGroundConversionHash = 0;
                _edgeProfileCache.Clear();
            _waterInteriorCache.Clear();
            _waterInteriorScoreCache.Clear();
            _loggedDarkEdgeSprites.Clear();
            return;
        }

            for (int i = 0; i < _runtimeGroundObjects.Count; i++)
            {
                var obj = _runtimeGroundObjects[i];
                if (obj == null) continue;
                if (UnityEngine.Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
            _runtimeGroundObjects.Clear();
            _runtimeGroundTileCache.Clear();
            _runtimeGroundSpriteCache.Clear();
            _runtimeGroundConversionHash = 0;
            _edgeProfileCache.Clear();
            _waterInteriorCache.Clear();
            _waterInteriorScoreCache.Clear();
            _loggedDarkEdgeSprites.Clear();
        }

        private void ClearReadableTextureCache()
        {
            if (_readableTextureCache.Count == 0) return;
            foreach (var pair in _readableTextureCache)
            {
                if (pair.Value == null) continue;
                if (UnityEngine.Application.isPlaying)
                    Destroy(pair.Value);
                else
                    DestroyImmediate(pair.Value);
            }
            _readableTextureCache.Clear();
            _edgeProfileCache.Clear();
            _waterInteriorCache.Clear();
            _waterInteriorScoreCache.Clear();
            _loggedDarkEdgeSprites.Clear();
        }

        private void OnDestroy()
        {
            ClearRuntimeGroundConversion();
            ClearReadableTextureCache();
            ClearAutoTerrainRuleset();
            ReleaseFarViewTexture();
            if (_farViewBakeRoutine != null)
            {
                StopCoroutine(_farViewBakeRoutine);
                _farViewBakeRoutine = null;
            }
        }

        private Vector3 ResolveCellSize()
        {
            if (CellSizeOverride != Vector3.zero)
                return CellSizeOverride;
            if (_hex == null) return Vector3.one;
            float size = Mathf.Max(0.001f, _hex.HexSize);
            if (CellLayout == GridLayout.CellLayout.Hexagon)
                return new Vector3(Mathf.Sqrt(3f) * size, 2f * size, 0f);
            return new Vector3(size, size, 0f);
        }

        private int ResolveObstacleLayer()
        {
            int layer = LayerMask.NameToLayer(ObstacleLayerName);
            if (layer < 0 && _hex != null)
                layer = FirstLayerFromMask(_hex.ObstacleMask);
            if (layer < 0) layer = 0;
            return layer;
        }

        private void ApplyObstacleLayer(Tilemap map)
        {
            if (map == null) return;
            int layer = ResolveObstacleLayer();
            if (layer >= 0)
                map.gameObject.layer = layer;
            if (_hex != null && layer >= 0)
            {
                var m = _hex.ObstacleMask;
                m.value |= (1 << layer);
                _hex.ObstacleMask = m;
            }
        }

        private static TileBase PickTile(System.Random rng, TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            return palette[rng.Next(palette.Length)];
        }

        private TileBase PickGroundTile(int col, int row, System.Random rng, TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            if (!UseGroundNoise || GroundNoiseScale <= 0f)
                return PickTile(rng, palette);

            float scale = Mathf.Max(0.0001f, GroundNoiseScale);
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            const float sqrt3Over2 = 0.8660254f;
            float wx = q + (r * 0.5f);
            float wy = r * sqrt3Over2;
            float nx = (wx + _groundNoiseOffset.x) * scale;
            float ny = (wy + _groundNoiseOffset.y) * scale;
            float v = Mathf.Clamp01(Mathf.PerlinNoise(nx, ny));

            if (GroundGroupSize > 0)
            {
                int groupSize = Mathf.Max(1, GroundGroupSize);
                int groupCount = (palette.Length + groupSize - 1) / groupSize;
                int groupIndex = Mathf.Clamp(Mathf.FloorToInt(v * groupCount), 0, groupCount - 1);
                int groupStart = groupIndex * groupSize;
                int groupEnd = Mathf.Min(groupStart + groupSize, palette.Length);
                return palette[rng.Next(groupStart, groupEnd)];
            }

            int idx = Mathf.Clamp(Mathf.FloorToInt(v * palette.Length), 0, palette.Length - 1);
            return palette[idx];
        }

        private static bool SortingLayerExists(string name)
        {
            foreach (var l in SortingLayer.layers)
            {
                if (l.name == name) return true;
            }
            return false;
        }

        private static int FirstLayerFromMask(LayerMask mask)
        {
            int m = mask.value;
            if (m == 0) return -1;
            for (int i = 0; i < 32; i++)
            {
                if (((m >> i) & 1) != 0) return i;
            }
            return -1;
        }

        private void BakeBlockingIfNeeded(int blockingTargetCount, int propTargetCount, BlockBounds bounds)
        {
            if (UseDirectWalkableUpdates) return;
            bool hasBlockingFromProps = PropsBlockMovement && propTargetCount > 0 && _props != null;
            if (hasBlockingFromProps)
            {
                EnsurePropCollider(_props);
                int layer = ResolveObstacleLayer();
                if (layer >= 0)
                    _props.gameObject.layer = layer;
                if (_hex != null)
                {
                    var m = _hex.ObstacleMask;
                    if (layer >= 0) m.value |= (1 << layer);
                    _hex.ObstacleMask = m;
                }
            }

            bool hasBlockingFromBlockers = BlockingPropsBlockMovement && blockingTargetCount > 0 && _blockers != null;
            if (hasBlockingFromBlockers)
            {
                EnsurePropCollider(_blockers);
                int layer = ResolveObstacleLayer();
                if (layer >= 0)
                    _blockers.gameObject.layer = layer;
                if (_hex != null)
                {
                    var m = _hex.ObstacleMask;
                    if (layer >= 0) m.value |= (1 << layer);
                    _hex.ObstacleMask = m;
                }
            }

            if ((hasBlockingFromProps || hasBlockingFromBlockers) && _hex != null)
            {
                if (bounds != null && bounds.HasAny)
                    _hex.BakeFromPhysicsRectCells(bounds.MinCol, bounds.MinRow, bounds.MaxCol, bounds.MaxRow, paddingCells: 1);
                else
                    _hex.BakeFromPhysics();
            }
        }

        private void ResolvePalettes(out TileBase[] groundPalette, out TileBase[] propPalette, out TileBase[] blockingPalette)
        {
            groundPalette = GroundTiles;
            propPalette = PropTiles;
            blockingPalette = BlockingPropTiles;
            if (AutoSplitGroundByName && GroundTiles != null && GroundTiles.Length > 0
                && PropNameKeywords != null && PropNameKeywords.Length > 0)
            {
                var grounds = new List<TileBase>(GroundTiles.Length);
                var splitProps = new List<TileBase>((PropTiles?.Length ?? 0) + GroundTiles.Length);
                if (PropTiles != null && PropTiles.Length > 0)
                    splitProps.AddRange(PropTiles);

                for (int i = 0; i < GroundTiles.Length; i++)
                {
                    var tile = GroundTiles[i];
                    if (tile == null) continue;
                    if (IsNameMatch(tile.name, PropNameKeywords))
                        splitProps.Add(tile);
                    else
                        grounds.Add(tile);
                }

                groundPalette = grounds.ToArray();
                propPalette = splitProps.ToArray();
            }

            if (UseGroundSuffixFilter && groundPalette != null && groundPalette.Length > 0 && !string.IsNullOrEmpty(GroundSuffixFilter))
            {
                var filtered = new List<TileBase>(groundPalette.Length);
                for (int i = 0; i < groundPalette.Length; i++)
                {
                    var tile = groundPalette[i];
                    if (tile == null) continue;
                    if (tile.name.EndsWith(GroundSuffixFilter, StringComparison.OrdinalIgnoreCase))
                        filtered.Add(tile);
                }
                if (filtered.Count > 0)
                    groundPalette = filtered.ToArray();
            }

            if (AutoSplitBlockingByName
                && propPalette != null && propPalette.Length > 0
                && BlockingNameKeywords != null && BlockingNameKeywords.Length > 0)
            {
                var props = new List<TileBase>(propPalette.Length);
                var blockers = new List<TileBase>(propPalette.Length / 2);
                for (int i = 0; i < propPalette.Length; i++)
                {
                    var tile = propPalette[i];
                    if (tile == null) continue;
                    if (IsNameMatch(tile.name, BlockingNameKeywords))
                        blockers.Add(tile);
                    else
                        props.Add(tile);
                }

                if (BlockingPropTiles != null && BlockingPropTiles.Length > 0)
                    blockers.AddRange(BlockingPropTiles);

                propPalette = props.ToArray();
                blockingPalette = blockers.ToArray();
            }

            propPalette = ApplyPropWeighting(propPalette);

            if (ConvertGroundTilesRuntime)
                groundPalette = ConvertGroundPalette(groundPalette);
        }

        private static bool IsNameMatch(string tileName, string[] keywords)
        {
            if (string.IsNullOrEmpty(tileName) || keywords == null || keywords.Length == 0) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                var kw = keywords[i];
                if (string.IsNullOrEmpty(kw)) continue;
                if (tileName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private TileBase[] ApplyPropWeighting(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            if (PropNoBoostKeywords == null || PropNoBoostKeywords.Length == 0) return palette;

            const int baseWeight = 4;
            const int boostedWeight = 9; // stronger bias for non-A12 props
            var weighted = new List<TileBase>(palette.Length * boostedWeight);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile == null) continue;
                int weight = IsNameMatch(tile.name, PropNoBoostKeywords) ? baseWeight : boostedWeight;
                for (int w = 0; w < weight; w++)
                    weighted.Add(tile);
            }
            return weighted.Count > 0 ? weighted.ToArray() : palette;
        }

        private TileBase[] ApplyTreeWeighting(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            bool hasRare = TreeRareKeywords != null && TreeRareKeywords.Length > 0;
            bool hasVeryRare = TreeVeryRareKeywords != null && TreeVeryRareKeywords.Length > 0;
            bool hasAccent = TreeAccentExcludeFromBase
                && TreeAccentKeywords != null
                && TreeAccentKeywords.Length > 0;
            if (!hasRare && !hasVeryRare && !hasAccent) return palette;

            float rareWeight = Mathf.Clamp(TreeRareWeight, 0.05f, 1f);
            float veryRareWeight = Mathf.Clamp(TreeVeryRareWeight, 0.02f, 1f);
            if (rareWeight >= 0.99f && veryRareWeight >= 0.99f) return palette;

            const int baseWeight = 10;
            int rareCount = Mathf.Max(1, Mathf.RoundToInt(baseWeight * rareWeight));
            int veryRareCount = Mathf.Max(1, Mathf.RoundToInt(baseWeight * veryRareWeight));
            var weighted = new List<TileBase>(palette.Length * baseWeight);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile == null) continue;
                if (hasAccent && IsNameMatch(tile.name, TreeAccentKeywords))
                    continue;
                int weight = baseWeight;
                if (hasVeryRare && IsNameMatch(tile.name, TreeVeryRareKeywords))
                    weight = veryRareCount;
                else if (hasRare && IsNameMatch(tile.name, TreeRareKeywords))
                    weight = rareCount;
                for (int w = 0; w < weight; w++)
                    weighted.Add(tile);
            }
            return weighted.Count > 0 ? weighted.ToArray() : palette;
        }

        private TileBase[] ResolveTreeAccentTiles()
        {
            if (TreeAccentTiles != null && TreeAccentTiles.Length > 0)
                return TreeAccentTiles;
            if (TreeTiles == null || TreeTiles.Length == 0) return null;
            if (TreeAccentKeywords == null || TreeAccentKeywords.Length == 0) return null;
            var list = new List<TileBase>();
            for (int i = 0; i < TreeTiles.Length; i++)
            {
                var tile = TreeTiles[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, TreeAccentKeywords))
                    list.Add(tile);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private TileBase[] BuildPropBoostPalette(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            if (PropNoBoostKeywords == null || PropNoBoostKeywords.Length == 0) return palette;
            var filtered = new List<TileBase>(palette.Length);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, PropNoBoostKeywords)) continue;
                filtered.Add(tile);
            }
            return filtered.Count > 0 ? filtered.ToArray() : palette;
        }

        private static bool IsMaskInterior(bool[] mask, int width, int height, int col, int row)
        {
            if (mask == null) return false;
            if (col <= 0 || row <= 0 || col >= width - 1 || row >= height - 1) return false;
            int idx = (row * width) + col;
            if (!mask[idx]) return false;
            return mask[idx - 1] && mask[idx + 1] && mask[idx - width] && mask[idx + width];
        }

        private static bool IsMaskHole(bool[] mask, int width, int height, int col, int row)
        {
            if (mask == null) return false;
            if (col <= 0 || row <= 0 || col >= width - 1 || row >= height - 1) return false;
            int idx = (row * width) + col;
            if (mask[idx]) return false;
            return mask[idx - 1] && mask[idx + 1] && mask[idx - width] && mask[idx + width];
        }

        private static bool IsWaterCell(bool[] waterMask, int width, int height, int col, int row)
        {
            if (waterMask == null) return false;
            if (col < 0 || row < 0 || col >= width || row >= height) return false;
            int idx = (row * width) + col;
            if (waterMask[idx]) return true;
            return IsMaskHole(waterMask, width, height, col, row);
        }

        private readonly struct TileScore
        {
            public readonly TileBase Tile;
            public readonly float Score;

            public TileScore(TileBase tile, float score)
            {
                Tile = tile;
                Score = score;
            }
        }

        private static int CountMaskNeighbors4(bool[] mask, int width, int height, int col, int row)
        {
            if (mask == null) return 0;
            int idx = (row * width) + col;
            int count = 0;
            if (col > 0 && mask[idx - 1]) count++;
            if (col < width - 1 && mask[idx + 1]) count++;
            if (row > 0 && mask[idx - width]) count++;
            if (row < height - 1 && mask[idx + width]) count++;
            return count;
        }

        private static int RemoveIsolatedMaskCells(bool[] mask, int width, int height)
        {
            if (mask == null) return 0;
            int size = width * height;
            var original = new bool[size];
            Array.Copy(mask, original, size);
            int removed = 0;
            for (int row = 0; row < height; row++)
            {
                int rowBase = row * width;
                for (int col = 0; col < width; col++)
                {
                    int idx = rowBase + col;
                    if (!original[idx]) continue;
                    if (CountMaskNeighbors4(original, width, height, col, row) == 0)
                    {
                        mask[idx] = false;
                        removed++;
                    }
                }
            }
            return removed;
        }

        private bool[] SmoothMask(bool[] mask, int width, int height)
        {
            if (!UseWaterMaskSmoothing || mask == null) return mask;
            int passes = Mathf.Max(0, WaterMaskSmoothPasses);
            if (passes == 0) return mask;
            int size = width * height;
            bool[] src = mask;
            bool[] dst = new bool[size];
            var offsets = WaterMaskSmoothIncludeDiagonals ? RectNeighborOffsets8 : RectNeighborOffsets4;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        int idx = rowBase + col;
                        int count = 0;
                        for (int i = 0; i < offsets.Length; i++)
                        {
                            int nc = col + offsets[i].x;
                            int nr = row + offsets[i].y;
                            if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                            int nIdx = (nr * width) + nc;
                            if (src[nIdx]) count++;
                        }

                        if (src[idx])
                            dst[idx] = count >= WaterMaskSmoothStayNeighbors;
                        else
                            dst[idx] = count >= WaterMaskSmoothFillNeighbors;
                    }
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, mask))
                Array.Copy(src, mask, size);
            return mask;
        }

        private static Color[] UnrotatePackedPixels(Color[] pixels, int width, int height, SpritePackingRotation rotation, out int outW, out int outH)
        {
            outW = width;
            outH = height;
            if (pixels == null || pixels.Length == 0) return pixels;
            if (rotation == SpritePackingRotation.None) return pixels;

            if (rotation == SpritePackingRotation.Rotate180)
            {
                var rotated = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    int dstRow = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++)
                    {
                        rotated[dstRow + (width - 1 - x)] = pixels[row + x];
                    }
                }
                return rotated;
            }

            if (rotation == SpritePackingRotation.FlipHorizontal)
            {
                var flipped = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        flipped[row + (width - 1 - x)] = pixels[row + x];
                    }
                }
                return flipped;
            }

            if (rotation == SpritePackingRotation.FlipVertical)
            {
                var flipped = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    int dstRow = (height - 1 - y) * width;
                    Array.Copy(pixels, row, flipped, dstRow, width);
                }
                return flipped;
            }

            // Unity versions differ on naming for 90-degree rotation; treat any unhandled rotation as 90.
            string rotationName = rotation.ToString();
            bool rotateCw = rotationName.IndexOf("CW", StringComparison.OrdinalIgnoreCase) >= 0;
            bool rotateCcw = rotationName.IndexOf("CCW", StringComparison.OrdinalIgnoreCase) >= 0;
            outW = height;
            outH = width;
            var rotated90 = new Color[outW * outH];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int dstX;
                    int dstY;
                    if (rotateCcw)
                    {
                        dstX = (height - 1 - y);
                        dstY = x;
                    }
                    else
                    {
                        // Default to clockwise for Rotate90 (or unknown) cases.
                        dstX = y;
                        dstY = (width - 1 - x);
                    }
                    rotated90[(dstY * outW) + dstX] = pixels[row + x];
                }
            }
            return rotated90;

            return pixels;
        }

        private static Sprite ExtractTileSprite(TileBase tile)
        {
            if (tile == null) return null;
            if (tile is Tile t) return t.sprite;
            return null;
        }

        private static Matrix4x4 GetTileTransform(TileBase tile)
        {
            if (tile is Tile t)
                return t.transform;
            return Matrix4x4.identity;
        }

        private static Vector2 InsetCorner(Vector2 corner, Vector2 center, float inset)
        {
            var dir = corner - center;
            float len = dir.magnitude;
            if (len <= 0.0001f) return corner;
            float shrink = Mathf.Min(inset, len * 0.5f);
            return corner - (dir / len) * shrink;
        }

        private static bool[] BuildDiamondMask(int width, int height, Vector2 top, Vector2 right, Vector2 bottom, Vector2 left)
        {
            var mask = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                float py = y + 0.5f;
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    mask[(y * width) + x] = IsPointInQuad(new Vector2(px, py), top, right, bottom, left);
                }
            }
            return mask;
        }

        private static bool IsPointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float ab = Cross(b - a, p - a);
            float bc = Cross(c - b, p - b);
            float cd = Cross(d - c, p - c);
            float da = Cross(a - d, p - d);
            bool hasNeg = (ab < 0f) || (bc < 0f) || (cd < 0f) || (da < 0f);
            bool hasPos = (ab > 0f) || (bc > 0f) || (cd > 0f) || (da > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static void DrawLine(Color[] pixels, int width, int height, Vector2 from, Vector2 to, Color color, int thickness)
        {
            int x0 = Mathf.RoundToInt(from.x);
            int y0 = Mathf.RoundToInt(from.y);
            int x1 = Mathf.RoundToInt(to.x);
            int y1 = Mathf.RoundToInt(to.y);
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(pixels, width, height, x0, y0, color, thickness);
                if (x0 == x1 && y0 == y1) break;
                int e2 = err * 2;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void Plot(Color[] pixels, int width, int height, int x, int y, Color color, int thickness)
        {
            if (pixels == null) return;
            int radius = Mathf.Max(0, thickness - 1);
            for (int oy = -radius; oy <= radius; oy++)
            {
                int py = y + oy;
                if (py < 0 || py >= height) continue;
                int row = py * width;
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int px = x + ox;
                    if (px < 0 || px >= width) continue;
                    pixels[row + px] = color;
                }
            }
        }

        private static bool TryFindNearestOpaque(
            int x,
            int y,
            int width,
            int height,
            bool[] mask,
            Color[] pixels,
            float threshold,
            int radius,
            out int insideX,
            out int insideY,
            float darkThreshold = -1f,
            float chromaThreshold = -1f)
        {
            insideX = x;
            insideY = y;
            if (pixels == null) return false;
            int best = int.MaxValue;
            int r = Mathf.Max(1, radius);
            bool requireNonDark = darkThreshold >= 0f;
            for (int oy = -r; oy <= r; oy++)
            {
                int py = y + oy;
                if (py < 0 || py >= height) continue;
                int row = py * width;
                for (int ox = -r; ox <= r; ox++)
                {
                    int px = x + ox;
                    if (px < 0 || px >= width) continue;
                    int idx = row + px;
                    if (mask != null && !mask[idx]) continue;
                    if (pixels[idx].a <= threshold) continue;
                    if (requireNonDark && IsEdgeDark(pixels[idx], darkThreshold, chromaThreshold)) continue;
                    int dist = (ox * ox) + (oy * oy);
                    if (dist >= best) continue;
                    best = dist;
                    insideX = px;
                    insideY = py;
                    if (best == 0) return true;
                }
            }
            return best != int.MaxValue;
        }

        private static bool IsDark(Color c, float threshold)
        {
            float luma = (c.r * 0.2126f) + (c.g * 0.7152f) + (c.b * 0.0722f);
            return luma <= threshold;
        }

        private static bool IsEdgeDark(Color c, float lumaThreshold, float chromaThreshold)
        {
            float luma = (c.r * 0.2126f) + (c.g * 0.7152f) + (c.b * 0.0722f);
            if (luma > lumaThreshold) return false;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float chroma = max - min;
            if (chromaThreshold < 0f) return true;
            return chroma <= chromaThreshold;
        }

        private bool HasDarkEdgePixels(Color[] pixels, int width, int height, out float ratio)
        {
            ratio = 0f;
            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0) return false;
            int edge = Mathf.Max(1, GroundTileEdgeTrimPixels + 1);
            int total = 0;
            int dark = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            float lumaThreshold = Mathf.Clamp01(GroundTileEdgeBlackThreshold);
            float chromaThreshold = Mathf.Clamp01(GroundTileEdgeChromaThreshold);

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                bool yEdge = y < edge || y >= height - edge;
                for (int x = 0; x < width; x++)
                {
                    if (!yEdge && x >= edge && x < width - edge) continue;
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsEdgeDark(c, lumaThreshold, chromaThreshold))
                        dark++;
                }
            }

            if (total == 0) return false;
            ratio = dark / (float)total;
            return ratio >= GroundTileDebugDarkEdgeRatio;
        }

        private static bool TryFindDiamondCorners(
            int startX,
            int startY,
            int width,
            int height,
            Color[] pixels,
            int stride,
            float threshold,
            out Vector2 top,
            out Vector2 right,
            out Vector2 bottom,
            out Vector2 left)
        {
            top = right = bottom = left = Vector2.zero;
            int topRow = -1;
            int bottomRow = -1;
            float topX = 0f;
            float bottomX = 0f;
            for (int y = startY; y < startY + height; y++)
            {
                int rowStart = y * stride;
                int minPx = int.MaxValue;
                int maxPx = int.MinValue;
                for (int x = startX; x < startX + width; x++)
                {
                    if (pixels[rowStart + x].a <= threshold) continue;
                    if (x < minPx) minPx = x;
                    if (x > maxPx) maxPx = x;
                }
                if (maxPx < minPx) continue;
                if (topRow < 0)
                {
                    topRow = y;
                    topX = (minPx + maxPx) * 0.5f;
                }
                bottomRow = y;
                bottomX = (minPx + maxPx) * 0.5f;
            }

            if (topRow < 0 || bottomRow < 0) return false;

            int leftCol = -1;
            int rightCol = -1;
            float leftY = 0f;
            float rightY = 0f;
            for (int x = startX; x < startX + width; x++)
            {
                int minPy = int.MaxValue;
                int maxPy = int.MinValue;
                for (int y = startY; y < startY + height; y++)
                {
                    if (pixels[(y * stride) + x].a <= threshold) continue;
                    if (y < minPy) minPy = y;
                    if (y > maxPy) maxPy = y;
                }
                if (maxPy < minPy) continue;
                if (leftCol < 0)
                {
                    leftCol = x;
                    leftY = (minPy + maxPy) * 0.5f;
                }
                rightCol = x;
                rightY = (minPy + maxPy) * 0.5f;
            }

            if (leftCol < 0 || rightCol < 0) return false;

            top = new Vector2(topX, topRow);
            bottom = new Vector2(bottomX, bottomRow);
            left = new Vector2(leftCol, leftY);
            right = new Vector2(rightCol, rightY);
            return true;
        }

        private TileBase[] ConvertGroundPalette(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            int hash = ComputeGroundConversionHash(palette);
            if (_runtimeGroundConversionHash != hash)
            {
                ClearRuntimeGroundConversion();
                _runtimeGroundConversionHash = hash;
            }

            var converted = new TileBase[palette.Length];
            for (int i = 0; i < palette.Length; i++)
                converted[i] = GetOrCreateRuntimeGroundTile(palette[i]);
            return converted;
        }

        private IEnumerator ConvertGroundPaletteRoutine(TileBase[] palette, Action<TileBase[]> onDone)
        {
            if (palette == null || palette.Length == 0)
            {
                onDone?.Invoke(palette);
                yield break;
            }

            int hash = ComputeGroundConversionHash(palette);
            if (_runtimeGroundConversionHash != hash)
            {
                ClearRuntimeGroundConversion();
                _runtimeGroundConversionHash = hash;
            }

            var converted = new TileBase[palette.Length];
            int batch = Mathf.Max(1, GroundTileConversionBatchSize);
            for (int i = 0; i < palette.Length; i++)
            {
                converted[i] = GetOrCreateRuntimeGroundTile(palette[i]);
                if ((i + 1) % batch == 0)
                    yield return null;
            }

            onDone?.Invoke(converted);
        }
        private int ComputeGroundConversionHash(TileBase[] palette)
        {
            unchecked
            {
                int h = 17;
                h = (h * 23) + (ConvertGroundTilesRuntime ? 1 : 0);
                h = (h * 23) + (UseGroundTileUnskew ? 1 : 0);
                h = (h * 23) + (UseGroundTileManualDiamond ? 1 : 0);
                h = (h * 23) + (GroundTileDiamondNormalized ? 1 : 0);
                h = (h * 23) + (GroundTileDiamondYFromTop ? 1 : 0);
                h = (h * 23) + GroundTileDiamondInsetPixels.GetHashCode();
                h = (h * 23) + (GroundTileMaskOutsideDiamond ? 1 : 0);
                h = (h * 23) + GroundTileEdgeDilatePixels;
                h = (h * 23) + GroundTileEdgeTrimPixels;
                h = (h * 23) + GroundTileEdgeBlackThreshold.GetHashCode();
                h = (h * 23) + GroundTileEdgeChromaThreshold.GetHashCode();
                h = (h * 23) + (GroundTilePreservePattern ? 1 : 0);
                h = (h * 23) + (GroundTileNoTransform ? 1 : 0);
                h = (h * 23) + (GroundTileDebugOutlineOnly ? 1 : 0);
                h = (h * 23) + GroundTileDebugOutlineColor.GetHashCode();
                h = (h * 23) + GroundTileDebugOutlineThickness;
                h = (h * 23) + GroundTileDiamondTop.GetHashCode();
                h = (h * 23) + GroundTileDiamondRight.GetHashCode();
                h = (h * 23) + GroundTileDiamondBottom.GetHashCode();
                h = (h * 23) + GroundTileDiamondLeft.GetHashCode();
                h = (h * 23) + (UseGroundTileAutoCrop ? 1 : 0);
                h = (h * 23) + GroundTileAutoCropPadding;
                h = (h * 23) + GroundTileCropMin.GetHashCode();
                h = (h * 23) + GroundTileCropMax.GetHashCode();
                h = (h * 23) + GroundTileIsoRatio.GetHashCode();
                h = (h * 23) + GroundTileCenterYOffset.GetHashCode();
                h = (h * 23) + GroundTileResolutionScale.GetHashCode();
                h = (h * 23) + GroundTileAlphaThreshold.GetHashCode();
                h = (h * 23) + (GroundTileFillTransparent ? 1 : 0);
                h = (h * 23) + (int)GroundTileFilterMode;
                if (GroundTileOverrides != null)
                {
                    h = (h * 23) + GroundTileOverrides.Length;
                    for (int i = 0; i < GroundTileOverrides.Length; i++)
                    {
                        var ov = GroundTileOverrides[i];
                        if (ov == null) continue;
                        string name = ov.NameContains ?? string.Empty;
                        h = (h * 23) + StringComparer.OrdinalIgnoreCase.GetHashCode(name);
                        h = (h * 23) + ov.ExtraInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraEdgeTrimPixels;
                        h = (h * 23) + (ov.OverrideEdgeBlackThreshold ? 1 : 0);
                        h = (h * 23) + ov.EdgeBlackThresholdOverride.GetHashCode();
                        h = (h * 23) + (ov.OverrideEdgeChromaThreshold ? 1 : 0);
                        h = (h * 23) + ov.EdgeChromaThresholdOverride.GetHashCode();
                        h = (h * 23) + ov.ExtraTopInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraRightInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraBottomInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraLeftInsetPixels.GetHashCode();
                    }
                }
                h = (h * 23) + palette.Length;
                for (int i = 0; i < palette.Length; i++)
                {
                    var tile = palette[i];
                    h = (h * 23) + (tile == null ? 0 : tile.GetInstanceID());
                }
                return h;
            }
        }

        private TileBase GetOrCreateRuntimeGroundTile(TileBase source)
        {
            if (source == null) return null;
            if (_runtimeGroundTileCache.TryGetValue(source, out var cached) && cached != null)
                return cached;

            var sprite = ExtractTileSprite(source);
            if (sprite == null)
            {
                _runtimeGroundTileCache[source] = source;
                return source;
            }

            if (!TryCreateSquareSprite(sprite, out var convertedSprite))
            {
                _runtimeGroundTileCache[source] = source;
                return source;
            }

            var runtimeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTile.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            runtimeTile.name = source.name;
            runtimeTile.sprite = convertedSprite;
            if (source is Tile srcTile)
            {
                runtimeTile.color = srcTile.color;
                runtimeTile.transform = srcTile.transform;
                runtimeTile.flags = srcTile.flags;
                runtimeTile.colliderType = srcTile.colliderType;
            }

            _runtimeGroundObjects.Add(runtimeTile);
            _runtimeGroundTileCache[source] = runtimeTile;
            return runtimeTile;
        }

        private float ResolveGroundTargetWorldWidth(float fallbackWorldWidth)
        {
            if (!GroundTileUseGridCellWidth) return fallbackWorldWidth;
            float cellWidth = _grid != null ? _grid.cellSize.x : ResolveCellSize().x;
            float scale = Mathf.Max(0.01f, GroundTileWorldScaleMultiplier);
            float result = cellWidth * scale;
            return result > 0.0001f ? result : fallbackWorldWidth;
        }

        private bool TryCreateSquareSprite(Sprite sprite, out Sprite result)
        {
            result = null;
            if (sprite == null) return false;
            if (_runtimeGroundSpriteCache.TryGetValue(sprite, out var cached) && cached != null)
            {
                result = cached;
                return true;
            }

            var sourceTexture = sprite.texture;
            if (sourceTexture == null) return false;
            var readable = GetReadableTexture(sourceTexture);
            if (readable == null) return false;

            float insetPixels = GroundTileDiamondInsetPixels;
            int edgeTrimPixels = GroundTileEdgeTrimPixels;
            float edgeBlackThreshold = GroundTileEdgeBlackThreshold;
            float edgeChromaThreshold = GroundTileEdgeChromaThreshold;
            float extraTopInsetPixels = 0f;
            float extraRightInsetPixels = 0f;
            float extraBottomInsetPixels = 0f;
            float extraLeftInsetPixels = 0f;
            ApplyGroundTileOverrides(
                sprite.name,
                ref insetPixels,
                ref edgeTrimPixels,
                ref edgeBlackThreshold,
                ref edgeChromaThreshold,
                ref extraTopInsetPixels,
                ref extraRightInsetPixels,
                ref extraBottomInsetPixels,
                ref extraLeftInsetPixels);

            bool useManualDiamond = UseGroundTileManualDiamond;
            bool debugOutline = useManualDiamond && GroundTileDebugOutlineOnly;
            bool useUnskew = UseGroundTileUnskew || useManualDiamond;
            var rect = sprite.textureRect;
            var cropRect = rect;
            float threshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            if (!debugOutline && !useManualDiamond && UseGroundTileAutoCrop)
            {
                int fullW = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                int fullH = Mathf.Max(1, Mathf.RoundToInt(rect.height));
                var fullPixels = readable.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), fullW, fullH);
                int minPx = fullW;
                int minPy = fullH;
                int maxPx = -1;
                int maxPy = -1;
                for (int y = 0; y < fullH; y++)
                {
                    int row = y * fullW;
                    for (int x = 0; x < fullW; x++)
                    {
                        if (fullPixels[row + x].a <= threshold) continue;
                        if (x < minPx) minPx = x;
                        if (y < minPy) minPy = y;
                        if (x > maxPx) maxPx = x;
                        if (y > maxPy) maxPy = y;
                    }
                }

                if (maxPx >= minPx && maxPy >= minPy)
                {
                    int pad = Mathf.Max(0, GroundTileAutoCropPadding);
                    minPx = Mathf.Max(0, minPx - pad);
                    minPy = Mathf.Max(0, minPy - pad);
                    maxPx = Mathf.Min(fullW - 1, maxPx + pad);
                    maxPy = Mathf.Min(fullH - 1, maxPy + pad);
                    cropRect = new Rect(rect.x + minPx, rect.y + minPy, (maxPx - minPx + 1), (maxPy - minPy + 1));
                }
            }

            if (!debugOutline && !useManualDiamond)
            {
                float minX = Mathf.Clamp01(GroundTileCropMin.x);
                float minY = Mathf.Clamp01(GroundTileCropMin.y);
                float maxX = Mathf.Clamp01(GroundTileCropMax.x);
                float maxY = Mathf.Clamp01(GroundTileCropMax.y);
                if (maxX <= minX) maxX = Mathf.Min(1f, minX + 0.01f);
                if (maxY <= minY) maxY = Mathf.Min(1f, minY + 0.01f);
                cropRect = new Rect(
                    cropRect.x + (cropRect.width * minX),
                    cropRect.y + (cropRect.height * minY),
                    cropRect.width * (maxX - minX),
                    cropRect.height * (maxY - minY));
            }

            int cropW = Mathf.Max(1, Mathf.RoundToInt(cropRect.width));
            int cropH = Mathf.Max(1, Mathf.RoundToInt(cropRect.height));
            var srcPixels = readable.GetPixels(Mathf.RoundToInt(cropRect.x), Mathf.RoundToInt(cropRect.y), cropW, cropH);
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
            {
                srcPixels = UnrotatePackedPixels(srcPixels, cropW, cropH, sprite.packingRotation, out cropW, out cropH);
                cropRect = new Rect(0f, 0f, cropW, cropH);
            }
            if (useManualDiamond)
            {
                int fullW = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width));
                int fullH = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height));
                int offX = Mathf.RoundToInt(sprite.textureRectOffset.x);
                int offY = Mathf.RoundToInt(sprite.textureRectOffset.y);
                if (fullW != cropW || fullH != cropH || offX != 0 || offY != 0)
                {
                    var fullPixels = new Color[fullW * fullH];
                    int srcStartX = 0;
                    int srcStartY = 0;
                    int dstStartX = offX;
                    int dstStartY = offY;
                    if (dstStartX < 0)
                    {
                        srcStartX = -dstStartX;
                        dstStartX = 0;
                    }
                    if (dstStartY < 0)
                    {
                        srcStartY = -dstStartY;
                        dstStartY = 0;
                    }
                    int copyW = Mathf.Min(cropW - srcStartX, fullW - dstStartX);
                    int copyH = Mathf.Min(cropH - srcStartY, fullH - dstStartY);
                    if (copyW > 0 && copyH > 0)
                    {
                        for (int y = 0; y < copyH; y++)
                        {
                            int srcRow = (srcStartY + y) * cropW;
                            int dstRow = (dstStartY + y) * fullW;
                            Array.Copy(srcPixels, srcRow + srcStartX, fullPixels, dstRow + dstStartX, copyW);
                        }
                    }
                    srcPixels = fullPixels;
                    cropW = fullW;
                    cropH = fullH;
                    cropRect = new Rect(0f, 0f, fullW, fullH);
                }
            }

            float ratio = Mathf.Max(0.01f, GroundTileIsoRatio);
            float resolutionScale = Mathf.Max(0.01f, GroundTileResolutionScale);
            int faceX = 0;
            int faceY = 0;
            int faceW = cropW;
            int faceH = cropH;
            int diamondBoundsX = 0;
            int diamondBoundsY = 0;
            int diamondBoundsW = cropW;
            int diamondBoundsH = cropH;
            if (UseGroundTileUnskew && !useManualDiamond)
            {
                int topRow = -1;
                int maxWidth = 0;
                int rowAtMax = -1;
                int rowMinX = 0;
                int rowMaxX = 0;
                int peakRow = -1;
                int peakWidth = 0;
                int peakMinX = 0;
                int peakMaxX = 0;
                int prevWidth = -1;
                int prevMinX = 0;
                int prevMaxX = 0;
                bool sawIncrease = false;
                for (int y = 0; y < cropH; y++)
                {
                    int rowStart = y * cropW;
                    int minPx = cropW;
                    int maxPx = -1;
                    for (int x = 0; x < cropW; x++)
                    {
                        if (srcPixels[rowStart + x].a <= threshold) continue;
                        if (x < minPx) minPx = x;
                        if (x > maxPx) maxPx = x;
                    }
                    if (maxPx < minPx) continue;
                    if (topRow < 0) topRow = y;
                    int width = maxPx - minPx + 1;
                    if (prevWidth >= 0)
                    {
                        if (width > prevWidth)
                            sawIncrease = true;
                        else if (width < prevWidth && sawIncrease && peakRow < 0)
                        {
                            peakRow = y - 1;
                            peakWidth = prevWidth;
                            peakMinX = prevMinX;
                            peakMaxX = prevMaxX;
                            break;
                        }
                    }
                    if (width > maxWidth)
                    {
                        maxWidth = width;
                        rowAtMax = y;
                        rowMinX = minPx;
                        rowMaxX = maxPx;
                    }
                    prevWidth = width;
                    prevMinX = minPx;
                    prevMaxX = maxPx;
                }

                int useRow = peakRow >= 0 ? peakRow : rowAtMax;
                int useWidth = peakRow >= 0 ? peakWidth : maxWidth;
                int useMinX = peakRow >= 0 ? peakMinX : rowMinX;
                int useMaxX = peakRow >= 0 ? peakMaxX : rowMaxX;
                if (useRow >= 0 && useWidth > 0)
                {
                    int heightToMax = (topRow >= 0) ? (useRow - topRow) : 0;
                    int faceHFromShape = heightToMax > 0 ? (heightToMax * 2 + 1) : 0;
                    int targetH = faceHFromShape > 0 ? faceHFromShape : Mathf.RoundToInt(useWidth / ratio);
                    if (targetH <= 0) targetH = cropH - Mathf.Max(0, topRow);
                    faceH = Mathf.Clamp(targetH, 1, cropH - Mathf.Max(0, topRow));
                    float rowCenterX = (useMinX + useMaxX) * 0.5f;
                    faceW = Mathf.Clamp(useWidth, 1, cropW);
                    faceX = Mathf.RoundToInt(rowCenterX - (faceW - 1) * 0.5f);
                    faceX = Mathf.Clamp(faceX, 0, cropW - faceW);
                    faceY = topRow >= 0 ? topRow : 0;
                }
            }

            Vector2 cornerTop = Vector2.zero;
            Vector2 cornerRight = Vector2.zero;
            Vector2 cornerBottom = Vector2.zero;
            Vector2 cornerLeft = Vector2.zero;
            bool hasDiamond = false;
            bool[] insideDiamond = null;
            if (useManualDiamond)
            {
                float maxX = cropW - 1f;
                float maxY = cropH - 1f;
                float manualMaxX = maxX;
                float manualMaxY = maxY;
                Vector2 top = GroundTileDiamondTop;
                Vector2 right = GroundTileDiamondRight;
                Vector2 bottom = GroundTileDiamondBottom;
                Vector2 left = GroundTileDiamondLeft;
                if (GroundTileDiamondNormalized)
                {
                    top = new Vector2(top.x * manualMaxX, top.y * manualMaxY);
                    right = new Vector2(right.x * manualMaxX, right.y * manualMaxY);
                    bottom = new Vector2(bottom.x * manualMaxX, bottom.y * manualMaxY);
                    left = new Vector2(left.x * manualMaxX, left.y * manualMaxY);
                }
                if (GroundTileDiamondYFromTop)
                {
                    top.y = manualMaxY - top.y;
                    right.y = manualMaxY - right.y;
                    bottom.y = manualMaxY - bottom.y;
                    left.y = manualMaxY - left.y;
                }
                cornerTop = new Vector2(Mathf.Clamp(top.x, 0f, maxX), Mathf.Clamp(top.y, 0f, maxY));
                cornerRight = new Vector2(Mathf.Clamp(right.x, 0f, maxX), Mathf.Clamp(right.y, 0f, maxY));
                cornerBottom = new Vector2(Mathf.Clamp(bottom.x, 0f, maxX), Mathf.Clamp(bottom.y, 0f, maxY));
                cornerLeft = new Vector2(Mathf.Clamp(left.x, 0f, maxX), Mathf.Clamp(left.y, 0f, maxY));
                float inset = Mathf.Max(0f, insetPixels);
                var centroid = (cornerTop + cornerRight + cornerBottom + cornerLeft) * 0.25f;
                if (inset > 0f)
                {
                    cornerTop = InsetCorner(cornerTop, centroid, inset);
                    cornerRight = InsetCorner(cornerRight, centroid, inset);
                    cornerBottom = InsetCorner(cornerBottom, centroid, inset);
                    cornerLeft = InsetCorner(cornerLeft, centroid, inset);
                    centroid = (cornerTop + cornerRight + cornerBottom + cornerLeft) * 0.25f;
                }
                if (extraTopInsetPixels > 0f)
                    cornerTop = InsetCorner(cornerTop, centroid, extraTopInsetPixels);
                if (extraRightInsetPixels > 0f)
                    cornerRight = InsetCorner(cornerRight, centroid, extraRightInsetPixels);
                if (extraBottomInsetPixels > 0f)
                    cornerBottom = InsetCorner(cornerBottom, centroid, extraBottomInsetPixels);
                if (extraLeftInsetPixels > 0f)
                    cornerLeft = InsetCorner(cornerLeft, centroid, extraLeftInsetPixels);
                float manualW = Vector2.Distance(cornerLeft, cornerRight);
                float manualH = Vector2.Distance(cornerTop, cornerBottom);
                if (manualW >= 1f && manualH >= 1f)
                {
                    faceW = Mathf.Max(1, Mathf.RoundToInt(manualW));
                    faceH = Mathf.Max(1, Mathf.RoundToInt(manualH));
                    float minFaceX = Mathf.Min(Mathf.Min(cornerLeft.x, cornerRight.x), Mathf.Min(cornerTop.x, cornerBottom.x));
                    float maxFaceX = Mathf.Max(Mathf.Max(cornerLeft.x, cornerRight.x), Mathf.Max(cornerTop.x, cornerBottom.x));
                    float minFaceY = Mathf.Min(Mathf.Min(cornerLeft.y, cornerRight.y), Mathf.Min(cornerTop.y, cornerBottom.y));
                    float maxFaceY = Mathf.Max(Mathf.Max(cornerLeft.y, cornerRight.y), Mathf.Max(cornerTop.y, cornerBottom.y));
                    diamondBoundsX = Mathf.Clamp(Mathf.FloorToInt(minFaceX), 0, cropW - 1);
                    diamondBoundsY = Mathf.Clamp(Mathf.FloorToInt(minFaceY), 0, cropH - 1);
                    diamondBoundsW = Mathf.Clamp(Mathf.CeilToInt(maxFaceX - minFaceX + 1f), 1, cropW - diamondBoundsX);
                    diamondBoundsH = Mathf.Clamp(Mathf.CeilToInt(maxFaceY - minFaceY + 1f), 1, cropH - diamondBoundsY);
                    faceX = diamondBoundsX;
                    faceY = diamondBoundsY;
                    hasDiamond = true;
                }
                else
                {
                    useManualDiamond = false;
                    useUnskew = UseGroundTileUnskew;
                }
                if (hasDiamond && GroundTileMaskOutsideDiamond)
                {
                    insideDiamond = BuildDiamondMask(cropW, cropH, cornerTop, cornerRight, cornerBottom, cornerLeft);
                }
            }
            if (useManualDiamond && faceH > 0)
            {
                ratio = Mathf.Clamp(faceW / (float)faceH, 0.25f, 4f);
            }

            if (debugOutline && hasDiamond)
            {
                int outW = Mathf.Max(1, cropW);
                int outH = Mathf.Max(1, cropH);
                var outlinePixels = new Color[outW * outH];
                var outlineColor = GroundTileDebugOutlineColor;
                DrawLine(outlinePixels, outW, outH, cornerTop, cornerRight, outlineColor, GroundTileDebugOutlineThickness);
                DrawLine(outlinePixels, outW, outH, cornerRight, cornerBottom, outlineColor, GroundTileDebugOutlineThickness);
                DrawLine(outlinePixels, outW, outH, cornerBottom, cornerLeft, outlineColor, GroundTileDebugOutlineThickness);
                DrawLine(outlinePixels, outW, outH, cornerLeft, cornerTop, outlineColor, GroundTileDebugOutlineThickness);

                var outlineTexture = new Texture2D(outW, outH, TextureFormat.RGBA32, false)
                {
                    filterMode = GroundTileFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                outlineTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                outlineTexture.SetPixels(outlinePixels);
                outlineTexture.Apply(false, false);

                float ppu = Mathf.Max(0.001f, sprite.pixelsPerUnit);
                var pivotNorm = new Vector2(
                    sprite.rect.width > 0f ? (sprite.pivot.x / sprite.rect.width) : 0.5f,
                    sprite.rect.height > 0f ? (sprite.pivot.y / sprite.rect.height) : 0.5f);
                var outlineSprite = Sprite.Create(outlineTexture, new Rect(0, 0, outW, outH), pivotNorm, ppu);
                outlineSprite.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                outlineSprite.name = sprite.name;

                _runtimeGroundObjects.Add(outlineTexture);
                _runtimeGroundObjects.Add(outlineSprite);
                _runtimeGroundSpriteCache[sprite] = outlineSprite;
                result = outlineSprite;
                return true;
            }

            bool preservePattern = useManualDiamond && GroundTilePreservePattern && hasDiamond;
            if (useManualDiamond && GroundTileNoTransform && hasDiamond)
            {
                int outW = Mathf.Max(1, diamondBoundsW);
                int outH = Mathf.Max(1, diamondBoundsH);
                var noWarpPixels = new Color[outW * outH];
                for (int y = 0; y < outH; y++)
                {
                    int sy = diamondBoundsY + y;
                    if (sy < 0 || sy >= cropH) continue;
                    int srcRow = sy * cropW;
                    int dstRow = y * outW;
                    for (int x = 0; x < outW; x++)
                    {
                        int sx = diamondBoundsX + x;
                        if (sx < 0 || sx >= cropW) continue;
                        if (insideDiamond != null && !insideDiamond[srcRow + sx])
                        {
                            noWarpPixels[dstRow + x] = Color.clear;
                            continue;
                        }
                        noWarpPixels[dstRow + x] = srcPixels[srcRow + sx];
                    }
                }

                var noWarpTexture = new Texture2D(outW, outH, TextureFormat.RGBA32, false)
                {
                    filterMode = GroundTileFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                noWarpTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                noWarpTexture.SetPixels(noWarpPixels);
                noWarpTexture.Apply(false, false);

                float noWarpTargetWorldWidth = outW / Mathf.Max(0.001f, sprite.pixelsPerUnit);
                noWarpTargetWorldWidth = ResolveGroundTargetWorldWidth(noWarpTargetWorldWidth);
                float noWarpPpu = outW / Mathf.Max(0.001f, noWarpTargetWorldWidth);
                var noWarpSprite = Sprite.Create(noWarpTexture, new Rect(0, 0, outW, outH), new Vector2(0.5f, 0.5f), noWarpPpu);
                noWarpSprite.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                noWarpSprite.name = sprite.name;

                _runtimeGroundObjects.Add(noWarpTexture);
                _runtimeGroundObjects.Add(noWarpSprite);
                _runtimeGroundSpriteCache[sprite] = noWarpSprite;
                result = noWarpSprite;
                return true;
            }
            int baseSize = preservePattern
                ? Mathf.RoundToInt(Mathf.Max(diamondBoundsW, diamondBoundsH))
                : (useUnskew
                    ? (useManualDiamond ? Mathf.RoundToInt(faceW / ratio) : faceW)
                    : Mathf.RoundToInt(cropW / ratio));
            int outSize = Mathf.RoundToInt(baseSize * resolutionScale);
            if (outSize <= 0)
                outSize = Mathf.Max(1, preservePattern ? Mathf.Max(diamondBoundsW, diamondBoundsH) : (useUnskew ? faceW : cropH));

            var outPixels = new Color[outSize * outSize];
            Color sum = Color.black;
            int count = 0;
            for (int i = 0; i < srcPixels.Length; i++)
            {
                var c = srcPixels[i];
                if (c.a <= threshold) continue;
                sum.r += c.r;
                sum.g += c.g;
                sum.b += c.b;
                count++;
            }
            Color avg = count > 0
                ? new Color(sum.r / count, sum.g / count, sum.b / count, 1f)
                : Color.black;

            float half = (outSize - 1) * 0.5f;
            float invHalf = half > 0.0001f ? 1f / half : 0f;
            float isoW = (useUnskew ? faceW : cropW) - 1f;
            float isoH = (useUnskew ? faceH : cropH) - 1f;
            float centerX = (useUnskew ? faceX : 0) + isoW * 0.5f;
            float centerY = (useUnskew ? faceY : 0) + isoH * 0.5f
                + (GroundTileCenterYOffset * isoH);
            if (!useManualDiamond && UseGroundTileUnskew)
            {
                hasDiamond = TryFindDiamondCorners(faceX, faceY, faceW, faceH, srcPixels, cropW, threshold,
                    out cornerTop, out cornerRight, out cornerBottom, out cornerLeft);
            }

            for (int y = 0; y < outSize; y++)
            {
                float v = (y - half) * invHalf;
                for (int x = 0; x < outSize; x++)
                {
                    float u = (x - half) * invHalf;
                    float sx;
                    float sy;
                    if (preservePattern)
                    {
                        float offsetX = (outSize - diamondBoundsW) * 0.5f;
                        float offsetY = (outSize - diamondBoundsH) * 0.5f;
                        sx = diamondBoundsX + (x - offsetX);
                        sy = diamondBoundsY + (y - offsetY);
                    }
                    else if (useUnskew && hasDiamond)
                    {
                        float uu = outSize > 1 ? (x / (float)(outSize - 1)) : 0f;
                        float vv = outSize > 1 ? (y / (float)(outSize - 1)) : 0f;
                        float invU = 1f - uu;
                        float invV = 1f - vv;
                        float px = (cornerTop.x * invU * invV)
                            + (cornerRight.x * uu * invV)
                            + (cornerBottom.x * uu * vv)
                            + (cornerLeft.x * invU * vv);
                        float py = (cornerTop.y * invU * invV)
                            + (cornerRight.y * uu * invV)
                            + (cornerBottom.y * uu * vv)
                            + (cornerLeft.y * invU * vv);
                        sx = px;
                        sy = py;
                    }
                    else if (useUnskew)
                    {
                        float isoX = (u - v) * 0.25f * isoW;
                        float isoY = (u + v) * 0.25f * isoH;
                        sx = isoX + centerX;
                        sy = isoY + centerY;
                    }
                    else
                    {
                        sx = ((u * 0.5f) + 0.5f) * (cropW - 1f);
                        sy = ((v * 0.5f) + 0.5f) * (cropH - 1f);
                    }

                    int ix = Mathf.RoundToInt(sx);
                    int iy = Mathf.RoundToInt(sy);
                    int sampleX = ix;
                    int sampleY = iy;
                    bool outOfRange = sampleX < 0 || sampleY < 0 || sampleX >= cropW || sampleY >= cropH;
                    bool outsideDiamond = false;
                    bool inside = true;
                    if (insideDiamond != null)
                    {
                        int cx = Mathf.Clamp(sampleX, 0, cropW - 1);
                        int cy = Mathf.Clamp(sampleY, 0, cropH - 1);
                        inside = insideDiamond[(cy * cropW) + cx];
                        outsideDiamond = !inside;
                        if (GroundTileEdgeDilatePixels > 0 && !inside)
                        {
                            if (TryFindNearestOpaque(cx, cy, cropW, cropH, insideDiamond, srcPixels, threshold, GroundTileEdgeDilatePixels, out var nx, out var ny))
                            {
                                sampleX = nx;
                                sampleY = ny;
                                outOfRange = false;
                                outsideDiamond = false;
                                inside = true;
                            }
                        }
                    }
                    Color sp = (outOfRange || outsideDiamond) ? Color.clear : srcPixels[(sampleY * cropW) + sampleX];
                    if (!outOfRange && inside && sp.a <= threshold && GroundTileEdgeDilatePixels > 0)
                    {
                        if (TryFindNearestOpaque(sampleX, sampleY, cropW, cropH, insideDiamond, srcPixels, threshold, GroundTileEdgeDilatePixels, out var nx, out var ny))
                        {
                            sp = srcPixels[(ny * cropW) + nx];
                        }
                    }
                    if (!outOfRange && !outsideDiamond && edgeTrimPixels > 0 && sp.a > threshold)
                    {
                        int edge = edgeTrimPixels;
                        if (x < edge || y < edge || x >= (outSize - edge) || y >= (outSize - edge))
                        {
                            if (IsEdgeDark(sp, edgeBlackThreshold, edgeChromaThreshold))
                            {
                                int cx = Mathf.Clamp(sampleX, 0, cropW - 1);
                                int cy = Mathf.Clamp(sampleY, 0, cropH - 1);
                                if (TryFindNearestOpaque(cx, cy, cropW, cropH, insideDiamond, srcPixels, threshold, edge, out var nx, out var ny, edgeBlackThreshold, edgeChromaThreshold))
                                {
                                    sp = srcPixels[(ny * cropW) + nx];
                                }
                                else
                                {
                                    sp = Color.clear;
                                }
                            }
                        }
                    }
                    bool transparent = outOfRange || outsideDiamond || sp.a <= threshold;
                    if (transparent)
                    {
                        if (!GroundTileFillTransparent || outsideDiamond)
                            continue;
                        sp = avg;
                        sp.a = 1f;
                    }
            outPixels[(y * outSize) + x] = sp;
                }
            }

            if (GroundTileDebugLogDarkEdges)
            {
                if (HasDarkEdgePixels(outPixels, outSize, outSize, out var edgeRatio))
                {
                    string key = sprite.name;
                    if (_loggedDarkEdgeSprites.Add(key))
                        Debug.LogWarning($"[GroundTileDebug] Dark edge ratio={edgeRatio:0.00} sprite={sprite.name}");
                }
            }

            var outTexture = new Texture2D(outSize, outSize, TextureFormat.RGBA32, false)
            {
                filterMode = GroundTileFilterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            outTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            outTexture.SetPixels(outPixels);
            outTexture.Apply(false, false);

            float targetWorldWidth = (preservePattern ? diamondBoundsW : (useUnskew ? faceW : cropW)) / Mathf.Max(0.001f, sprite.pixelsPerUnit);
            targetWorldWidth = ResolveGroundTargetWorldWidth(targetWorldWidth);
            float newPpu = outSize / Mathf.Max(0.001f, targetWorldWidth);
            var runtimeSprite = Sprite.Create(outTexture, new Rect(0, 0, outSize, outSize), new Vector2(0.5f, 0.5f), newPpu);
            runtimeSprite.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            runtimeSprite.name = sprite.name;

            _runtimeGroundObjects.Add(outTexture);
            _runtimeGroundObjects.Add(runtimeSprite);
            _runtimeGroundSpriteCache[sprite] = runtimeSprite;
            result = runtimeSprite;
            return true;
        }

        private struct Placement
        {
            public Vector2Int Cell;
            public TileBase Tile;
        }

        private float GetTreePlacementWeight(Vector2Int cell)
        {
            return GetTreeDensity(cell) * GetTreeBiomeWeight(cell);
        }

        private void BuildPropCandidateCaches(int width, int height, out List<Vector2Int> landCells, out List<Vector2Int> rockCells, out List<Vector2Int> anyCells)
        {
            int total = width * height;
            landCells = new List<Vector2Int>(Mathf.Max(16, total / 2));
            rockCells = new List<Vector2Int>(Mathf.Max(8, total / 8));
            anyCells = new List<Vector2Int>(Mathf.Max(16, total));
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    var cell = new Vector2Int(col, row);
                    anyCells.Add(cell);
                    if (TryGetBackgroundCellFlags(cell, out bool isWater, out bool isRock))
                    {
                        if (isWater) continue;
                        if (isRock) rockCells.Add(cell);
                        else landCells.Add(cell);
                    }
                    else
                    {
                        landCells.Add(cell);
                    }
                }
            }
        }

        private static void ShuffleList<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void BuildPlacements(
            List<Placement> output,
            List<Vector2Int> candidates,
            int targetCount,
            int minHexDistance,
            System.Random rng,
            TileBase[] palette,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            SpatialHash hash,
            Func<Vector2Int, float> weightFunc,
            bool markBlocked,
            BlockBounds bounds)
        {
            if (targetCount <= 0 || palette == null || palette.Length == 0 || candidates == null || candidates.Count == 0)
                return;

            ShuffleList(candidates, rng);
            for (int i = 0; i < candidates.Count && output.Count < targetCount; i++)
            {
                var cell = candidates[i];
                if (occupiedSet.Contains(cell)) continue;
                if (minHexDistance > 0 && hash != null && !hash.IsFarEnough(cell, minHexDistance)) continue;
                float weight = weightFunc != null ? weightFunc(cell) : 1f;
                if (weight <= 0f) continue;
                if (weight < 1f && rng.NextDouble() > weight) continue;

                var tile = PickTile(rng, palette);
                output.Add(new Placement { Cell = cell, Tile = tile });
                occupied.Add(cell);
                occupiedSet.Add(cell);
                hash?.Add(cell);
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
            }
        }

        private IEnumerator BuildPlacementsBatched(
            List<Placement> output,
            List<Vector2Int> candidates,
            int targetCount,
            int minHexDistance,
            System.Random rng,
            TileBase[] palette,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            SpatialHash hash,
            Func<Vector2Int, float> weightFunc,
            bool markBlocked,
            BlockBounds bounds)
        {
            if (targetCount <= 0 || palette == null || palette.Length == 0 || candidates == null || candidates.Count == 0)
                yield break;

            ShuffleList(candidates, rng);
            int perFrame = Mathf.Max(1, PropCandidatesPerFrame);
            int processed = 0;
            for (int i = 0; i < candidates.Count && output.Count < targetCount; i++)
            {
                var cell = candidates[i];
                if (occupiedSet.Contains(cell)) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }
                if (minHexDistance > 0 && hash != null && !hash.IsFarEnough(cell, minHexDistance)) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }
                float weight = weightFunc != null ? weightFunc(cell) : 1f;
                if (weight <= 0f) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }
                if (weight < 1f && rng.NextDouble() > weight) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }

                var tile = PickTile(rng, palette);
                output.Add(new Placement { Cell = cell, Tile = tile });
                occupied.Add(cell);
                occupiedSet.Add(cell);
                hash?.Add(cell);
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);

                processed++;
                if (processed >= perFrame)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }

        private static void ApplyPlacements(Tilemap map, List<Placement> placements)
        {
            if (map == null || placements == null || placements.Count == 0) return;
            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                map.SetTile(new Vector3Int(placement.Cell.x, placement.Cell.y, 0), placement.Tile);
            }
        }

        private void PlaceProps(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked,
            PropBiomeFilter biomeFilter)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) return;
            int attempts = 0;
            int placed = 0;
            while (placed < targetCount && attempts < targetCount * 50)
            {
                attempts++;
                if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                    continue;
                if (!IsAllowedPropCell(cell, biomeFilter))
                    continue;

                occupied.Add(cell);
                occupiedSet.Add(cell);
                map.SetTile(new Vector3Int(cell.x, cell.y, 0), PickTile(rng, palette));
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
                placed++;
            }
        }

        private IEnumerator PlacePropsBatched(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked,
            PropBiomeFilter biomeFilter)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) yield break;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 50;
            int attemptsPerFrame = Mathf.Max(1, PropAttemptsPerFrame);
            while (placed < targetCount && attempts < maxAttempts)
            {
                int frameAttempts = attemptsPerFrame;
                for (int i = 0; i < frameAttempts && placed < targetCount && attempts < maxAttempts; i++)
                {
                    attempts++;
                    if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                        continue;
                    if (!IsAllowedPropCell(cell, biomeFilter))
                        continue;

                    occupied.Add(cell);
                    occupiedSet.Add(cell);
                    map.SetTile(new Vector3Int(cell.x, cell.y, 0), PickTile(rng, palette));
                    bounds?.Include(cell.x, cell.y);
                    if (markBlocked)
                        MarkBlockedCell(cell);
                    placed++;
                }
                yield return null;
            }
        }

        private void PlaceTrees(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) return;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 200;
            while (placed < targetCount && attempts < maxAttempts)
            {
                attempts++;
                if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                    continue;
                if (!IsAllowedPropCell(cell, PropBiomeFilter.LandOnly))
                    continue;
                float density = GetTreeDensity(cell);
                float biomeWeight = GetTreeBiomeWeight(cell);
                if (rng.NextDouble() > density * biomeWeight)
                    continue;

                occupied.Add(cell);
                occupiedSet.Add(cell);
                map.SetTile(new Vector3Int(cell.x, cell.y, 0), PickTile(rng, palette));
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
                placed++;
            }
        }

        private IEnumerator PlaceTreesBatched(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) yield break;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 200;
            int attemptsPerFrame = Mathf.Max(1, PropAttemptsPerFrame);
            while (placed < targetCount && attempts < maxAttempts)
            {
                int frameAttempts = attemptsPerFrame;
                for (int i = 0; i < frameAttempts && placed < targetCount && attempts < maxAttempts; i++)
                {
                    attempts++;
                    if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                        continue;
                    if (!IsAllowedPropCell(cell, PropBiomeFilter.LandOnly))
                        continue;
                    float density = GetTreeDensity(cell);
                    float biomeWeight = GetTreeBiomeWeight(cell);
                    if (rng.NextDouble() > density * biomeWeight)
                        continue;

                    occupied.Add(cell);
                    occupiedSet.Add(cell);
                    map.SetTile(new Vector3Int(cell.x, cell.y, 0), PickTile(rng, palette));
                    bounds?.Include(cell.x, cell.y);
                    if (markBlocked)
                        MarkBlockedCell(cell);
                    placed++;
                }
                yield return null;
            }
        }

        private static bool TryPickCell(int width, int height, System.Random rng, int minHexDistance,
            List<Vector2Int> occupied, HashSet<Vector2Int> occupiedSet, out Vector2Int cell)
        {
            int col = rng.Next(0, width);
            int row = rng.Next(0, height);
            cell = new Vector2Int(col, row);
            if (occupiedSet.Contains(cell)) return false;
            if (minHexDistance > 0)
            {
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (HexDistance(cell, occupied[i]) < minHexDistance)
                        return false;
                }
            }
            return true;
        }

        private enum PropBiomeFilter
        {
            Any,
            LandOnly,
            RockOnly
        }

        private bool IsAllowedPropCell(Vector2Int cell, PropBiomeFilter filter)
        {
            if (filter == PropBiomeFilter.Any) return true;
            if (TryGetBackgroundCellFlags(cell, out bool isWater, out bool isRock))
            {
                if (isWater) return false;
                if (filter == PropBiomeFilter.RockOnly) return isRock;
                return !isRock;
            }

            bool hasBiome = TryGetBiomeCountsAtCell(cell, out int waterCount, out int rockCount);
            if (!hasBiome)
                return filter != PropBiomeFilter.RockOnly;
            if (filter == PropBiomeFilter.RockOnly)
                return rockCount > 0 && rockCount >= waterCount;
            return waterCount == 0 && rockCount == 0;
        }

        private bool TryGetBiomeFlagsAtCell(Vector2Int cell, out bool isWater, out bool isRock)
        {
            isWater = false;
            isRock = false;
            if (!TryGetBiomeCountsAtCell(cell, out int waterCount, out int rockCount))
                return false;

            if (waterCount > 0 && waterCount >= rockCount)
            {
                isWater = true;
                return true;
            }
            if (rockCount > 0)
            {
                isRock = true;
                return true;
            }
            return true;
        }

        private void CacheBackgroundBiomeMasks(int width, int height, bool[] waterMask, bool[] rockMask)
        {
            if (!UseBackgroundTilemap || _background == null)
            {
                _backgroundWaterMask = null;
                _backgroundRockMask = null;
                _backgroundMaskWidth = 0;
                _backgroundMaskHeight = 0;
                _backgroundLandDistance = null;
                _backgroundLandMaxDistance = 0;
                return;
            }

            _backgroundMaskWidth = width;
            _backgroundMaskHeight = height;
            _backgroundWaterMask = waterMask;
            _backgroundRockMask = rockMask;
            _backgroundLandDistance = BuildLandDistanceField(width, height, waterMask, rockMask, out _backgroundLandMaxDistance);
        }

        private bool TryGetBiomeCountsAtCell(Vector2Int cell, out int waterCount, out int rockCount)
        {
            waterCount = 0;
            rockCount = 0;
            bool usedMask = TrySampleBackgroundBiomeCounts(cell, out waterCount, out rockCount);
            if (usedMask && (waterCount > 0 || rockCount > 0))
                return true;

            TileBase tile = null;
            if (UseBackgroundTilemap && _background != null && _backgroundGrid != null && _grid != null)
            {
                Vector3 world = _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                Vector3Int bgCell = _backgroundGrid.WorldToCell(world);
                tile = _background.GetTile(bgCell);
            }
            else if (_ground != null)
            {
                tile = _ground.GetTile(new Vector3Int(cell.x, cell.y, 0));
            }

            if (tile == null) return false;
            string tileName = tile.name;
            string spriteName = null;
            if (tile is Tile tileAsset && tileAsset.sprite != null)
                spriteName = tileAsset.sprite.name;
            bool matchesWater = IsNameMatch(tileName, WaterTileNameKeywords)
                || IsNameMatch(tileName, WaterInteriorTileNameKeywords)
                || (!string.IsNullOrEmpty(spriteName)
                    && (IsNameMatch(spriteName, WaterTileNameKeywords)
                        || IsNameMatch(spriteName, WaterInteriorTileNameKeywords)));
            bool matchesRock = IsNameMatch(tileName, RockTileNameKeywords)
                || (!string.IsNullOrEmpty(spriteName) && IsNameMatch(spriteName, RockTileNameKeywords));
            if (matchesWater) waterCount = 1;
            if (matchesRock) rockCount = 1;
            return true;
        }

        private bool TrySampleBackgroundBiomeCounts(Vector2Int cell, out int waterCount, out int rockCount)
        {
            waterCount = 0;
            rockCount = 0;
            if (!UseBackgroundTilemap || _backgroundGrid == null || _grid == null)
                return false;
            if (_backgroundWaterMask == null || _backgroundMaskWidth <= 0 || _backgroundMaskHeight <= 0)
                return false;

            Vector3 center = _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            float cellSizeX = Mathf.Abs(_grid.cellSize.x);
            float cellSizeY = Mathf.Abs(_grid.cellSize.y);
            int sampleCount = 0;
            for (int i = 0; i < BiomeSampleOffsets.Length; i++)
            {
                var offset = BiomeSampleOffsets[i];
                Vector3 world = center + new Vector3(offset.x * cellSizeX, offset.y * cellSizeY, 0f);
                Vector3Int bgCell = _backgroundGrid.WorldToCell(world);
                int sx = bgCell.x;
                int sy = bgCell.y;
                if (sx < 0 || sy < 0 || sx >= _backgroundMaskWidth || sy >= _backgroundMaskHeight)
                    continue;
                sampleCount++;
                int idx = (sy * _backgroundMaskWidth) + sx;
                bool waterMask = _backgroundWaterMask[idx]
                    || IsMaskHole(_backgroundWaterMask, _backgroundMaskWidth, _backgroundMaskHeight, sx, sy);
                if (waterMask)
                {
                    waterCount++;
                    continue;
                }
                if (_backgroundRockMask != null && _backgroundRockMask.Length == _backgroundWaterMask.Length
                    && _backgroundRockMask[idx])
                {
                    rockCount++;
                }
            }

            return sampleCount > 0;
        }

        private float GetTreeDensity(Vector2Int cell)
        {
            if (_backgroundLandDistance == null || _backgroundLandDistance.Length == 0 || _backgroundLandMaxDistance <= 0)
                return 1f;
            if (!TryGetBackgroundCellIndex(cell, out int idx))
                return 1f;
            int dist = _backgroundLandDistance[idx];
            float t = _backgroundLandMaxDistance > 0 ? Mathf.Clamp01(dist / (float)_backgroundLandMaxDistance) : 1f;
            float shaped = Mathf.Pow(t, TreeGradientPower);
            return Mathf.Lerp(TreeGradientEdge, 1f, shaped);
        }

        private float GetTreeBiomeWeight(Vector2Int cell)
        {
            if (!UseTreeBiomeNoise || TreeBiomeScale <= 0f)
                return 1f;
            float scale = Mathf.Max(0.0001f, TreeBiomeScale);
            int octaves = Mathf.Max(1, TreeBiomeOctaves);
            float persistence = Mathf.Clamp01(TreeBiomePersistence);
            float lacunarity = Mathf.Max(0.01f, TreeBiomeLacunarity);
            float threshold = Mathf.Clamp01(TreeBiomeThreshold);
            float feather = Mathf.Clamp01(TreeBiomeFeather);

            int q = cell.x - (cell.y - (cell.y & 1)) / 2;
            int r = cell.y;
            const float sqrt3Over2 = 0.8660254f;
            float wx = q + (r * 0.5f);
            float wy = r * sqrt3Over2;

            float nx = (wx + _treeNoiseOffset.x) * scale;
            float ny = (wy + _treeNoiseOffset.y) * scale;
            float v = FractalNoise(nx, ny, octaves, persistence, lacunarity);
            v = AdjustContrast(v, TreeBiomeContrast);

            if (feather <= 0f)
                return v >= threshold ? 1f : 0f;

            float t0 = threshold - feather;
            float t1 = threshold + feather;
            return Mathf.Clamp01((v - t0) / Mathf.Max(0.0001f, t1 - t0));
        }

        private bool TryGetBackgroundCellIndex(Vector2Int cell, out int idx)
        {
            idx = 0;
            if (_backgroundGrid == null || _grid == null)
                return false;
            Vector3 world = _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            Vector3Int bgCell = _backgroundGrid.WorldToCell(world);
            if (bgCell.x < 0 || bgCell.y < 0 || bgCell.x >= _backgroundMaskWidth || bgCell.y >= _backgroundMaskHeight)
                return false;
            idx = (bgCell.y * _backgroundMaskWidth) + bgCell.x;
            return true;
        }

        private bool TryGetBackgroundCellFlags(Vector2Int cell, out bool isWater, out bool isRock)
        {
            isWater = false;
            isRock = false;
            if (_backgroundWaterMask == null || _backgroundMaskWidth <= 0 || _backgroundMaskHeight <= 0)
                return false;
            if (!TryGetBackgroundCellIndex(cell, out int idx))
                return false;
            int x = idx % _backgroundMaskWidth;
            int y = idx / _backgroundMaskWidth;
            bool waterMask = _backgroundWaterMask[idx]
                || IsMaskHole(_backgroundWaterMask, _backgroundMaskWidth, _backgroundMaskHeight, x, y);
            if (waterMask)
            {
                isWater = true;
                return true;
            }
            if (_backgroundRockMask != null && _backgroundRockMask.Length == _backgroundWaterMask.Length
                && _backgroundRockMask[idx])
            {
                isRock = true;
            }
            return true;
        }

        private void MarkBlockedCell(Vector2Int cell)
        {
            if (_hex == null) return;
            if (_directBlockedSet.Contains(cell)) return;
            _directBlockedSet.Add(cell);
            _directBlockedCells.Add(cell);
            _hex.SetWalkable(cell.x, cell.y, false);
        }

        private void ClearDirectBlocks()
        {
            if (_hex == null || _directBlockedCells.Count == 0) return;
            for (int i = 0; i < _directBlockedCells.Count; i++)
            {
                var cell = _directBlockedCells[i];
                _hex.SetWalkable(cell.x, cell.y, true);
            }
            _directBlockedCells.Clear();
            _directBlockedSet.Clear();
        }

        private sealed class SpatialHash
        {
            private readonly int _cellSize;
            private readonly Dictionary<long, List<Vector2Int>> _buckets = new Dictionary<long, List<Vector2Int>>(1024);

            public SpatialHash(int cellSize)
            {
                _cellSize = Mathf.Max(1, cellSize);
            }

            public void Add(Vector2Int cell)
            {
                Vector2Int bucket = GetBucket(cell);
                long key = Key(bucket.x, bucket.y);
                if (!_buckets.TryGetValue(key, out var list))
                {
                    list = new List<Vector2Int>(8);
                    _buckets[key] = list;
                }
                list.Add(cell);
            }

            public bool IsFarEnough(Vector2Int cell, int minDist)
            {
                if (minDist <= 0) return true;
                int radius = Mathf.Max(1, Mathf.CeilToInt(minDist / (float)_cellSize));
                Vector2Int bucket = GetBucket(cell);
                for (int by = -radius; by <= radius; by++)
                {
                    for (int bx = -radius; bx <= radius; bx++)
                    {
                        long key = Key(bucket.x + bx, bucket.y + by);
                        if (!_buckets.TryGetValue(key, out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (HexDistance(cell, list[i]) < minDist)
                                return false;
                        }
                    }
                }
                return true;
            }

            private Vector2Int GetBucket(Vector2Int cell)
            {
                return new Vector2Int(cell.x / _cellSize, cell.y / _cellSize);
            }

            private static long Key(int x, int y)
            {
                unchecked
                {
                    return ((long)x << 32) ^ (uint)y;
                }
            }
        }

        private sealed class BlockBounds
        {
            public int MinCol;
            public int MinRow;
            public int MaxCol;
            public int MaxRow;

            public bool HasAny => MaxCol >= 0 && MaxRow >= 0;

            public void Reset(int width, int height)
            {
                MinCol = width;
                MinRow = height;
                MaxCol = -1;
                MaxRow = -1;
            }

            public void Include(int col, int row)
            {
                if (col < MinCol) MinCol = col;
                if (row < MinRow) MinRow = row;
                if (col > MaxCol) MaxCol = col;
                if (row > MaxRow) MaxRow = row;
            }
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            Axial aa = OddRToAxial(a.x, a.y);
            Axial bb = OddRToAxial(b.x, b.y);
            int dx = aa.q - bb.q; if (dx < 0) dx = -dx;
            int dz = aa.r - bb.r; if (dz < 0) dz = -dz;
            int dy = -(aa.q + aa.r) - (-(bb.q + bb.r));
            if (dy < 0) dy = -dy;
            return (dx + dy + dz) / 2;
        }

        private struct Axial { public int q; public int r; }
        private static Axial OddRToAxial(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return new Axial { q = q, r = r };
        }
    }
}
