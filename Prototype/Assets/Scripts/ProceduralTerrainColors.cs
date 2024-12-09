using UnityEngine;

public class ProceduralTerrainColors : MonoBehaviour
{
    [Header("Color Settings")]
    public Texture2D colorMap;   // The pre-existing Voronoi texture to apply

    [Header("Texture Scaling")]
    [Range(0.1f, 3f)]                // Allow scaling from 0.1 to 3 times the original size
    public float textureScale = 1f;    // The scaling factor of the texture (controlled in Inspector)

    private Renderer renderer;         // Renderer component to apply the texture

    void Start()
    {
        renderer = GetComponent<Renderer>();

        // Check if the Voronoi texture is assigned
        if (colorMap != null)
        {
            // Apply the pre-existing Voronoi texture to the material
            renderer.material.mainTexture = colorMap;
        }
    }

    void Update()
    {
        // Apply the texture scale when the variable changes in the Inspector
        ApplyTextureScale();
    }

    /// <summary>
    /// Apply the scaling factor to the texture's scale based on the textureScale variable.
    /// </summary>
    void ApplyTextureScale()
    {
        if (colorMap != null)
        {
            // Get the aspect ratio of the texture
            float aspectRatio = (float)colorMap.width / (float)colorMap.height;

            // Apply the texture's aspect ratio to the scaling
            renderer.material.mainTextureScale = new Vector2(textureScale * aspectRatio, textureScale);
        }
    }
}