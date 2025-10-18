using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SpriteImportTools
{
    [MenuItem("Tools/RTS/Sprites/Import From Repo")] 
    public static void ImportFromRepo()
    {
        try
        {
            string projectDir = Directory.GetParent(Application.dataPath).FullName; // .../Game/My project
            string repoRoot = Directory.GetParent(projectDir).FullName;             // .../Game
            string srcRoot = Path.Combine(repoRoot, "Sprites");
            if (!Directory.Exists(srcRoot))
            {
                EditorUtility.DisplayDialog("Sprite Import", $"Source folder not found:\n{srcRoot}", "OK");
                return;
            }

            string dstRootAbs = Path.Combine(Application.dataPath, "Sprites/Imported");
            string dstRootAsset = "Assets/Sprites/Imported";
            Directory.CreateDirectory(dstRootAbs);

            var copiedAssets = new List<string>();
            foreach (var file in Directory.GetFiles(srcRoot, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                // Build destination relative path preserving subfolders
                string rel = file.Substring(srcRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dstAbs = Path.Combine(dstRootAbs, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dstAbs));
                File.Copy(file, dstAbs, true);

                string assetPath = "Assets" + dstAbs.Substring(Application.dataPath.Length).Replace('\\', '/');
                copiedAssets.Add(assetPath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int changed = 0;
            foreach (var assetPath in copiedAssets)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
#if UNITY_2020_2_OR_NEWER
                importer.spriteImportMode = SpriteImportMode.Single;
#endif
                importer.SaveAndReimport();
                changed++;
            }

            EditorUtility.DisplayDialog("Sprite Import", $"Imported {copiedAssets.Count} sprite(s).\nUpdated import settings for {changed} asset(s).\n\nDestination: {dstRootAsset}", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpriteImportTools] Import failed: {ex}");
            EditorUtility.DisplayDialog("Sprite Import", $"Import failed:\n{ex.Message}", "OK");
        }
    }
}

