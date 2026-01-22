using UnityEngine;
using TMPro;
using UnityEngine.UI;
using SmallScaleInc.CharacterCreatorModern;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CharacterCreatorSimpleConfig : MonoBehaviour
{
    public SpritesheetGenerator Generator;

    [Header("Folder Names")]
    public string ShoesFolder = "Shoes1";
    public string LegsFolder = "Legs1";
    public string ChestFolder = "Chest4";
    public string HeadFolder = "Head1";

    [Header("Colors")]
    public Color ShoesColor = Color.white;
    public Color LegsColor = Color.white;
    public Color ChestColor = Color.white;
    public Color HeadColor = Color.white;
    public Color SkinColor = Color.white;

    [Header("Options")]
    public bool Use64 = true;
    public bool SliceSpritesheets = true;
    public bool Outline = false;
    public bool GradientOutline = false;

    [Header("Preview")]
    [Tooltip("Preview row in the spritesheet (0 = bottom row).")]
    [Range(0, 7)]
    public int PreviewRow = 0;

    [Tooltip("Preview column in the spritesheet (0 = first frame).")]
    [Range(0, 14)]
    public int PreviewColumn = 0;

    [ContextMenu("Apply To Generator")]
    public void ApplyToGenerator()
    {
        if (Generator == null) return;

        SetText(Generator.shoesGearNameText, ShoesFolder);
        SetText(Generator.legsGearNameText, LegsFolder);
        SetText(Generator.chestGearNameText, ChestFolder);
        SetText(Generator.headGearNameText, HeadFolder);

        SetText(Generator.weaponNameText, "None");
        SetText(Generator.backpackNameText, "None");
        SetText(Generator.shieldNameText, "None");
        SetText(Generator.mountNameText, "None");
        SetText(Generator.shadowGearNameText, "Shadow");
        SetText(Generator.gunFireNameText, "None");

        SetColor(Generator.shoesRenderer, ShoesColor);
        SetColor(Generator.legsRenderer, LegsColor);
        SetColor(Generator.chestRenderer, ChestColor);
        SetColor(Generator.headRenderer, HeadColor);
        SetColor(Generator.skinColorRenderer, SkinColor);

        SetText(Generator.shoesColorText, ColorToHex(ShoesColor));
        SetText(Generator.legsColorText, ColorToHex(LegsColor));
        SetText(Generator.chestColorText, ColorToHex(ChestColor));
        SetText(Generator.headColorText, ColorToHex(HeadColor));
        SetText(Generator.skinColorText, ColorToHex(SkinColor));
        SetText(Generator.weaponColorText, "#000000");
        SetText(Generator.backpackColorText, "#000000");
        SetText(Generator.shieldColorText, "#000000");
        SetText(Generator.mountColorText, "#000000");
        SetText(Generator.shadowColorText, "100%");
        SetText(Generator.gunFireColorText, "#000000");

        SetToggle(Generator.use64Toggle, Use64);
        SetToggle(Generator.use128Toggle, !Use64);
        SetToggle(Generator.sliceSpritesheets, SliceSpritesheets);
        SetToggle(Generator.outlineToggle, Outline);
        SetToggle(Generator.gradientOutlineToggle, GradientOutline);
    }

    [ContextMenu("Preview Idle")]
    public void PreviewIdle()
    {
#if UNITY_EDITOR
        ApplyToGenerator();
        if (Generator == null) return;

        SetLayerPreview(Generator.shoesRenderer, ShoesFolder, ShoesColor);
        SetLayerPreview(Generator.legsRenderer, LegsFolder, LegsColor);
        SetLayerPreview(Generator.chestRenderer, ChestFolder, ChestColor);
        SetLayerPreview(Generator.headRenderer, HeadFolder, HeadColor);

        bool useSkin = Generator.skinToggle == null || Generator.skinToggle.isOn;
        SetLayerPreview(Generator.skinColorRenderer, useSkin ? "NakedBody" : null, SkinColor);

        // Optional: shadow preview if available
        if (Generator.shadowRenderer != null)
        {
            SetLayerPreview(Generator.shadowRenderer, "Shadow", Generator.shadowRenderer.color);
        }

        SceneView.RepaintAll();
#endif
    }

    [ContextMenu("Clear Preview")]
    public void ClearPreview()
    {
        if (Generator == null) return;
        ClearLayer(Generator.shoesRenderer);
        ClearLayer(Generator.legsRenderer);
        ClearLayer(Generator.chestRenderer);
        ClearLayer(Generator.headRenderer);
        ClearLayer(Generator.skinColorRenderer);
        ClearLayer(Generator.shadowRenderer);
    }

    [ContextMenu("Generate Spritesheets")]
    public void Generate()
    {
        ApplyToGenerator();
        if (Generator != null)
            Generator.StartCombineSpritesheets();
    }

    private void OnValidate()
    {
        ApplyToGenerator();
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null) label.text = value;
    }

    private static void SetColor(SpriteRenderer renderer, Color value)
    {
        if (renderer != null) renderer.color = value;
    }

    private static void SetToggle(Toggle toggle, bool value)
    {
        if (toggle != null) toggle.isOn = value;
    }

#if UNITY_EDITOR
    private void SetLayerPreview(SpriteRenderer renderer, string folderName, Color tint)
    {
        if (renderer == null) return;
        if (string.IsNullOrWhiteSpace(folderName) || folderName == "None")
        {
            renderer.sprite = null;
            return;
        }

        const string basePath = "Assets/SmallScaleInt/Character creator - Modern/Spritesheets";
        string assetPath = $"{basePath}/{folderName}/Idle.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null)
        {
            renderer.sprite = null;
            return;
        }

        int columns = 15;
        int rows = 8;
        int frameW = tex.width / columns;
        int frameH = tex.height / rows;
        int row = Mathf.Clamp(PreviewRow, 0, rows - 1);
        int col = Mathf.Clamp(PreviewColumn, 0, columns - 1);
        var rect = new Rect(col * frameW, row * frameH, frameW, frameH);

        var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
        sprite.name = $"{folderName}_IdlePreview_{row}_{col}";
        renderer.sprite = sprite;
        renderer.color = tint;
    }
#endif

    private static void ClearLayer(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        renderer.sprite = null;
    }

    private static string ColorToHex(Color color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }
}
