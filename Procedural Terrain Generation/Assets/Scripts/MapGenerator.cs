using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour {
    public enum TextureBasedPropPlacementType { Outer, Inner }

    [System.Serializable]
    public struct Range {
        public float minimum; // minimum value for the range
        public float maximum; // maximum value for the range

        // Generates a random value within the range
        public float GetRandomValue() => Random.Range(minimum, maximum);
    }

    [System.Serializable]
    public class MapProp {
        [Header("Prop Settings")]
        public string name; // Name of the map prop
        [Range(0f, 1f)] public float density; // Density of the prop in the map
        public GameObject prefab; // Prefab of the prop to instantiate

        [Header("Object Settings")]
        public Range height; // height range for the prop placement
        public Range size; // Random size range for the prop
        public Range rotation; // Random rotation range for the prop

        // Generates a random size vector for the prop
        public Vector3 GetRandomSize() => Vector3.one * size.GetRandomValue();
        // Generates a random rotation for the prop
        public Quaternion GetRandomRotation() => Quaternion.Euler(Vector3.one * rotation.GetRandomValue());
    }

    [Header("General Settings")]
    [SerializeField] private bool autoUpdate = true; // If true, auto-update map on validate
    [SerializeField] private Texture2D mapTexture;
    [SerializeField] private Transform propHolder; // Parent transform for instantiated props
    [SerializeField] private TextureBasedPropPlacementType textureBasedPropPlacementType = TextureBasedPropPlacementType.Inner;

    [Header("Mesh Settings")]
    [SerializeField, Range(1, 255)] private int xSize = 255; // Width of the terrain mesh
    [SerializeField, Range(1, 255)] private int zSize = 255; // Depth of the terrain mesh
    [SerializeField, Range(1f, 10f)] private float sizeMultiplier = 5f; // Scale multiplier for the terrain
    [SerializeField] private Transform meshTransform; // Transform for the terrain mesh

    [Header("Noise Settings")]
    [SerializeField, Range(1, 10)] private int octaves = 6; // Number of noise octaves
    [SerializeField, Range(1f, 100f)] private float noiseScale = 50f; // Scale of the noise map
    [SerializeField, Range(0f, 1f)] private float persistence = 0.5f; // Amplitude reduction per octave
    [SerializeField, Range(0f, 2f)] private float lacunarity = 2f; // Frequency increase per octave

    [Header("height Settings")]
    [SerializeField] private float heightMultiplier = 10f; // Multiplier for terrain height
    [SerializeField] private AnimationCurve heightCurve; // height curve for terrain elevation
    [SerializeField] private Gradient heightGradient; // Gradient for vertex coloring based on height

    [Header("Falloff Settings")]
    [SerializeField] private bool useFalloff = true; // Whether to apply falloff to the terrain
    [SerializeField, Range(0f, 1f)] private float falloffStart = 0.5f; // Start of the falloff effect
    [SerializeField, Range(0f, 1f)] private float falloffEnd = 1f; // End of the falloff effect

    [Header("Seed Settings")]
    [SerializeField] private bool randomSeed; // Whether to use a random seed
    [SerializeField] private int seed; // Seed for noise generation
    [SerializeField] private Vector2 offset; // Offset for the noise map

    [Header("Height Based Prop Placement Settings")]
    [SerializeField] private MapProp[] heightBasedProps; // Array of map props

    [Header("Texture Based Prop Placement Settings")]
    [SerializeField, Range(0f, 255f)] private float colorRange = 20f;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private MapProp[] textureBasedProps;

    private float[,] noiseMap; // 2D array for noise map values
    private float[,] falloffMap; // 2D array for falloff map values
    private Mesh mesh; // Terrain mesh

    private void Start() {
        // REMOVE 'GenerateHeightProps();', 'GenerateTextureProps();' AND 'GenerateMap();' ACCORDING TO HOW YOU WILL BE GENERATING TERRAIN!
        // USE DEFAULT FOR ALL PROCEDURAL GENERATION:

        GenerateMap(); // Generate the terrain map at start
        GenerateHeightProps(); // Generate height props at start
        GenerateTextureProps(); // Generate texture props at start
    }

    public void GenerateMap() {
        noiseMap = GenerateNoiseMap(); // Create noise map
        falloffMap = useFalloff ? GenerateFalloffMap() : null; // Generate falloff map if enabled
        ApplyFalloffToNoiseMap(); // Apply falloff map to noise map

        mesh = CreateMesh(); // Create the terrain mesh
        ApplyMesh(mesh); // Apply the mesh to the mesh filter and collider
    }

    public void GenerateHeightProps() {
        for (int z = 0; z < zSize; z++) {
            for (int x = 0; x < xSize; x++) {
                foreach (MapProp prop in heightBasedProps) {
                    // Check if the current position is within the prop's height range and meets density criteria
                    if (IsWithinHeightRange(x, z, prop) && Random.value <= prop.density / 10f) {
                        SpawnProp(x, z, prop); // Spawn the prop at the position
                    }
                }
            }
        }
    }

    private bool IsWithinHeightRange(int x, int z, MapProp prop) {
        float height = noiseMap[x, z]; // Get height from noise map
        return height >= prop.height.minimum && height <= prop.height.maximum; // Check if within range
    }

    private void SpawnProp(int x, int z, MapProp prop) {
        int vertexIndex = (z * (xSize + 1)) + x; // Calculate vertex index
        Vector3 position = (mesh.vertices[vertexIndex] * sizeMultiplier) + propHolder.position; // Determine prop position
        GameObject instance = Instantiate(prop.prefab, position, prop.GetRandomRotation(), propHolder); // Instantiate the prop
        instance.transform.localScale = prop.GetRandomSize(); // Set prop scale
    }

    private void OnValidate() {
        if (falloffEnd < falloffStart) {
            falloffEnd = falloffStart; // Ensure falloffEnd is not less than falloffStart
        }

        if (autoUpdate) {
            GenerateMap(); // Auto-update map if enabled
        }
    }

    private void ApplyFalloffToNoiseMap() {
        if (falloffMap == null) return; // Skip if falloff map is not used

        for (int z = 0; z <= zSize; z++) {
            for (int x = 0; x <= xSize; x++) {
                noiseMap[x, z] *= falloffMap[x, z]; // Combine noise map with falloff map
            }
        }
    }

    private float[,] GenerateNoiseMap() {
        float[,] map = new float[xSize + 1, zSize + 1]; // Initialize noise map

        System.Random prng = new System.Random(randomSeed ? Random.Range(-100000, 100000) : seed); // Random or fixed seed
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng); // Generate offsets for octaves

        float halfXSize = xSize / 2f;
        float halfZSize = zSize / 2f;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int z = 0; z <= zSize; z++) {
            for (int x = 0; x <= xSize; x++) {
                // Calculate height value using Perlin noise
                float height = CalculatePerlinHeight(x, z, octaveOffsets, halfXSize, halfZSize, ref minHeight, ref maxHeight);
                map[x, z] = height;
            }
        }

        NormalizeNoiseMap(map, minHeight, maxHeight); // Normalize noise values to [0, 1]
        return map;
    }

    private Vector2[] GenerateOctaveOffsets(System.Random prng) {
        Vector2[] offsets = new Vector2[octaves]; // Create array for octave offsets
        for (int i = 0; i < octaves; i++) {
            offsets[i] = new Vector2(prng.Next(-100000, 100000), prng.Next(-100000, 100000)) + (offset / 10f); // Generate random offset
        }
        return offsets;
    }

    private float CalculatePerlinHeight(int x, int z, Vector2[] octaveOffsets, float halfXSize, float halfZSize, ref float minHeight, ref float maxHeight) {
        float amplitude = 1f; // Initial amplitude
        float frequency = 1f; // Initial frequency
        float height = 0f; // Accumulated height value

        for (int i = 0; i < octaves; i++) {
            float sampleX = ((x - halfXSize) / noiseScale * frequency) + octaveOffsets[i].x; // Adjust X by frequency and offset
            float sampleZ = ((z - halfZSize) / noiseScale * frequency) + octaveOffsets[i].y; // Adjust Z by frequency and offset

            float perlinValue = (Mathf.PerlinNoise(sampleX, sampleZ) * 2f) - 1f; // Get Perlin noise value and adjust range
            height += perlinValue * amplitude; // Add weighted Perlin value to height

            amplitude *= persistence; // Reduce amplitude
            frequency *= lacunarity; // Increase frequency
        }

        maxHeight = Mathf.Max(maxHeight, height); // Update max height
        minHeight = Mathf.Min(minHeight, height); // Update min height
        return height;
    }

    private void NormalizeNoiseMap(float[,] map, float minHeight, float maxHeight) {
        for (int z = 0; z <= zSize; z++) {
            for (int x = 0; x <= xSize; x++) {
                // Scale noise values to [0, 1]
                map[x, z] = Mathf.InverseLerp(minHeight, maxHeight, map[x, z]);
            }
        }
    }

    private float[,] GenerateFalloffMap() {
        float[,] map = new float[xSize + 1, zSize + 1]; // Initialize falloff map

        for (int z = 0; z <= zSize; z++) {
            for (int x = 0; x <= xSize; x++) {
                float xVal = ((float)x / xSize * 2f) - 1f; // Map X to [-1, 1]
                float zVal = ((float)z / zSize * 2f) - 1f; // Map Z to [-1, 1]
                float edgeDistance = Mathf.Max(Mathf.Abs(xVal), Mathf.Abs(zVal)); // Calculate edge distance

                float falloffValue = Mathf.InverseLerp(falloffStart, falloffEnd, edgeDistance); // Map to falloff range
                map[x, z] = Mathf.SmoothStep(1f, 0f, falloffValue); // Smooth transition for falloff
            }
        }

        return map;
    }

    private Mesh CreateMesh() {
        Mesh mesh = new Mesh(); // Create new mesh instance

        Vector3[] vertices = new Vector3[(xSize + 1) * (zSize + 1)]; // Vertex array
        Vector2[] uvs = new Vector2[vertices.Length]; // UV coordinates array
        int[] triangles = new int[xSize * zSize * 6]; // Triangle indices array
        Color[] colors = new Color[vertices.Length]; // Vertex color array

        GenerateVerticesAndUVs(vertices, uvs); // Generate vertices and UVs
        GenerateTriangles(triangles); // Generate triangle indices
        ApplyHeightGradient(vertices, colors); // Apply gradient to vertices

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;

        mesh.RecalculateNormals(); // Calculate normals for lighting
        return mesh;
    }

    private void GenerateVerticesAndUVs(Vector3[] vertices, Vector2[] uvs) {
        for (int z = 0, i = 0; z <= zSize; z++) {
            for (int x = 0; x <= xSize; x++, i++) {
                float height = heightCurve.Evaluate(noiseMap[x, z]) * heightMultiplier; // Adjust height using curve
                vertices[i] = new Vector3(x, height, z); // Set vertex position
                uvs[i] = new Vector2((float)x / xSize, (float)z / zSize); // Set UV coordinate
            }
        }
    }

    private void GenerateTriangles(int[] triangles) {
        for (int z = 0, vert = 0, tris = 0; z < zSize; z++, vert++) {
            for (int x = 0; x < xSize; x++, vert++, tris += 6) {
                // Define two triangles for each quad
                triangles[tris] = vert;
                triangles[tris + 1] = vert + xSize + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + xSize + 1;
                triangles[tris + 5] = vert + xSize + 2;
            }
        }
    }

    private void ApplyHeightGradient(Vector3[] vertices, Color[] colors) {
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Find min and max height values
        foreach (Vector3 vertex in vertices) {
            minHeight = Mathf.Min(minHeight, vertex.y);
            maxHeight = Mathf.Max(maxHeight, vertex.y);
        }

        for (int i = 0; i < vertices.Length; i++) {
            // Set color based on normalized height
            colors[i] = heightGradient.Evaluate(Mathf.InverseLerp(minHeight, maxHeight, vertices[i].y));
        }
    }

    private void ApplyMesh(Mesh generatedMesh) {
        MeshFilter meshFilter = meshTransform.GetComponent<MeshFilter>(); // Get mesh filter component
        MeshCollider meshCollider = meshTransform.GetComponent<MeshCollider>(); // Get mesh collider component

        meshFilter.sharedMesh = generatedMesh; // Apply generated mesh to filter
        meshCollider.sharedMesh = generatedMesh; // Apply generated mesh to collider

        Vector3 offsetPosition = new Vector3(-(xSize / 2f), 0f, -(zSize / 2f)) * sizeMultiplier; // Center mesh
        meshTransform.localPosition = offsetPosition; // Offset position
        propHolder.localPosition = offsetPosition; // Offset props holder position
        meshTransform.localScale = Vector3.one * sizeMultiplier; // Scale mesh
    }

    private List<Vector2Int> GetNeighborOffsets(int x, int y, int textureWidth, int textureHeight) {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // Neighbor positions relative to the current pixel
        int[] dx = { -1, 1, 0, 0 }; // Left, Right, Up, Down
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < dx.Length; i++) {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (nx >= 0 && nx < textureWidth && ny >= 0 && ny < textureHeight) {
                neighbors.Add(new Vector2Int(nx, ny));
            }
        }

        return neighbors;
    }

    public void GenerateTextureProps() {
        if (textureBasedPropPlacementType == TextureBasedPropPlacementType.Inner) {
            Color[] pixels = mapTexture.GetPixels();
            int textureWidth = mapTexture.width;
            int textureHeight = mapTexture.height;

            for (int y = 0; y < textureHeight; y++) {
                for (int x = 0; x < textureWidth; x++) {
                    int pixelIndex = (y * textureWidth) + x;
                    Color pixelColor = pixels[pixelIndex];

                    if (IsColorMatch(pixelColor, selectedColor)) {
                        foreach (MapProp prop in textureBasedProps) {
                            if (Random.value <= prop.density / 10f) {
                                SpawnTextureProp(x, y, prop);
                            }
                        }
                    }
                }
            }
        } else if (textureBasedPropPlacementType == TextureBasedPropPlacementType.Outer) {
            Color[] pixels = mapTexture.GetPixels();
            int textureWidth = mapTexture.width;
            int textureHeight = mapTexture.height;

            for (int y = 0; y < textureHeight; y++) {
                for (int x = 0; x < textureWidth; x++) {
                    int pixelIndex = (y * textureWidth) + x;
                    Color currentPixelColor = pixels[pixelIndex];

                    // Check neighbors for color difference
                    bool isEdge = false;
                    foreach (Vector2Int neighborOffset in GetNeighborOffsets(x, y, textureWidth, textureHeight)) {
                        int neighborIndex = (neighborOffset.y * textureWidth) + neighborOffset.x;
                        Color neighborColor = pixels[neighborIndex];

                        if (!IsColorMatch(currentPixelColor, neighborColor)) {
                            isEdge = true;
                            break;
                        }
                    }

                    if (isEdge) {
                        foreach (MapProp prop in textureBasedProps) {
                            if (Random.value <= prop.density / 10f) {
                                SpawnTextureProp(x, y, prop);
                            }
                        }
                    }
                }
            }
        }
    }

    private bool IsColorMatch(Color pixelColor, Color targetColor) {
        float rDiff = Mathf.Abs(pixelColor.r - targetColor.r);
        float gDiff = Mathf.Abs(pixelColor.g - targetColor.g);
        float bDiff = Mathf.Abs(pixelColor.b - targetColor.b);

        return rDiff <= colorRange / 255f && gDiff <= colorRange / 255f && bDiff <= colorRange / 255f;
    }

    private void SpawnTextureProp(int x, int y, MapProp prop) {
        Vector3 worldPos = TextureToWorldPosition(x, y);
        GameObject instance = Instantiate(prop.prefab, worldPos, prop.GetRandomRotation(), propHolder);
        instance.transform.localScale = prop.GetRandomSize();
    }

    private Vector3 TextureToWorldPosition(int x, int y) {
        float worldX = ((float)x / mapTexture.width * xSize * sizeMultiplier) - (xSize * sizeMultiplier / 2f);
        float worldZ = ((float)y / mapTexture.height * zSize * sizeMultiplier) - (zSize * sizeMultiplier / 2f);
        float height = GetHeightFromNoiseMap(worldX, worldZ);

        return new Vector3(worldX, height, worldZ) + transform.position;
    }

    private float GetHeightFromNoiseMap(float worldX, float worldZ) {
        float normalizedX = Mathf.InverseLerp(-(xSize / 2f), xSize / 2f, worldX / sizeMultiplier);
        float normalizedZ = Mathf.InverseLerp(-(zSize / 2f), zSize / 2f, worldZ / sizeMultiplier);

        int xCoord = Mathf.FloorToInt(normalizedX * xSize);
        int zCoord = Mathf.FloorToInt(normalizedZ * zSize);

        xCoord = Mathf.Clamp(xCoord, 0, xSize - 1);
        zCoord = Mathf.Clamp(zCoord, 0, zSize - 1);

        float baseHeight = noiseMap[xCoord, zCoord];
        return heightCurve.Evaluate(baseHeight) * heightMultiplier * sizeMultiplier;
    }

    public void ClearAllProps() {
        foreach (Transform child in propHolder) {
            DestroyImmediate(child.gameObject);
        }
    }
}
