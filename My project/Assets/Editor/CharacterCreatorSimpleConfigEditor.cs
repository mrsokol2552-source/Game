using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterCreatorSimpleConfig))]
public class CharacterCreatorSimpleConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var cfg = (CharacterCreatorSimpleConfig)target;
        GUILayout.Space(8);
        if (GUILayout.Button("Apply To Generator"))
        {
            cfg.ApplyToGenerator();
        }
        if (GUILayout.Button("Preview Idle"))
        {
            cfg.PreviewIdle();
        }
        if (GUILayout.Button("Clear Preview"))
        {
            cfg.ClearPreview();
        }
        if (GUILayout.Button("Generate Spritesheets"))
        {
            cfg.Generate();
        }
    }
}
