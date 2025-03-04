using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    // Enum to define where props should be placed based on texture color (Outer or Inner)
    public enum TextureBasedPropPlacementType { Outer, Inner }

    // Structure for defining a range of values (e.g., height, size, rotation)
    [System.Serializable]
    public struct Range
    {
        public float minimum;
        public float maximum;

        // Returns a random value within the defined range
        public float GetRandomValue()
        {
            return Random.Range(minimum, maximum);
        }
    }

    // Class to define settings for map props (trees, rocks, etc.)
    [System.Serializable]
    public class MapProp
    {
        [Header("Prop Settings")]
        public string name;  // Name of the prop
        [Range(0f, 1f)] public float density; // Determines how frequently the prop appears
        public GameObject prefab; // The object to spawn

        [Header("Object Settings")]
        public Range height; // Height range of the prop
        public Range size; // Size range of the prop
        public Range rotation; // Rotation range of the prop

        // Returns a random size for the prop
        public Vector3 GetRandomSize()
        {
            return Vector3.one * size.GetRandomValue();
        }

        // Returns a random rotation for the prop
        public Quaternion GetRandomRotation()
        {
            return Quaternion.Euler(Vector3.one * rotation.GetRandomValue());
        }
    }

    // ========== General Settings ==========
    [Header("General Settings")]
    [SerializeField] bool autoUpdate = true; // Auto-generates map when settings change
    [SerializeField] Texture2D mapTexture; // Texture used for prop placement
    [SerializeField] TextureBasedPropPlacementType textureBasedPropPlacementType = TextureBasedPropPlacementType.Inner; // Placement type for texture-based props

    // ========== Mesh Settings ==========
    [Header("Mesh Settings")]
    [SerializeField, Range(1, 255)] int xSize = 255; // Width of the terrain
    [SerializeField, Range(1, 255)] int zSize = 255; // Depth of the terrain
    [SerializeField, Range(1f, 10f)] float sizeMultiplier = 5f; // Scale multiplier for the terrain
    [SerializeField] Transform meshTransform; // Transform that holds the terrain mesh

    // ========== Noise Settings ==========
    [Header("Noise Settings")]
    [SerializeField, Range(1, 10)] int octaves = 6; // Number of layers in the noise generation
    [SerializeField, Range(1f, 100f)] float noiseScale = 50f; // Controls terrain detail
    [SerializeField, Range(0f, 1f)] float persistence = 0.5f; // Controls amplitude of each octave
    [SerializeField, Range(0f, 2f)] float lacunarity = 2f; // Controls frequency of each octave

    // ========== Height Settings ==========
    [Header("Height Settings")]
    [SerializeField] float heightMultiplier = 10f; // Multiplier for terrain height
    [SerializeField] AnimationCurve heightCurve; // Curve to modify height variation
    [SerializeField] Gradient heightGradient; // Gradient for coloring terrain based on height

    // ========== Falloff Settings ==========
    [Header("Falloff Settings")]
    [SerializeField] bool useFalloff = true; // Enables falloff effect
    [SerializeField, Range(0f, 1f)] float falloffStart = 0.5f; // Start intensity for falloff
    [SerializeField, Range(0f, 1f)] float falloffEnd = 1f; // End intensity for falloff

    // ========== Seed Settings ==========
    [Header("Seed Settings")]
    [SerializeField] bool randomSeed; // If true, generates a random seed
    [SerializeField] int seed; // Manual seed value
    [SerializeField] Vector2 offset; // Offset for noise generation

    // ========== Prop Placement Settings ==========
    [Header("Height Based Prop Placement Settings")]
    [SerializeField] Transform heightPropHolder; // Parent object for height-based props
    [SerializeField] MapProp[] heightBasedProps; // Props placed based on height

    [Header("Texture Based Prop Placement Settings")]
    [SerializeField] Transform texturePropHolder; // Parent object for texture-based props
    [SerializeField, Range(0f, 255f)] float colorRange = 20f; // Color range for matching texture colors
    [SerializeField] Color[] selectedColors; // Colors used to place texture-based props
    [SerializeField] MapProp[] textureBasedProps; // Props placed based on texture color

    // ========== Paint Settings ==========
    [Header("Paint Settings")]
    [SerializeField] Texture2D paintMap; // Stores paint information for terrain
    [SerializeField] int paintMapSize = 128; // Resolution of the paint map
    [SerializeField] bool showPaintPreview = true; // Toggles paint preview
    public bool removePropsWhilePainting = true; // Removes props if painting over them
    public float propRemovalRadius = 1f; // Determines how much area is cleared when painting

    // ========== Internal Variables ==========
    float[,] noiseMap; // Stores terrain height data
    float[,] falloffMap; // Stores falloff effect data
    Mesh mesh; // Stores generated mesh
    bool isPaintMapInitialized = false; // Tracks if paint map is ready
    Material terrainMaterial; // Stores the terrain material reference

    // ========== Unity Methods ==========
    void Start()
    {
        GenerateMap();
        GenerateHeightProps();
        GenerateTextureProps();
        InitializePaintMap();
    }

    // ========== Map Generation Methods ==========
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

    // ========== Prop Generation Methods ==========
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

    // Checks if a location is within the allowed height range for a prop
    bool IsWithinHeightRange(int x, int z, MapProp prop)
    {
        float height = noiseMap[x, z];
        return height >= prop.height.minimum && height <= prop.height.maximum;
    }

    // Spawns a height-based prop at the given position
    void SpawnHeightProp(int x, int z, MapProp prop)
    {
        int vertexIndex = (z * (xSize + 1)) + x;
        Vector3 position = (mesh.vertices[vertexIndex] * sizeMultiplier) + heightPropHolder.position;
        GameObject instance = Instantiate(prop.prefab, position, prop.GetRandomRotation(), heightPropHolder);
        instance.transform.localScale = prop.GetRandomSize();
    }

    // Ensures values are valid and auto-updates the map when changes occur in the inspector
    void OnValidate()
    {
        // Prevents falloffEnd from being smaller than falloffStart
        if (falloffEnd < falloffStart)
            falloffEnd = falloffStart;

        // Automatically regenerates the map when any values are modified in the inspector
        if (autoUpdate)
            GenerateMap();
    }

    // Applies the falloff effect to the noise map (used to create island-like terrain)
    void ApplyFalloffToNoiseMap()
    {
        if (falloffMap == null)
            return; // Exits if the falloff map is not generated

        // Loops through each coordinate and applies the falloff effect
        for (int z = 0; z <= zSize; z++)
            for (int x = 0; x <= xSize; x++)
                noiseMap[x, z] *= falloffMap[x, z]; // Multiplies noise values by the falloff map values
    }

    // Generates a Perlin noise map for terrain height calculation
    float[,] GenerateNoiseMap()
    {
        float[,] map = new float[xSize + 1, zSize + 1]; // Creates a 2D array to store height values

        // Generates a random seed if the user chooses, otherwise uses a fixed seed
        System.Random prng = new System.Random(randomSeed ? Random.Range(-100000, 100000) : seed);
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng); // Generates random offsets for Perlin noise

        float halfXSize = xSize / 2f;
        float halfZSize = zSize / 2f;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Loops through every coordinate and calculates height based on Perlin noise
        for (int z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                float height = CalculatePerlinHeight(x, z, octaveOffsets, halfXSize, halfZSize, ref minHeight, ref maxHeight);
                map[x, z] = height;
            }
        }

        NormalizeNoiseMap(map, minHeight, maxHeight); // Normalizes the height values between 0 and 1
        return map;
    }

    // Generates random offsets for Perlin noise octaves to add variation in terrain
    Vector2[] GenerateOctaveOffsets(System.Random prng)
    {
        Vector2[] offsets = new Vector2[octaves];

        // Generates random offsets for each octave to avoid repetitive patterns
        for (int i = 0; i < octaves; i++)
            offsets[i] = new Vector2(prng.Next(-100000, 100000), prng.Next(-100000, 100000)) + (offset / 10f);

        return offsets;
    }

    // Calculates Perlin noise height for a given coordinate using multiple octaves
    float CalculatePerlinHeight(int x, int z, Vector2[] octaveOffsets, float halfXSize, float halfZSize, ref float minHeight, ref float maxHeight)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float height = 0f;

        for (int i = 0; i < octaves; i++)
        {
            // Calculates the sample points for Perlin noise
            float sampleX = ((x - halfXSize) / noiseScale * frequency) + octaveOffsets[i].x;
            float sampleZ = ((z - halfZSize) / noiseScale * frequency) + octaveOffsets[i].y;

            // Gets Perlin noise value and maps it between -1 and 1
            float perlinValue = (Mathf.PerlinNoise(sampleX, sampleZ) * 2f) - 1f;
            height += perlinValue * amplitude; // Adds height contribution from this octave

            amplitude *= persistence; // Reduces amplitude for the next octave
            frequency *= lacunarity; // Increases frequency for more detail
        }

        // Keeps track of min and max height for normalization
        maxHeight = Mathf.Max(maxHeight, height);
        minHeight = Mathf.Min(minHeight, height);

        return height;
    }

    // Normalizes height values to be between 0 and 1
    void NormalizeNoiseMap(float[,] map, float minHeight, float maxHeight)
    {
        for (int z = 0; z <= zSize; z++)
            for (int x = 0; x <= xSize; x++)
                map[x, z] = Mathf.InverseLerp(minHeight, maxHeight, map[x, z]);
    }

    // Generates a falloff map to smoothly transition terrain height at the edges
    float[,] GenerateFalloffMap()
    {
        float[,] map = new float[xSize + 1, zSize + 1];

        for (int z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                // Normalizes x and z to be between -1 and 1
                float xVal = ((float)x / xSize * 2f) - 1f;
                float zVal = ((float)z / zSize * 2f) - 1f;
                float edgeDistance = Mathf.Max(Mathf.Abs(xVal), Mathf.Abs(zVal)); // Determines the edge distance

                // Smoothly interpolates the falloff effect
                float falloffValue = Mathf.InverseLerp(falloffStart, falloffEnd, edgeDistance);
                map[x, z] = Mathf.SmoothStep(1f, 0f, falloffValue);
            }
        }

        return map;
    }

    // Generates the terrain mesh based on the noise map
    Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();

        // Creates arrays for storing mesh data
        Vector3[] vertices = new Vector3[(xSize + 1) * (zSize + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[xSize * zSize * 6];
        Color[] colors = new Color[vertices.Length];

        // Populates the mesh data
        GenerateVerticesAndUVs(vertices, uvs);
        GenerateTriangles(triangles);
        ApplyHeightGradient(vertices, colors);

        // Assigns the generated data to the mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;

        mesh.RecalculateNormals(); // Ensures proper lighting and shading
        return mesh;
    }

    // Generates terrain vertices and assigns texture UV coordinates
    void GenerateVerticesAndUVs(Vector3[] vertices, Vector2[] uvs)
    {
        for (int z = 0, i = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++, i++)
            {
                // Gets height from noise map and applies height curve and multiplier
                float height = heightCurve.Evaluate(noiseMap[x, z]) * heightMultiplier;

                // Sets vertex position
                vertices[i] = new Vector3(x, height, z);

                // Assigns UV coordinates for textures
                uvs[i] = new Vector2((float)x / xSize, (float)z / zSize);
            }
        }
    }

    // Generates the triangles that define the mesh faces
    void GenerateTriangles(int[] triangles)
    {
        for (int z = 0, vert = 0, tris = 0; z < zSize; z++, vert++)
        {
            for (int x = 0; x < xSize; x++, vert++, tris += 6)
            {
                // Defines two triangles for each quad in the grid
                triangles[tris] = vert;
                triangles[tris + 1] = vert + xSize + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + xSize + 1;
                triangles[tris + 5] = vert + xSize + 2;
            }
        }
    }

    // Applies a height-based color gradient to the terrain vertices
    void ApplyHeightGradient(Vector3[] vertices, Color[] colors)
    {
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Finds the min and max height values in the mesh
        foreach (Vector3 vertex in vertices)
        {
            minHeight = Mathf.Min(minHeight, vertex.y);
            maxHeight = Mathf.Max(maxHeight, vertex.y);
        }

        // Assigns colors based on height using a gradient
        for (int i = 0; i < vertices.Length; i++)
            colors[i] = heightGradient.Evaluate(Mathf.InverseLerp(minHeight, maxHeight, vertices[i].y));
    }

    // Applies the generated mesh to the terrain GameObject
    void ApplyMesh(Mesh generatedMesh)
    {
        // Retrieves the mesh filter and collider components
        MeshFilter meshFilter = meshTransform.GetComponent<MeshFilter>();
        MeshCollider meshCollider = meshTransform.GetComponent<MeshCollider>();

        // Assigns the generated mesh to the components
        meshFilter.sharedMesh = generatedMesh;
        meshCollider.sharedMesh = generatedMesh;

        // Offsets the terrain to center it correctly
        Vector3 offsetPosition = new Vector3(-(xSize / 2f), 0f, -(zSize / 2f)) * sizeMultiplier;
        meshTransform.localPosition = offsetPosition;

        // Applies the size multiplier to the terrain scale
        meshTransform.localScale = Vector3.one * sizeMultiplier;

        // Also adjusts the prop holders to match the terrain's offset
        heightPropHolder.localPosition = offsetPosition;
        texturePropHolder.localPosition = offsetPosition;
    }

    // Returns a list of neighboring pixels for texture-based prop placement
    List<Vector2Int> GetNeighborOffsets(int x, int y, int textureWidth, int textureHeight)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        int[] dx = { -1, 1, 0, 0 }; // X offsets for adjacent pixels
        int[] dy = { 0, 0, -1, 1 }; // Y offsets for adjacent pixels

        for (int i = 0; i < dx.Length; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            // Ensures the neighbor is within bounds
            if (nx >= 0 && nx < textureWidth && ny >= 0 && ny < textureHeight)
                neighbors.Add(new Vector2Int(nx, ny));
        }

        return neighbors;
    }

    // Generates texture-based props by checking selected colors in the texture
    public void GenerateTextureProps()
    {
        // Loops through all selected colors and places props accordingly
        foreach (Color color in selectedColors)
            Local_GenerateTextureProps(color);
    }

    // Places props based on the given texture color and placement type (Inner or Outer)
    void Local_GenerateTextureProps(Color selectedColor)
    {
        Color[] pixels = mapTexture.GetPixels(); // Gets all pixels from the texture
        int textureWidth = mapTexture.width;
        int textureHeight = mapTexture.height;

        // If placement type is "Inner" (props placed where color matches)
        if (textureBasedPropPlacementType == TextureBasedPropPlacementType.Inner)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    int pixelIndex = (y * textureWidth) + x;
                    Color pixelColor = pixels[pixelIndex];

                    // If the pixel color matches the selected color, spawn a prop
                    if (IsColorMatch(pixelColor, selectedColor))
                    {
                        foreach (MapProp prop in textureBasedProps)
                        {
                            // Random chance based on density to spawn the prop
                            if (Random.value <= prop.density / 10f)
                                SpawnTextureProp(x, y, prop);
                        }
                    }
                }
            }
        }
        // If placement type is "Outer" (props placed at the edge of color regions)
        else if (textureBasedPropPlacementType == TextureBasedPropPlacementType.Outer)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    int pixelIndex = (y * textureWidth) + x;
                    Color currentPixelColor = pixels[pixelIndex];

                    bool isEdge = false;

                    // Checks neighboring pixels to determine if the current pixel is an edge
                    foreach (Vector2Int neighborOffset in GetNeighborOffsets(x, y, textureWidth, textureHeight))
                    {
                        int neighborIndex = (neighborOffset.y * textureWidth) + neighborOffset.x;
                        Color neighborColor = pixels[neighborIndex];

                        // If the neighbor color is different, mark this as an edge pixel
                        if (!IsColorMatch(currentPixelColor, neighborColor))
                        {
                            isEdge = true;
                            break;
                        }
                    }

                    // If this pixel is at an edge, spawn a prop with a chance based on density
                    if (isEdge)
                    {
                        foreach (MapProp prop in textureBasedProps)
                        {
                            if (Random.value <= prop.density / 10f)
                                SpawnTextureProp(x, y, prop);
                        }
                    }
                }
            }
        }
    }

    // Compares two colors and checks if they are within the allowed color range
    bool IsColorMatch(Color pixelColor, Color targetColor)
    {
        float rDiff = Mathf.Abs(pixelColor.r - targetColor.r);
        float gDiff = Mathf.Abs(pixelColor.g - targetColor.g);
        float bDiff = Mathf.Abs(pixelColor.b - targetColor.b);

        // Returns true if the color difference is within the defined threshold
        return rDiff <= colorRange / 255f && gDiff <= colorRange / 255f && bDiff <= colorRange / 255f;
    }

    // Spawns a prop at a world position converted from texture coordinates
    void SpawnTextureProp(int x, int y, MapProp prop)
    {
        Vector3 worldPos = TextureToWorldPosition(x, y); // Converts texture coordinates to world position
        GameObject instance = Instantiate(prop.prefab, worldPos, prop.GetRandomRotation(), texturePropHolder);
        instance.transform.localScale = prop.GetRandomSize(); // Sets random scale for variation
    }

    // Converts texture pixel coordinates into a world position for prop placement
    Vector3 TextureToWorldPosition(int x, int y)
    {
        // Converts texture coordinates to world space while keeping it centered
        float worldX = ((float)x / mapTexture.width * xSize * sizeMultiplier) - (xSize * sizeMultiplier / 2f);
        float worldZ = ((float)y / mapTexture.height * zSize * sizeMultiplier) - (zSize * sizeMultiplier / 2f);

        // Gets the height value from the noise map to position the prop correctly
        float height = GetHeightFromNoiseMap(worldX, worldZ);

        // Returns the final position with proper height adjustment
        return new Vector3(worldX, height, worldZ) + transform.position;
    }

    // Retrieves the height of a specific world position from the noise map
    float GetHeightFromNoiseMap(float worldX, float worldZ)
    {
        // Normalizes world coordinates to map them to the noise map range
        float normalizedX = Mathf.InverseLerp(-(xSize / 2f), xSize / 2f, worldX / sizeMultiplier);
        float normalizedZ = Mathf.InverseLerp(-(zSize / 2f), zSize / 2f, worldZ / sizeMultiplier);

        // Converts normalized coordinates to discrete grid indices
        int xCoord = Mathf.FloorToInt(normalizedX * xSize);
        int zCoord = Mathf.FloorToInt(normalizedZ * zSize);

        // Ensures coordinates are within bounds
        xCoord = Mathf.Clamp(xCoord, 0, xSize - 1);
        zCoord = Mathf.Clamp(zCoord, 0, zSize - 1);

        // Retrieves height from noise map and applies height curve and multiplier
        float baseHeight = noiseMap[xCoord, zCoord];
        return heightCurve.Evaluate(baseHeight) * heightMultiplier * sizeMultiplier;
    }

    // Clears all height-based props from the scene
    public void ClearAllHeightProps()
    {
        ClearAllProps(heightPropHolder);
    }

    // Clears all texture-based props from the scene
    public void ClearAllTextureProps()
    {
        ClearAllProps(texturePropHolder);
    }

    // Destroys all child objects under the given prop holder
    void ClearAllProps(Transform propHolder)
    {
        foreach (Transform child in propHolder)
            DestroyImmediate(child.gameObject);
    }


    // Initializes the paint map for terrain modifications
    public void InitializePaintMap()
    {
        // If a paint map already exists, destroy it before creating a new one
        if (paintMap != null)
            DestroyImmediate(paintMap);

        // Creates a new blank texture for painting
        paintMap = new Texture2D(paintMapSize, paintMapSize, TextureFormat.RGBA32, false);
        paintMap.wrapMode = TextureWrapMode.Clamp; // Ensures the texture does not repeat at edges

        ClearPaintMap(); // Clears the texture (sets it to transparent)
        isPaintMapInitialized = true; // Marks the paint map as initialized

        // Checks if the terrain material is valid
        if (terrainMaterial != null)
        {
            // Ensures the material has a "_PaintMap" property (to store the painted texture)
            if (terrainMaterial.HasProperty("_PaintMap"))
            {
                // Retrieves the existing paint texture from the material
                Texture existingMap = terrainMaterial.GetTexture("_PaintMap");
                if (existingMap != null)
                {
                    // Creates a temporary RenderTexture to copy the existing texture data
                    RenderTexture renderTex = RenderTexture.GetTemporary(
                        paintMap.width, paintMap.height, 0, RenderTextureFormat.ARGB32);

                    Graphics.Blit(existingMap, renderTex); // Copies the existing texture to the render texture
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = renderTex;

                    // Reads pixels from the render texture into the paint map
                    paintMap.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                    paintMap.Apply(); // Applies changes to the texture

                    // Restores the previous render texture and releases the temporary one
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(renderTex);
                }
            }
            else
            {
                // Warns if the material does not support painting
                Debug.LogWarning("Material does not have _PaintMap property. Make sure to use a compatible shader.");
            }
        }

        UpdateMaterial(); // Updates the terrain material with the new paint map
    }

    // Clears the paint map by setting all pixels to transparent
    public void ClearPaintMap()
    {
        if (paintMap == null)
            return;

        // Creates an array of transparent colors
        Color[] clearColors = new Color[paintMap.width * paintMap.height];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = new Color(0, 0, 0, 0); // Transparent

        paintMap.SetPixels(clearColors); // Applies the clear colors
        paintMap.Apply(); // Updates the texture

        // Assigns the cleared paint map to the terrain material (if applicable)
        if (terrainMaterial != null && terrainMaterial.HasProperty("_PaintMap"))
            terrainMaterial.SetTexture("_PaintMap", paintMap);
    }

    // Applies paint to a specific position on the paint map
    public void ApplyPaintAtPosition(Vector3 worldPosition, Vector2 uv, Color paintColor, float brushRadius, float brushStrength, float brushOpacity, Texture2D brushTexture)
    {
        if (paintMap == null || !isPaintMapInitialized)
            return;

        // Converts UV coordinates into pixel coordinates
        int centerX = Mathf.FloorToInt(uv.x * paintMap.width);
        int centerY = Mathf.FloorToInt(uv.y * paintMap.height);
        int radius = Mathf.FloorToInt(brushRadius * paintMap.width / 10); // Adjusts radius based on texture size

        // Loops through pixels within the brush radius
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int pixelX = centerX + x;
                int pixelY = centerY + y;

                // Ensures the pixel is within the texture bounds
                if (pixelX < 0 || pixelX >= paintMap.width || pixelY < 0 || pixelY >= paintMap.height)
                    continue;

                // Calculates the distance of the current pixel from the brush center
                float distance = Mathf.Sqrt(x * x + y * y) / radius;
                if (distance > 1)
                    continue;

                float brushFactor = 1.0f; // Determines paint strength
                if (brushTexture != null)
                {
                    // Samples the brush texture for alpha transparency
                    float brushU = (x + radius) / (float)(radius * 2);
                    float brushV = (y + radius) / (float)(radius * 2);
                    Color brushSample = brushTexture.GetPixelBilinear(brushU, brushV);
                    brushFactor = brushSample.a; // Uses brush alpha for smooth edges
                }
                else
                {
                    // Uses a smooth step function for a soft brush effect
                    brushFactor = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, distance);
                }

                // Blends the current paint color with the existing texture color
                Color currentColor = paintMap.GetPixel(pixelX, pixelY);
                Color newColor = Color.Lerp(
                    currentColor,
                    paintColor,
                    brushFactor * brushStrength * brushOpacity
                );

                paintMap.SetPixel(pixelX, pixelY, newColor); // Applies the new color
            }
        }

        paintMap.Apply(); // Updates the texture
        UpdateMaterial(); // Updates the terrain material

        // Removes props in the painted area if enabled
        if (removePropsWhilePainting)
            RemovePropsInBrushArea(worldPosition, brushRadius * propRemovalRadius);
    }

    // Removes props from the terrain within a given brush area
    public void RemovePropsInBrushArea(Vector3 worldPosition, float radius)
    {
        RemovePropsInHolder(heightPropHolder, worldPosition, radius);
        RemovePropsInHolder(texturePropHolder, worldPosition, radius);
    }

    // Removes props from a specific holder (either height-based or texture-based props)
    void RemovePropsInHolder(Transform propHolder, Vector3 position, float radius)
    {
        List<GameObject> propsToRemove = new List<GameObject>();

        // Loops through each child object (prop) within the holder
        foreach (Transform child in propHolder)
        {
            // Calculates the distance between the prop and the center of the brush
            float distanceToCenter = Vector3.Distance(new Vector3(child.position.x, position.y, child.position.z), position);

            // If the prop is within the brush radius, mark it for removal
            if (distanceToCenter <= radius)
                propsToRemove.Add(child.gameObject);
        }

        // Destroys all marked props
        foreach (GameObject prop in propsToRemove)
            DestroyImmediate(prop);
    }

    // Updates the terrain material with the current paint map texture
    void UpdateMaterial()
    {
        if (terrainMaterial == null || paintMap == null)
            return;

        // Ensures the material supports paint mapping
        if (!terrainMaterial.HasProperty("_PaintMap"))
        {
            Debug.LogWarning("Material does not have _PaintMap property. Make sure to use a compatible shader.");
            return;
        }

        terrainMaterial.SetTexture("_PaintMap", paintMap); // Assigns the paint map to the material
    }

    // Saves the current paint map as a PNG file
    public void SavePaintMap(string path)
    {
        if (paintMap == null || !isPaintMapInitialized)
            return;

        // Converts the paint map to a PNG file
        byte[] bytes = paintMap.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes); // Saves the file to disk

#if UNITY_EDITOR
    // Refreshes the Unity Asset Database to recognize the new file
    UnityEditor.AssetDatabase.Refresh();
    
    // Ensures the saved texture is properly imported in Unity
    UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
    if (importer != null)
    {
        importer.textureType = UnityEditor.TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    // Loads the saved texture back into Unity
    Texture2D savedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    // Updates the material with the saved texture
    if (terrainMaterial != null && terrainMaterial.HasProperty("_PaintMap"))
        terrainMaterial.SetTexture("_PaintMap", savedTexture);
        
    Debug.Log("Paint map saved to: " + path);
#endif
    }

}