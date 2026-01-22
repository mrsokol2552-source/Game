using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using SmallScaleInc.CharacterCreatorModern;

public static class CharacterCreatorSceneBuilder
{
    [MenuItem("Tools/Character Creator/Create Simple Generator Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // EventSystem
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Canvas
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Root generator
        var root = new GameObject("CharacterCreator");
        var generator = root.AddComponent<SpritesheetGenerator>();
        var config = root.AddComponent<CharacterCreatorSimpleConfig>();
        config.Generator = generator;

        // Renderers
        var renderers = new GameObject("Renderers");
        renderers.transform.SetParent(root.transform, false);
        generator.shoesRenderer = CreateRenderer(renderers.transform, "ShoesRenderer");
        generator.legsRenderer = CreateRenderer(renderers.transform, "LegsRenderer");
        generator.chestRenderer = CreateRenderer(renderers.transform, "ChestRenderer");
        generator.headRenderer = CreateRenderer(renderers.transform, "HeadRenderer");
        generator.skinColorRenderer = CreateRenderer(renderers.transform, "SkinRenderer");
        generator.shadowRenderer = CreateRenderer(renderers.transform, "ShadowRenderer");
        generator.gunFireRenderer = CreateRenderer(renderers.transform, "GunFireRenderer");

        // TMP Texts
        var texts = new GameObject("Texts");
        texts.transform.SetParent(canvasGO.transform, false);
        generator.shoesGearNameText = CreateTMP(texts.transform, "ShoesName");
        generator.chestGearNameText = CreateTMP(texts.transform, "ChestName");
        generator.legsGearNameText = CreateTMP(texts.transform, "LegsName");
        generator.headGearNameText = CreateTMP(texts.transform, "HeadName");

        generator.shoesColorText = CreateTMP(texts.transform, "ShoesColor");
        generator.chestColorText = CreateTMP(texts.transform, "ChestColor");
        generator.legsColorText = CreateTMP(texts.transform, "LegsColor");
        generator.headColorText = CreateTMP(texts.transform, "HeadColor");

        generator.weaponNameText = CreateTMP(texts.transform, "WeaponName");
        generator.weaponColorText = CreateTMP(texts.transform, "WeaponColor");
        generator.backpackNameText = CreateTMP(texts.transform, "BackpackName");
        generator.backpackColorText = CreateTMP(texts.transform, "BackpackColor");
        generator.shieldNameText = CreateTMP(texts.transform, "ShieldName");
        generator.shieldColorText = CreateTMP(texts.transform, "ShieldColor");
        generator.mountNameText = CreateTMP(texts.transform, "MountName");
        generator.mountColorText = CreateTMP(texts.transform, "MountColor");
        generator.shadowGearNameText = CreateTMP(texts.transform, "ShadowName");
        generator.shadowColorText = CreateTMP(texts.transform, "ShadowColor");
        generator.gunFireNameText = CreateTMP(texts.transform, "GunFireName");
        generator.gunFireColorText = CreateTMP(texts.transform, "GunFireColor");
        generator.skinColorText = CreateTMP(texts.transform, "SkinColor");
        generator.currentlyGeneratingTMP = CreateTMP(texts.transform, "GeneratingText");

        // Toggles + Slider (hidden)
        var toggles = new GameObject("Toggles");
        toggles.transform.SetParent(canvasGO.transform, false);
        generator.sliceSpritesheets = CreateToggle(toggles.transform, "SliceSpritesheets", true);
        generator.use64Toggle = CreateToggle(toggles.transform, "Use64", true);
        generator.use128Toggle = CreateToggle(toggles.transform, "Use128", false);
        generator.outlineToggle = CreateToggle(toggles.transform, "Outline", false);
        generator.gradientOutlineToggle = CreateToggle(toggles.transform, "GradientOutline", false);
        generator.gunFireToggle = CreateToggle(toggles.transform, "GunFire", false);
        generator.skinToggle = CreateToggle(toggles.transform, "Skin", true);
        generator.maxFramesToggle = CreateToggle(toggles.transform, "MaxFrames15", true);
        generator.fourteenFramesToggle = CreateToggle(toggles.transform, "Frames14", false);
        generator.twelveFramesToggle = CreateToggle(toggles.transform, "Frames12", false);
        generator.tenFramesToggle = CreateToggle(toggles.transform, "Frames10", false);
        generator.eightFramesToggle = CreateToggle(toggles.transform, "Frames8", false);
        generator.sixFramesToggle = CreateToggle(toggles.transform, "Frames6", false);
        generator.fourFramesToggle = CreateToggle(toggles.transform, "Frames4", false);

        generator.loadProgressSlider = CreateSlider(canvasGO.transform, "LoadProgress");
        generator.loadScreenPanel = new GameObject("LoadScreenPanel");
        generator.loadScreenPanel.transform.SetParent(canvasGO.transform, false);
        generator.loadScreenPanel.SetActive(false);

        // Arrays to avoid null refs in Update()
        generator.weapons = Array.Empty<SpritesheetGenerator.Weapon>();
        generator.backpacks = Array.Empty<SpritesheetGenerator.Backpack>();
        generator.shields = Array.Empty<SpritesheetGenerator.Shield>();
        generator.mounts = Array.Empty<SpritesheetGenerator.Mount>();

        config.ApplyToGenerator();

        const string scenePath = "Assets/Scenes/CharacterCreator_Simple.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        Debug.Log($"Created Character Creator scene at: {scenePath}");
    }

    private static SpriteRenderer CreateRenderer(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.color = Color.white;
        sr.enabled = true;
        return sr;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 18;
        return tmp;
    }

    private static Toggle CreateToggle(Transform parent, string name, bool isOn)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var toggle = go.AddComponent<Toggle>();
        toggle.isOn = isOn;
        return toggle;
    }

    private static Slider CreateSlider(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var slider = go.AddComponent<Slider>();
        slider.value = 0f;
        return slider;
    }
}
