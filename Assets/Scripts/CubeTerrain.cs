using UnityEngine;

public class CubeTerrain
{
    public Cube[,,] cubes;

    public CubeTerrain(bool[,,] map, Color32[,,] colorMap, float cubeSize)
    {
        int nodeCountX = map.GetLength(0);
        int nodeCountY = map.GetLength(1);
        int nodeCountZ = map.GetLength(2);
        float mapWidth = nodeCountX * cubeSize;
        float mapHeight = nodeCountY * cubeSize;
        float mapDepth = nodeCountZ * cubeSize;

        cubes = new Cube[nodeCountX, nodeCountY, nodeCountZ];
        for (int x = 0; x < nodeCountX; x++)
        {
            for (int y = 0; y < nodeCountY; y++)
            {
                for (int z = 0; z < nodeCountZ; z++)
                {
                    Vector3 cubePosition = new Vector3(
                            -mapWidth / 2 + (x + 0.5f) * cubeSize,
                            (y + 0.5f) * cubeSize,
                            -mapDepth / 2 + (z + 0.5f) * cubeSize
                        );

                    cubes[x, y, z] = new Cube(map[x, y, z], cubePosition, colorMap[x, y, z], cubeSize);
                }
            }
        }
    }
}

public struct Cube
{
    public bool active;
    public Vector3 position;
    public Color32 color;

    // Corners are in the following order :
    // - Clockwise from Front Bottom Left Corner
    // - Then ClockWise from Back Bottom Left Corner
    // The origin is at the Front Bottom Left Corner
    public Vector3[] corners;


    public Cube(bool active, Vector3 position, Color32 color, float cubeSize)
    {
        this.active = active;
        this.position = position;
        this.color = color;
        corners = new Vector3[] {
                position + cubeSize/2 * new Vector3(-1, -1, -1),
                position + cubeSize/2 * new Vector3(-1, 1, -1),
                position + cubeSize/2 * new Vector3(1, 1, -1),
                position + cubeSize/2 * new Vector3(1, -1, -1),
                position + cubeSize/2 * new Vector3(-1, -1, 1),
                position + cubeSize/2 * new Vector3(-1, 1, 1),
                position + cubeSize/2 * new Vector3(1, 1, 1),
                position + cubeSize/2 * new Vector3(1, -1, 1)
            };
    }
}