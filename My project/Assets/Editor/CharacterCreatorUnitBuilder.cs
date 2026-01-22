using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Presentation.View;

public static class CharacterCreatorUnitBuilder
{
    [MenuItem("Tools/Character Creator/Build Unit Animation Set (15 frames)")]
    public static void BuildUnitAnimationSet()
    {
        const string baseFolder = "Assets/SmallScaleInt/Character creator - Modern/Created Spritesheets";
        if (!AssetDatabase.IsValidFolder(baseFolder))
        {
            Debug.LogError($"Character Creator folder not found: {baseFolder}");
            return;
        }

        string[] subDirs = AssetDatabase.GetSubFolders(baseFolder);
        if (subDirs == null || subDirs.Length == 0)
        {
            Debug.LogError("No Created Spritesheets folders found.");
            return;
        }

        // Pick the most recently modified folder
        string latestDir = subDirs
            .Select(d => new DirectoryInfo(Path.Combine(Application.dataPath, d.Substring("Assets/".Length))))
            .OrderByDescending(d => d.LastWriteTime)
            .Select(d => "Assets/" + d.FullName.Substring(Application.dataPath.Length + 1).Replace('\\', '/'))
            .First();

        string idlePath = Path.Combine(latestDir, "Idle.png").Replace('\\', '/');
        string walkPath = Path.Combine(latestDir, "Walk.png").Replace('\\', '/');
        string attackPath = Path.Combine(latestDir, "Attack1.png").Replace('\\', '/');
        string deathPath = Path.Combine(latestDir, "Die.png").Replace('\\', '/');

        if (!File.Exists(Path.Combine(Application.dataPath, idlePath.Substring("Assets/".Length))) ||
            !File.Exists(Path.Combine(Application.dataPath, walkPath.Substring("Assets/".Length))))
        {
            Debug.LogError($"Idle/Walk spritesheets not found in: {latestDir}");
            return;
        }

        Sprite[] idleSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(idlePath).OfType<Sprite>().ToArray();
        Sprite[] walkSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(walkPath).OfType<Sprite>().ToArray();
        Sprite[] attackSprites = File.Exists(Path.Combine(Application.dataPath, attackPath.Substring("Assets/".Length)))
            ? AssetDatabase.LoadAllAssetRepresentationsAtPath(attackPath).OfType<Sprite>().ToArray()
            : Array.Empty<Sprite>();
        Sprite[] deathSprites = File.Exists(Path.Combine(Application.dataPath, deathPath.Substring("Assets/".Length)))
            ? AssetDatabase.LoadAllAssetRepresentationsAtPath(deathPath).OfType<Sprite>().ToArray()
            : Array.Empty<Sprite>();

        if (idleSprites.Length == 0 || walkSprites.Length == 0)
        {
            Debug.LogError("Spritesheets are not sliced. Enable slicing in the Character Creator generator.");
            return;
        }

        int framesPerDir = GetFramesPerDirection(idleSprites);
        if (framesPerDir <= 0)
        {
            Debug.LogError("Failed to detect frames per direction from Idle sprites.");
            return;
        }

        var animSet = ScriptableObject.CreateInstance<DirectionalAnimationSet>();
        animSet.FramesPerDirection = framesPerDir;
        animSet.IdleFrames = BuildDirectionalArray(idleSprites, framesPerDir);
        animSet.WalkFrames = BuildDirectionalArray(walkSprites, framesPerDir);
        animSet.AttackFrames = BuildDirectionalArray(attackSprites, framesPerDir);
        animSet.DeathFrames = BuildDirectionalArray(deathSprites, framesPerDir);

        const string outFolder = "Assets/Art/Generated/CharacterCreator";
        if (!AssetDatabase.IsValidFolder(outFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Generated", "CharacterCreator");
        }

        string assetPath = Path.Combine(outFolder, "Unit_CharacterCreator_AnimSet.asset").Replace('\\', '/');
        var existing = AssetDatabase.LoadAssetAtPath<DirectionalAnimationSet>(assetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(animSet, existing);
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(existing);
            animSet = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(animSet, assetPath);
        }

        AssignToUnitPrefab(animSet);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Character Creator animation set built from: {latestDir}");
    }

    private static int GetFramesPerDirection(Sprite[] sprites)
    {
        int maxCol = -1;
        foreach (var s in sprites)
        {
            if (TryParseName(s.name, out _, out int col))
                if (col > maxCol) maxCol = col;
        }
        return maxCol + 1;
    }

    private static Sprite[] BuildDirectionalArray(Sprite[] sprites, int framesPerDir)
    {
        var result = new Sprite[framesPerDir * 8];
        foreach (var s in sprites)
        {
            if (!TryParseName(s.name, out int row, out int col)) continue;
            if (row < 0 || row >= 8 || col < 0 || col >= framesPerDir) continue;
            result[row * framesPerDir + col] = s;
        }
        return result;
    }

    private static bool TryParseName(string name, out int row, out int col)
    {
        // Expected: Idle_0_0 or Walk_3_14
        row = col = -1;
        var parts = name.Split('_');
        if (parts.Length < 3) return false;
        return int.TryParse(parts[^2], out row) && int.TryParse(parts[^1], out col);
    }

    private static void AssignToUnitPrefab(DirectionalAnimationSet animSet)
    {
        const string prefabPath = "Assets/Prefabs/Unit.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Unit.prefab not found. Skipping prefab assignment.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var unitView = root.GetComponent<UnitView>();
            if (unitView != null)
                unitView.MirrorSpriteX = false;

            var animator = root.GetComponent<UnitSpriteAnimator>();
            if (animator == null)
                animator = root.AddComponent<UnitSpriteAnimator>();
            animator.AnimSet = animSet;
            animator.TargetRenderer = root.GetComponent<SpriteRenderer>();

            // Set a default sprite so it looks correct in the editor
            if (animator.TargetRenderer != null && animSet != null && animSet.IdleFrames != null && animSet.IdleFrames.Length > 0)
                animator.TargetRenderer.sprite = animSet.IdleFrames[0];

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
