using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        MapGenerator mapGenerator = (MapGenerator)target;

        if (GUILayout.Button("Generate Map")) {
            mapGenerator.GenerateMap();
        }

        if (GUILayout.Button("Spawn Height Props")) {
            mapGenerator.GenerateHeightProps();
        }

        if (GUILayout.Button("Clear Height Props")) {
            mapGenerator.ClearAllHeightProps();
        }

        if (GUILayout.Button("Spawn Texture Props")) {
            mapGenerator.GenerateTextureProps();
        }

        if (GUILayout.Button("Clear Texture Props")) {
            mapGenerator.ClearAllTextureProps();
        }
    }
}
