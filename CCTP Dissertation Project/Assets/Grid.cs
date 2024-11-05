using UnityEngine;

public class Grid : MonoBehaviour
{
    public float Waterlevel = .4f;
    public float scale = .1f;
    //Grid size
    public int size = 100;

    Cell[,] grid;

    void Start() 
    {
        float[,] noiseMap = new float[size, size];
        for(int y = 0; y < size; y++) 
        {
            for(int x = 0; x < size; x++) 
            {
                float noiseValue = Mathf.PerlinNoise(x * scale, y * scale);
                noiseMap[x, y] = noiseValue;
            
            }
        }

        grid = new Cell[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = new Cell();
                float noiseValue = noiseMap[x, y];
                cell.isWater = noiseValue < Waterlevel;
                grid[x, y] = cell;
            }
        }
    }

    // Function handling the color when the cubes are created. Blue for water and Green for land
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        for(int y = 0; y < size; y++) 
        {
            for(int x = 0; x < size; x++) 
            {
                Cell cell = grid[x, y];
                if (cell.isWater)
                    Gizmos.color = Color.blue;
                else
                    Gizmos.color = Color.green;
                Vector3 pos = new Vector3(x, 0, y);
                Gizmos.DrawCube(pos, Vector3.one);
            }
        }
    }
}
