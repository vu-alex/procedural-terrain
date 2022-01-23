using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    [SerializeField] private Material meshMaterial;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Color32> colors = new List<Color32>();

    public void GenerateMesh(CubeTerrain terrain)
    {
        int terrainWidth = terrain.cubes.GetLength(0);
        int terrainHeight = terrain.cubes.GetLength(1);
        int terrainDepth = terrain.cubes.GetLength(2);

        int chunksCountX = terrainWidth / DataManager.CHUNK_SIZE;
        int chunksCountZ = terrainDepth / DataManager.CHUNK_SIZE;

        // Clear existing mesh
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        // Generate mesh, chunk by chunk
        for (int i = 0; i < chunksCountX; i++)
            for (int j = 0; j < chunksCountZ; j++)
                GenerateMeshChunk(terrain, new Vector2Int(i, j), terrainWidth, terrainHeight, terrainDepth);
    }

    public void GenerateMeshChunk(CubeTerrain terrain, Vector2Int chunkPosition, int terrainWidth, int terrainHeight, int terrainDepth)
    {
        GameObject chunk = new GameObject("Mesh Chunk");
        MeshFilter meshFilter = chunk.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunk.AddComponent<MeshRenderer>();
        chunk.transform.parent = transform;

        Mesh mesh = new Mesh();
        vertices.Clear();
        triangles.Clear();
        colors.Clear();

        // Neighbours are checked in the following order: FRONT BACK LEFT RIGHT BOTTOM TOP
        bool[] activeNeighbours = new bool[6];
        for (int i = chunkPosition.x * DataManager.CHUNK_SIZE; (i < terrainWidth) && (i < (chunkPosition.x + 1) * DataManager.CHUNK_SIZE); i++)
        {
            for (int j = chunkPosition.y * DataManager.CHUNK_SIZE; (j < terrainDepth) && (j < (chunkPosition.y + 1) * DataManager.CHUNK_SIZE); j++)
            {
                for (int k = 0; k < terrainHeight; k++)
                {
                    if (terrain.cubes[i, k, j].active)
                    {
                        activeNeighbours[0] = (j - 1 >= 0) && terrain.cubes[i, k, j - 1].active;
                        activeNeighbours[1] = (j + 1 < terrainDepth) && terrain.cubes[i, k, j + 1].active;
                        activeNeighbours[2] = (i - 1 >= 0) && terrain.cubes[i - 1, k, j].active;
                        activeNeighbours[3] = (i + 1 < terrainWidth) && terrain.cubes[i + 1, k, j].active;
                        activeNeighbours[4] = (k - 1 >= 0) && terrain.cubes[i, k - 1, j].active;
                        activeNeighbours[5] = (k + 1 < terrainHeight) && terrain.cubes[i, k + 1, j].active;

                        GenerateQuadsForCube(terrain.cubes[i, k, j], activeNeighbours);
                    }
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors32 = colors.ToArray();
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;

        meshRenderer.material = meshMaterial;
    }

    private void GenerateQuadsForCube(Cube cube, bool[] activeNeighbours)
    {
        // A face is drawn only if there is no neighbour there
        if (!activeNeighbours[0])
            GenerateQuadForFace(cube.corners[0], cube.corners[1], cube.corners[2], cube.corners[3], cube.color); // FRONT
        if (!activeNeighbours[1])
            GenerateQuadForFace(cube.corners[7], cube.corners[6], cube.corners[5], cube.corners[4], cube.color); // BACK
        if (!activeNeighbours[2])
            GenerateQuadForFace(cube.corners[4], cube.corners[5], cube.corners[1], cube.corners[0], cube.color); // LEFT
        if (!activeNeighbours[3])
            GenerateQuadForFace(cube.corners[3], cube.corners[2], cube.corners[6], cube.corners[7], cube.color); // RIGHT
        if (!activeNeighbours[4])
            GenerateQuadForFace(cube.corners[0], cube.corners[3], cube.corners[7], cube.corners[4], cube.color); // BOTTOM
        if (!activeNeighbours[5])
            GenerateQuadForFace(cube.corners[1], cube.corners[5], cube.corners[6], cube.corners[2], cube.color); // TOP
    }

    private void GenerateQuadForFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 faceColor)
    {
        int verticesCount = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        colors.Add(faceColor);
        colors.Add(faceColor);
        colors.Add(faceColor);
        colors.Add(faceColor);

        triangles.Add(verticesCount);
        triangles.Add(verticesCount + 1);
        triangles.Add(verticesCount + 3);
        triangles.Add(verticesCount + 3);
        triangles.Add(verticesCount + 1);
        triangles.Add(verticesCount + 2);
    }
}
