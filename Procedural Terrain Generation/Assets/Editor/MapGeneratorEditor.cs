using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        MapGenerator mapGenerator = (MapGenerator)target;

        if (GUILayout.Button("Generate Terrain")) {
            mapGenerator.GenerateMap();
        }

        if (GUILayout.Button("Spawn Height-Based Props")) {
            mapGenerator.GenerateHeightProps();
        }

        if (GUILayout.Button("Spawn Texture-Based Props")) {
            mapGenerator.GenerateTextureProps();
        }

        if (GUILayout.Button("Clear All Props")) {
            mapGenerator.ClearAllProps();
        }
    }
}
