using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum TextureBasedPropPlacementType { Outer, Inner }

    [System.Serializable]
    public struct Range
    {
        public float minimum;
        public float maximum;

        public float GetRandomValue()
        {
            return Random.Range(minimum, maximum);
        }
    }

    [System.Serializable]
    public class MapProp
    {
        [Header("Prop Settings")]
        public string name;
        [Range(0f, 1f)] public float density;
        public GameObject prefab;

        [Header("Object Settings")]
        public Range height;
        public Range size;
        public Range rotation;

        public Vector3 GetRandomSize()
        {
            return Vector3.one * size.GetRandomValue();
        }

        public Quaternion GetRandomRotation()
        {
            return Quaternion.Euler(Vector3.one * rotation.GetRandomValue());
        }
    }

    [Header("General Settings")]
    [SerializeField] bool autoUpdate = true;
    [SerializeField] Texture2D mapTexture;
    [SerializeField] TextureBasedPropPlacementType textureBasedPropPlacementType = TextureBasedPropPlacementType.Inner;

    [Header("Mesh Settings")]
    [SerializeField, Range(1, 255)] int xSize = 255;
    [SerializeField, Range(1, 255)] int zSize = 255;
    [SerializeField, Range(1f, 10f)] float sizeMultiplier = 5f;
    [SerializeField] Transform meshTransform;

    [Header("Noise Settings")]
    [SerializeField, Range(1, 10)] int octaves = 6;
    [SerializeField, Range(1f, 100f)] float noiseScale = 50f;
    [SerializeField, Range(0f, 1f)] float persistence = 0.5f;
    [SerializeField, Range(0f, 2f)] float lacunarity = 2f;

    [Header("Height Settings")]
    [SerializeField] float heightMultiplier = 10f;
    [SerializeField] AnimationCurve heightCurve;
    [SerializeField] Gradient heightGradient;

    [Header("Falloff Settings")]
    [SerializeField] bool useFalloff = true;
    [SerializeField, Range(0f, 1f)] float falloffStart = 0.5f;
    [SerializeField, Range(0f, 1f)] float falloffEnd = 1f;

    [Header("Seed Settings")]
    [SerializeField] bool randomSeed;
    [SerializeField] int seed;
    [SerializeField] Vector2 offset;

    [Header("Height Based Prop Placement Settings")]
    [SerializeField] Transform heightPropHolder;
    [SerializeField] MapProp[] heightBasedProps;

    [Header("Texture Based Prop Placement Settings")]
    [SerializeField] Transform texturePropHolder;
    [SerializeField, Range(0f, 255f)] float colorRange = 20f;
    [SerializeField] Color[] selectedColors;
    [SerializeField] MapProp[] textureBasedProps;

    [Header("Paint Settings")]
    [SerializeField] Texture2D paintMap;
    [SerializeField] int paintMapSize = 128;
    [SerializeField] bool showPaintPreview = true;
    public bool removePropsWhilePainting = true;
    public float propRemovalRadius = 1f;

    float[,] noiseMap;
    float[,] falloffMap;
    Mesh mesh;
    bool isPaintMapInitialized = false;
    Material terrainMaterial;

    void Start()
    {
        GenerateMap();
        GenerateHeightProps();
        GenerateTextureProps();
        InitializePaintMap();
    }

    public void GenerateMap()
    {
        noiseMap = GenerateNoiseMap();
        falloffMap = useFalloff ? GenerateFalloffMap() : null;
        ApplyFalloffToNoiseMap();

        mesh = CreateMesh();
        ApplyMesh(mesh);

        MeshRenderer renderer = meshTransform.GetComponent<MeshRenderer>();
        if (renderer != null)
            terrainMaterial = renderer.sharedMaterial;
    }

    public void GenerateHeightProps()
    {
        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                foreach (MapProp prop in heightBasedProps)
                {
                    if (IsWithinHeightRange(x, z, prop) && Random.value <= prop.density / 10f)
                        SpawnHeightProp(x, z, prop);
                }
            }
        }
    }

    bool IsWithinHeightRange(int x, int z, MapProp prop)
    {
        float height = noiseMap[x, z];
        return height >= prop.height.minimum && height <= prop.height.maximum;
    }

    void SpawnHeightProp(int x, int z, MapProp prop)
    {
        int vertexIndex = (z * (xSize + 1)) + x;
        Vector3 position = (mesh.vertices[vertexIndex] * sizeMultiplier) + heightPropHolder.position;
        GameObject instance = Instantiate(prop.prefab, position, prop.GetRandomRotation(), heightPropHolder);
        instance.transform.localScale = prop.GetRandomSize();
    }

    void OnValidate()
    {
        if (falloffEnd < falloffStart)
            falloffEnd = falloffStart;

        if (autoUpdate)
            GenerateMap();
    }

    void ApplyFalloffToNoiseMap()
    {
        if (falloffMap == null)
            return;

        for (int z = 0; z <= zSize; z++)
            for (int x = 0; x <= xSize; x++)
                noiseMap[x, z] *= falloffMap[x, z];
    }

    float[,] GenerateNoiseMap()
    {
        float[,] map = new float[xSize + 1, zSize + 1];

        System.Random prng = new System.Random(randomSeed ? Random.Range(-100000, 100000) : seed);
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng);

        float halfXSize = xSize / 2f;
        float halfZSize = zSize / 2f;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float height = CalculatePerlinHeight(x, z, octaveOffsets, halfXSize, halfZSize, ref minHeight, ref maxHeight);
                map[x, z] = height;
            }
        }

        NormalizeNoiseMap(map, minHeight, maxHeight);
        return map;
    }

    Vector2[] GenerateOctaveOffsets(System.Random prng)
    {
        Vector2[] offsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
            offsets[i] = new Vector2(prng.Next(-100000, 100000), prng.Next(-100000, 100000)) + (offset / 10f);

        return offsets;
    }

    float CalculatePerlinHeight(int x, int z, Vector2[] octaveOffsets, float halfXSize, float halfZSize, ref float minHeight, ref float maxHeight)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float height = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = ((x - halfXSize) / noiseScale * frequency) + octaveOffsets[i].x;
            float sampleZ = ((z - halfZSize) / noiseScale * frequency) + octaveOffsets[i].y;

            float perlinValue = (Mathf.PerlinNoise(sampleX, sampleZ) * 2f) - 1f;
            height += perlinValue * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        maxHeight = Mathf.Max(maxHeight, height);
        minHeight = Mathf.Min(minHeight, height);
        return height;
    }

    void NormalizeNoiseMap(float[,] map, float minHeight, float maxHeight)
    {
        for (int z = 0; z <= zSize; z++)
            for (int x = 0; x <= xSize; x++)
                map[x, z] = Mathf.InverseLerp(minHeight, maxHeight, map[x, z]);
    }

    float[,] GenerateFalloffMap()
    {
        float[,] map = new float[xSize + 1, zSize + 1];

        for (int z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float xVal = ((float)x / xSize * 2f) - 1f;
                float zVal = ((float)z / zSize * 2f) - 1f;
                float edgeDistance = Mathf.Max(Mathf.Abs(xVal), Mathf.Abs(zVal));

                float falloffValue = Mathf.InverseLerp(falloffStart, falloffEnd, edgeDistance);
                map[x, z] = Mathf.SmoothStep(1f, 0f, falloffValue);
            }
        }

        return map;
    }

    Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[(xSize + 1) * (zSize + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[xSize * zSize * 6];
        Color[] colors = new Color[vertices.Length];

        GenerateVerticesAndUVs(vertices, uvs);
        GenerateTriangles(triangles);
        ApplyHeightGradient(vertices, colors);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;

        mesh.RecalculateNormals();
        return mesh;
    }

    void GenerateVerticesAndUVs(Vector3[] vertices, Vector2[] uvs)
    {
        for (int z = 0, i = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {
                float height = heightCurve.Evaluate(noiseMap[x, z]) * heightMultiplier;
                vertices[i] = new Vector3(x, height, z);
                uvs[i] = new Vector2((float)x / xSize, (float)z / zSize);
            }
        }
    }

    void GenerateTriangles(int[] triangles)
    {
        for (int z = 0, vert = 0, tris = 0; z < zSize; z++, vert++)
        {
            for (int x = 0; x < xSize; x++, vert++, tris += 6)
            {
                triangles[tris] = vert;
                triangles[tris + 1] = vert + xSize + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + xSize + 1;
                triangles[tris + 5] = vert + xSize + 2;
            }
        }
    }

    void ApplyHeightGradient(Vector3[] vertices, Color[] colors)
    {
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        foreach (Vector3 vertex in vertices)
        {
            minHeight = Mathf.Min(minHeight, vertex.y);
            maxHeight = Mathf.Max(maxHeight, vertex.y);
        }

        for (int i = 0; i < vertices.Length; i++)
            colors[i] = heightGradient.Evaluate(Mathf.InverseLerp(minHeight, maxHeight, vertices[i].y));
    }

    void ApplyMesh(Mesh generatedMesh)
    {
        MeshFilter meshFilter = meshTransform.GetComponent<MeshFilter>();
        MeshCollider meshCollider = meshTransform.GetComponent<MeshCollider>();

        meshFilter.sharedMesh = generatedMesh;
        meshCollider.sharedMesh = generatedMesh;

        Vector3 offsetPosition = new Vector3(-(xSize / 2f), 0f, -(zSize / 2f)) * sizeMultiplier;
        meshTransform.localPosition = offsetPosition; // Offset position

        meshTransform.localScale = Vector3.one * sizeMultiplier;

        heightPropHolder.localPosition = offsetPosition;
        texturePropHolder.localPosition = offsetPosition;
    }

    List<Vector2Int> GetNeighborOffsets(int x, int y, int textureWidth, int textureHeight)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < dx.Length; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (nx >= 0 && nx < textureWidth && ny >= 0 && ny < textureHeight)
                neighbors.Add(new Vector2Int(nx, ny));
        }

        return neighbors;
    }

    public void GenerateTextureProps()
    {
        foreach (Color color in selectedColors)
            Local_GenerateTextureProps(color);
    }

    void Local_GenerateTextureProps(Color selectedColor)
    {
        if (textureBasedPropPlacementType == TextureBasedPropPlacementType.Inner)
        {
            Color[] pixels = mapTexture.GetPixels();
            int textureWidth = mapTexture.width;
            int textureHeight = mapTexture.height;

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    int pixelIndex = (y * textureWidth) + x;
                    Color pixelColor = pixels[pixelIndex];

                    if (IsColorMatch(pixelColor, selectedColor))
                        foreach (MapProp prop in textureBasedProps)
                            if (Random.value <= prop.density / 10f)
                                SpawnTextureProp(x, y, prop);
                }
            }
        }
        else if (textureBasedPropPlacementType == TextureBasedPropPlacementType.Outer)
        {
            Color[] pixels = mapTexture.GetPixels();
            int textureWidth = mapTexture.width;
            int textureHeight = mapTexture.height;

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    int pixelIndex = (y * textureWidth) + x;
                    Color currentPixelColor = pixels[pixelIndex];

                    bool isEdge = false;
                    foreach (Vector2Int neighborOffset in GetNeighborOffsets(x, y, textureWidth, textureHeight))
                    {
                        int neighborIndex = (neighborOffset.y * textureWidth) + neighborOffset.x;
                        Color neighborColor = pixels[neighborIndex];

                        if (!IsColorMatch(currentPixelColor, neighborColor))
                        {
                            isEdge = true;
                            break;
                        }
                    }

                    if (isEdge)
                        foreach (MapProp prop in textureBasedProps)
                            if (Random.value <= prop.density / 10f)
                                SpawnTextureProp(x, y, prop);
                }
            }
        }
    }

    bool IsColorMatch(Color pixelColor, Color targetColor)
    {
        float rDiff = Mathf.Abs(pixelColor.r - targetColor.r);
        float gDiff = Mathf.Abs(pixelColor.g - targetColor.g);
        float bDiff = Mathf.Abs(pixelColor.b - targetColor.b);

        return rDiff <= colorRange / 255f && gDiff <= colorRange / 255f && bDiff <= colorRange / 255f;
    }

    void SpawnTextureProp(int x, int y, MapProp prop)
    {
        Vector3 worldPos = TextureToWorldPosition(x, y);
        GameObject instance = Instantiate(prop.prefab, worldPos, prop.GetRandomRotation(), texturePropHolder);
        instance.transform.localScale = prop.GetRandomSize();
    }

    Vector3 TextureToWorldPosition(int x, int y)
    {
        float worldX = ((float)x / mapTexture.width * xSize * sizeMultiplier) - (xSize * sizeMultiplier / 2f);
        float worldZ = ((float)y / mapTexture.height * zSize * sizeMultiplier) - (zSize * sizeMultiplier / 2f);
        float height = GetHeightFromNoiseMap(worldX, worldZ);

        return new Vector3(worldX, height, worldZ) + transform.position;
    }

    float GetHeightFromNoiseMap(float worldX, float worldZ)
    {
        float normalizedX = Mathf.InverseLerp(-(xSize / 2f), xSize / 2f, worldX / sizeMultiplier);
        float normalizedZ = Mathf.InverseLerp(-(zSize / 2f), zSize / 2f, worldZ / sizeMultiplier);

        int xCoord = Mathf.FloorToInt(normalizedX * xSize);
        int zCoord = Mathf.FloorToInt(normalizedZ * zSize);

        xCoord = Mathf.Clamp(xCoord, 0, xSize - 1);
        zCoord = Mathf.Clamp(zCoord, 0, zSize - 1);

        float baseHeight = noiseMap[xCoord, zCoord];
        return heightCurve.Evaluate(baseHeight) * heightMultiplier * sizeMultiplier;
    }

    public void ClearAllHeightProps()
    {
        ClearAllProps(heightPropHolder);
    }

    public void ClearAllTextureProps()
    {
        ClearAllProps(texturePropHolder);
    }

    void ClearAllProps(Transform propHolder)
    {
        List<Transform> children = new List<Transform>();
        foreach (Transform child in propHolder)
        {
            if (child != propHolder)
                children.Add(child);
        }

        foreach (Transform child in children)
            DestroyImmediate(child.gameObject);
    }

    public void InitializePaintMap()
    {
        if (paintMap != null)
            DestroyImmediate(paintMap);

        paintMap = new Texture2D(paintMapSize, paintMapSize, TextureFormat.RGBA32, false);
        paintMap.wrapMode = TextureWrapMode.Clamp;

        ClearPaintMap();
        isPaintMapInitialized = true;

        if (terrainMaterial != null)
        {
            if (terrainMaterial.HasProperty("_PaintMap"))
            {
                Texture existingMap = terrainMaterial.GetTexture("_PaintMap");
                if (existingMap != null)
                {
                    RenderTexture renderTex = RenderTexture.GetTemporary(
                        paintMap.width, paintMap.height, 0, RenderTextureFormat.ARGB32);

                    Graphics.Blit(existingMap, renderTex);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = renderTex;

                    paintMap.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                    paintMap.Apply();

                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(renderTex);
                }
            }
            else
            {
                Debug.LogWarning("Material does not have _PaintMap property. Make sure to use a compatible shader.");
            }
        }

        UpdateMaterial();
    }

    public void ClearPaintMap()
    {
        if (paintMap == null)
            return;

        Color[] clearColors = new Color[paintMap.width * paintMap.height];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = new Color(0, 0, 0, 0);

        paintMap.SetPixels(clearColors);
        paintMap.Apply();

        if (terrainMaterial != null && terrainMaterial.HasProperty("_PaintMap"))
            terrainMaterial.SetTexture("_PaintMap", paintMap);
    }

    public void ApplyPaintAtPosition(Vector3 worldPosition, Vector2 uv, Color paintColor, float brushRadius, float brushStrength, float brushOpacity, Texture2D brushTexture)
    {
        if (paintMap == null || !isPaintMapInitialized)
            return;

        int centerX = Mathf.FloorToInt(uv.x * paintMap.width);
        int centerY = Mathf.FloorToInt(uv.y * paintMap.height);
        int radius = Mathf.FloorToInt(brushRadius * paintMap.width / 10);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int pixelX = centerX + x;
                int pixelY = centerY + y;

                if (pixelX < 0 || pixelX >= paintMap.width || pixelY < 0 || pixelY >= paintMap.height)
                    continue;

                float distance = Mathf.Sqrt(x * x + y * y) / radius;
                if (distance > 1)
                    continue;

                float brushFactor = 1.0f;
                if (brushTexture != null)
                {
                    float brushU = (x + radius) / (float)(radius * 2);
                    float brushV = (y + radius) / (float)(radius * 2);
                    Color brushSample = brushTexture.GetPixelBilinear(brushU, brushV);
                    brushFactor = brushSample.a;
                }
                else
                {
                    brushFactor = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, distance);
                }

                Color currentColor = paintMap.GetPixel(pixelX, pixelY);
                Color newColor = Color.Lerp(
                    currentColor,
                    paintColor,
                    brushFactor * brushStrength * brushOpacity
                );

                paintMap.SetPixel(pixelX, pixelY, newColor);
            }
        }

        paintMap.Apply();
        UpdateMaterial();

        if (removePropsWhilePainting)
            RemovePropsInBrushArea(worldPosition, brushRadius * propRemovalRadius);
    }

    public void RemovePropsInBrushArea(Vector3 worldPosition, float radius)
    {
        RemovePropsInHolder(heightPropHolder, worldPosition, radius);
        RemovePropsInHolder(texturePropHolder, worldPosition, radius);
    }

    void RemovePropsInHolder(Transform propHolder, Vector3 position, float radius)
    {
        List<GameObject> propsToRemove = new List<GameObject>();

        foreach (Transform child in propHolder)
        {
            float distanceToCenter = Vector3.Distance(new Vector3(child.position.x, position.y, child.position.z), position);
            if (distanceToCenter <= radius)
                propsToRemove.Add(child.gameObject);
        }

        foreach (GameObject prop in propsToRemove)
            DestroyImmediate(prop);
    }

    void UpdateMaterial()
    {
        if (terrainMaterial == null || paintMap == null)
            return;

        if (!terrainMaterial.HasProperty("_PaintMap"))
        {
            Debug.LogWarning("Material does not have _PaintMap property. Make sure to use a compatible shader.");
            return;
        }

        terrainMaterial.SetTexture("_PaintMap", paintMap);
    }

    public void SavePaintMap(string path)
    {
        if (paintMap == null || !isPaintMapInitialized)
            return;

        byte[] bytes = paintMap.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        
        UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer != null)
        {
            importer.textureType = UnityEditor.TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        Texture2D savedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (terrainMaterial != null && terrainMaterial.HasProperty("_PaintMap"))
            terrainMaterial.SetTexture("_PaintMap", savedTexture);
            
        Debug.Log("Paint map saved to: " + path);
#endif
    }
}
