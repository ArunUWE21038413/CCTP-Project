using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    enum EditorMode { Generation, Painting }
    EditorMode currentMode = EditorMode.Generation;

    float brushRadius = 0.5f;
    float brushStrength = 0.8f;
    float brushOpacity = 0.8f;
    Color brushColor = Color.red;
    Texture2D brushTexture;

    bool isPainting = false;
    Vector3 lastPaintPosition;
    bool showBrushSettings = true;
    bool showPaintMapSettings = true;
    bool showPropRemovalSettings = true;

    public override void OnInspectorGUI()
    {
        MapGenerator mapGenerator = (MapGenerator)target;

        EditorGUILayout.Space();
        currentMode = (EditorMode)GUILayout.Toolbar((int)currentMode, new string[] { "Map Generation", "Terrain Painting" });
        EditorGUILayout.Space();

        if (currentMode == EditorMode.Generation)
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Map Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Map"))
                mapGenerator.GenerateMap();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prop Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Height Props"))
                mapGenerator.GenerateHeightProps();
            if (GUILayout.Button("Clear Height Props"))
                mapGenerator.ClearAllHeightProps();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Texture Props"))
                mapGenerator.GenerateTextureProps();
            if (GUILayout.Button("Clear Texture Props"))
                mapGenerator.ClearAllTextureProps();
            EditorGUILayout.EndHorizontal();
        }
        else if (currentMode == EditorMode.Painting)
        {
            showPropRemovalSettings = EditorGUILayout.Foldout(showPropRemovalSettings, "Prop Removal Settings", true);
            if (showPropRemovalSettings)
            {
                EditorGUI.indentLevel++;
                SerializedProperty removePropsWhilePainting = serializedObject.FindProperty("removePropsWhilePainting");
                EditorGUILayout.PropertyField(removePropsWhilePainting, new GUIContent("Remove Props While Painting"));

                if (removePropsWhilePainting.boolValue)
                {
                    SerializedProperty propRemovalRadius = serializedObject.FindProperty("propRemovalRadius");
                    EditorGUILayout.PropertyField(propRemovalRadius, new GUIContent("Prop Removal Radius Multiplier"));
                }
                EditorGUI.indentLevel--;
            }

            showBrushSettings = EditorGUILayout.Foldout(showBrushSettings, "Brush Settings", true);
            if (showBrushSettings)
            {
                EditorGUI.indentLevel++;
                brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.1f, 20.0f);
                brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.0f, 1.0f);
                brushOpacity = EditorGUILayout.Slider("Brush Opacity", brushOpacity, 0.0f, 1.0f);
                brushColor = EditorGUILayout.ColorField("Brush Color", brushColor);
                brushTexture = (Texture2D)EditorGUILayout.ObjectField("Brush Texture", brushTexture, typeof(Texture2D), false);
                EditorGUI.indentLevel--;
            }

            showPaintMapSettings = EditorGUILayout.Foldout(showPaintMapSettings, "Paint Map Settings", true);
            if (showPaintMapSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Initialize Paint Map"))
                    mapGenerator.InitializePaintMap();
                if (GUILayout.Button("Clear Paint"))
                    mapGenerator.ClearPaintMap();
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Save Paint Map"))
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save Paint Map",
                        "TerrainPaintMap",
                        "png",
                        "Choose where to save the paint map."
                    );

                    if (!string.IsNullOrEmpty(path))
                        mapGenerator.SavePaintMap(path);
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(
                "Click and drag on the terrain to paint.\n" +
                "Hold Shift to sample color under cursor.\n" +
                "Use the brush settings to customize your painting.\n" +
                (mapGenerator.removePropsWhilePainting ? "Props will be removed as you paint." : ""),
                MessageType.Info
            );
        }
    }

    void OnSceneGUI()
    {
        if (currentMode != EditorMode.Painting)
            return;

        MapGenerator mapGenerator = (MapGenerator)target;

        Event e = Event.current;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform.GetComponent<MeshFilter>() != null && hit.transform.GetComponent<MeshRenderer>() != null)
            {
                Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.2f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushRadius);
                Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.8f);
                Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);

                if (mapGenerator.removePropsWhilePainting)
                {
                    Handles.color = new Color(1f, 0.3f, 0.3f, 0.2f);
                    Handles.DrawSolidDisc(hit.point, hit.normal, brushRadius * mapGenerator.propRemovalRadius);
                    Handles.color = new Color(1f, 0.3f, 0.3f, 0.5f);
                    Handles.DrawWireDisc(hit.point, hit.normal, brushRadius * mapGenerator.propRemovalRadius);
                }

                HandleMousePaintingEvents(e, hit, mapGenerator);

                if (e.type == EventType.Layout)
                    HandleUtility.Repaint();
            }
        }

        if (isPainting && e.type == EventType.MouseDrag)
            e.Use();
    }

    void HandleMousePaintingEvents(Event e, RaycastHit hit, MapGenerator mapGenerator)
    {
        if (e.shift)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Renderer renderer = hit.transform.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    Texture paintMapTexture = null;
                    if (renderer.sharedMaterial.HasProperty("_PaintMap"))
                        paintMapTexture = renderer.sharedMaterial.GetTexture("_PaintMap");

                    if (paintMapTexture != null && paintMapTexture is Texture2D)
                    {
                        Vector2 uv = hit.textureCoord;

                        RenderTexture tempRT = RenderTexture.GetTemporary(
                            paintMapTexture.width,
                            paintMapTexture.height,
                            0,
                            RenderTextureFormat.ARGB32
                        );

                        Graphics.Blit(paintMapTexture, tempRT);

                        RenderTexture prevRT = RenderTexture.active;
                        RenderTexture.active = tempRT;

                        Texture2D tempTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                        int x = Mathf.FloorToInt(uv.x * tempRT.width);
                        int y = Mathf.FloorToInt(uv.y * tempRT.height);
                        tempTex.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                        tempTex.Apply();

                        Color sampledColor = tempTex.GetPixel(0, 0);

                        RenderTexture.active = prevRT;
                        RenderTexture.ReleaseTemporary(tempRT);
                        Object.DestroyImmediate(tempTex);

                        if (sampledColor.a > 0)
                        {
                            brushColor = sampledColor;
                            e.Use();
                        }
                    }
                }
            }
            return;
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    isPainting = true;
                    ApplyPaintAtPosition(hit, mapGenerator);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isPainting && e.button == 0)
                {
                    if (lastPaintPosition != Vector3.zero && Vector3.Distance(lastPaintPosition, hit.point) > brushRadius * 0.5f)
                        InterpolatePaintingBetweenPoints(lastPaintPosition, hit.point, mapGenerator);
                    else
                        ApplyPaintAtPosition(hit, mapGenerator);

                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0)
                {
                    isPainting = false;
                    lastPaintPosition = Vector3.zero;
                    e.Use();
                }
                break;
        }
    }

    void ApplyPaintAtPosition(RaycastHit hit, MapGenerator mapGenerator)
    {
        Vector2 uv = hit.textureCoord;

        mapGenerator.ApplyPaintAtPosition(
            hit.point,
            uv,
            brushColor,
            brushRadius,
            brushStrength,
            brushOpacity,
            brushTexture
        );

        lastPaintPosition = hit.point;

        SceneView.RepaintAll();
    }

    void InterpolatePaintingBetweenPoints(Vector3 from, Vector3 to, MapGenerator mapGenerator)
    {
        float distance = Vector3.Distance(from, to);

        if (distance < 0.1f)
            return;

        int steps = Mathf.CeilToInt(distance / (brushRadius * 0.5f));
        steps = Mathf.Clamp(steps, 2, 20);

        for (int i = 1; i < steps; i++)
        {
            Vector3 interpolatedPosition = Vector3.Lerp(from, to, (float)i / steps);

            Ray ray = new Ray(interpolatedPosition + Vector3.up * 50f, Vector3.down);
            RaycastHit interpolatedHit;

            if (Physics.Raycast(ray, out interpolatedHit, 100f))
                ApplyPaintAtPosition(interpolatedHit, mapGenerator);
        }
    }

    void OnDisable()
    {
        isPainting = false;
        lastPaintPosition = Vector3.zero;
    }
}
