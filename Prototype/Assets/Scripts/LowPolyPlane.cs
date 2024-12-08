using UnityEngine;

public class LowPolyPlane : MonoBehaviour
{
    [Header("Mesh Settings")]
    public int size = 50; // Size of the terrain grid

    [Header("Noise Settings")]
    public float noiseScale = 15f; // Scaling factor for the noise
    public int octaves = 4; // Number of noise octaves
    [Range(0f, 1f)] public float persistence = 0.5f; // Amplitude reduction per octave
    public float lacunarity = 2f; // Frequency increase per octave
    public Vector2 noiseOffset; // Offsets for the noise generation

    [Header("Terrain Settings")]
    public float elevation = 5f; // Maximum height variation
    public int numVoronoiSeeds = 21; // Number of Voronoi regions

    private Mesh terrainMesh;
    private MeshRenderer meshRenderer;
    private Vector2[] voronoiSeeds; // Array to store Voronoi seed positions

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        GenerateVoronoiSeeds();
        GenerateMesh();
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
                float height = GenerateFractalNoise(x, y) * elevation;
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
    }

    float GenerateFractalNoise(int x, int y)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = (x + noiseOffset.x) * noiseScale * frequency / size;
            float sampleY = (y + noiseOffset.y) * noiseScale * frequency / size;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlinValue * amplitude;

            amplitude *= persistence; // Decrease amplitude for the next octave
            frequency *= lacunarity; // Increase frequency for the next octave
        }

        return Mathf.Clamp01(noiseHeight); // Clamp noise height to range [0, 1]
    }

    // Update method to tweak terrain in real-time during editing mode
    void Update()
    {
        GenerateMesh();
    }
}