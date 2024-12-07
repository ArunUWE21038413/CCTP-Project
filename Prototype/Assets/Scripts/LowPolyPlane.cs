using UnityEngine;

public class LowPolyPlane : MonoBehaviour
{
    [Header("Mesh Settings")]
    public int size = 50;             // Size of the terrain grid

    [Header("Noise Settings")]
    public float noiseScale = 15f;    // Scaling factor for the noise
    public int octaves = 4;           // Number of noise octaves
    [Range(0f, 1f)]
    public float persistence = 0.5f;  // Amplitude reduction per octave
    public float lacunarity = 2f;     // Frequency increase per octave
    public Vector2 noiseOffset;       // Offsets for the noise generation

    [Header("Terrain Settings")]
    public float elevation = 5f;      // Maximum height variation
    public Gradient terrainGradient;  // Gradient for terrain colors
    public TextureType textureType = TextureType.Farmland; // Terrain texture type

    [Header("Voronoi Farmland Settings")]
    public int voronoiSeed = 40;       // Seed for Voronoi generation
    public int numVoronoiSeeds = 21;   // Number of Voronoi regions
    public Color farmlandColors;       // Color of farmland regions

    private Mesh terrainMesh;
    private MeshRenderer meshRenderer;
    private Vector2[] voronoiSeeds;    // Array to store Voronoi seed positions

    public enum TextureType
    {
        Farmland,
        Desert,
        Forest,
        // Add more textures as needed
    }

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        // Initialize gradient in case it's not set in the Inspector
        if (terrainGradient == null)
        {
            terrainGradient = new Gradient();
            SetUpTerrainGradient();
        }

        // Generate Voronoi seeds
        GenerateVoronoiSeeds();

        // Generate the mesh at the start
        GenerateMesh();
    }

    void SetUpTerrainGradient()
    {
        // Set up the terrain gradient with colors
        terrainGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.green, 0f),        // Green for low elevation (e.g., flat ground)
                new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 0.4f),  // Brown for middle elevation (e.g., hills)
                new GradientColorKey(new Color(0.8f, 0.7f, 0.3f), 0.8f),  // Yellowish-brown for higher elevation (e.g., mountains)
                new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 1f)     // Light beige for very high elevation (e.g., snow capped peaks)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f), // Fully opaque at the lowest elevation
                new GradientAlphaKey(1f, 1f)  // Fully opaque at the highest elevation
            }
        );
    }

    void GenerateVoronoiSeeds()
    {
        // Randomly generate seed points for Voronoi regions
        voronoiSeeds = new Vector2[numVoronoiSeeds];
        for (int i = 0; i < numVoronoiSeeds; i++)
        {
            float x = Random.Range(0f, size);
            float y = Random.Range(0f, size);
            voronoiSeeds[i] = new Vector2(x, y);
        }
    }

    void GenerateMesh()
    {
        // Create a new mesh for terrain
        terrainMesh = new Mesh();
        terrainMesh.name = "LowPolyTerrain";

        Vector3[] vertices = new Vector3[size * size];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(size - 1) * (size - 1) * 6];

        // Generate vertices and UVs for the terrain
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                int i = x + y * size;
                float height = Mathf.PerlinNoise((x + noiseOffset.x) * noiseScale / size, (y + noiseOffset.y) * noiseScale / size) * elevation;
                vertices[i] = new Vector3(x, height, y);
                uvs[i] = new Vector2((float)x / size, (float)y / size);
            }
        }

        // Generate triangles for the terrain mesh
        int t = 0;
        for (int x = 0; x < size - 1; x++)
        {
            for (int y = 0; y < size - 1; y++)
            {
                int i = x + y * size;
                triangles[t] = i;
                triangles[t + 1] = i + size;
                triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 1;
                triangles[t + 4] = i + size;
                triangles[t + 5] = i + size + 1;
                t += 6;
            }
        }

        // Apply mesh data to terrainMesh
        terrainMesh.vertices = vertices;
        terrainMesh.uv = uvs;
        terrainMesh.triangles = triangles;

        // Recalculate normals and bounds for lighting and collisions
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();

        // Assign the generated mesh to the MeshFilter component
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.mesh = terrainMesh;
        }

        // Apply terrain texture (using the Voronoi map for color patterns)
        if (meshRenderer != null)
        {
            meshRenderer.material.mainTexture = CreateVoronoiTexture(); // Apply generated texture
        }
    }

    Texture2D CreateVoronoiTexture()
    {
        Texture2D texture = new Texture2D(size, size);

        // Loop through each pixel and assign a color based on Voronoi region
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float minDist = Mathf.Infinity;
                int closestSeed = -1;

                // Find the nearest Voronoi seed
                for (int i = 0; i < numVoronoiSeeds; i++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), voronoiSeeds[i]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestSeed = i;
                    }
                }

                // Apply color based on the closest seed
                Color color = GetVoronoiColor(closestSeed);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    Color GetVoronoiColor(int seedIndex)
    {
        // Return color based on the Voronoi region (for demonstration purposes)
        // You can customize this function to return different colors based on the seed region
        if (seedIndex == 0)
            return Color.green; // Lowland (green)
        else if (seedIndex == 1)
            return new Color(0.6f, 0.4f, 0.2f); // Farmland (brown)
        else if (seedIndex == 2)
            return new Color(0.8f, 0.7f, 0.3f); // Mountain (yellowish-brown)
        else
            return new Color(0.9f, 0.9f, 0.9f); // Snow/Peak (light beige)
    }

    // Update method to tweak terrain in real-time during editing mode
    void Update()
    {
        // Allow real-time adjustments to terrain properties while in scene view
        GenerateMesh();
    }
}
