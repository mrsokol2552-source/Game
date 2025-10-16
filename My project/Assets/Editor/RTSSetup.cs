using System.IO;
using Game.Presentation.Input;
using Game.Presentation.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BuildingConfig = Game.Infrastructure.Configs.BuildingConfig;

public static class RTSSetup
{
    private const string PrefabsFolder = "Assets/Prefabs";
    private const string ArtFolder = "Assets/Art/Generated";
    private const string SpritePath = ArtFolder + "/unit_white.png";
    private const string UnitPrefabPath = PrefabsFolder + "/Unit.prefab";
    private const string ScriptableObjectsFolder = "Assets/ScriptableObjects/Configs";

    [MenuItem("Tools/RTS/Setup/Create Unit Prefab")]
    public static void CreateUnitPrefabMenu()
    {
        EnsureFolder(PrefabsFolder);
        EnsureFolder(ArtFolder);

        var sprite = CreateOrGetSpriteAsset(SpritePath, Color.white);
        var go = new GameObject("Unit");
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        go.AddComponent<UnitView>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, UnitPrefabPath);
        Object.DestroyImmediate(go);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[RTSSetup] Created Unit prefab at {UnitPrefabPath}");
    }

    [MenuItem("Tools/RTS/Setup/Add Spawner To Scene")]
    public static void AddSpawnerToSceneMenu()
    {
        var unitPrefab = AssetDatabase.LoadAssetAtPath<UnitView>(UnitPrefabPath);
        if (unitPrefab == null)
        {
            Debug.LogWarning("[RTSSetup] Unit prefab not found. Creating it first...");
            CreateUnitPrefabMenu();
            unitPrefab = AssetDatabase.LoadAssetAtPath<UnitView>(UnitPrefabPath);
        }

        var spawner = new GameObject("Spawner");
        var cmd = spawner.AddComponent<UnitSpawnerCommander>();
        cmd.UnitPrefab = unitPrefab;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = spawner;
        Debug.Log("[RTSSetup] Added Spawner with Unit prefab reference to the scene.");
    }

    [MenuItem("Tools/RTS/Setup/Create Unit Prefab + Spawner")]
    public static void CreateAll()
    {
        CreateUnitPrefabMenu();
        AddSpawnerToSceneMenu();
    }

    [MenuItem("Tools/RTS/Setup/Create Building Config Asset")] 
    public static void CreateBuildingConfigAsset()
    {
        EnsureFolder(ScriptableObjectsFolder);
        var asset = ScriptableObject.CreateInstance<BuildingConfig>();
        asset.name = "Building Config";
        // Optional default cost
        // asset.Cost = new [] { new Game.Domain.Economy.ResourceAmount(Game.Domain.Economy.ResourceType.Materials, 25) };
        var path = AssetDatabase.GenerateUniqueAssetPath(ScriptableObjectsFolder + "/Building Config.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"[RTSSetup] Created Building Config at {path}");
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static Sprite CreateOrGetSpriteAsset(string path, Color color)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        // Create a simple 64x64 PNG and import it as a Sprite
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pixels = tex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (Color32)color;
        tex.SetPixels32(pixels);
        tex.Apply();

        var png = tex.EncodeToPNG();
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError("[RTSSetup] Failed to create sprite asset at: " + path);
        }
        return sprite;
    }
}
