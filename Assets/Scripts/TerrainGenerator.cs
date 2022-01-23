using UnityEngine;
using System.Collections;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Global parameters")]
    [SerializeField] private int sideLengthInChunks;
    [SerializeField] private int maxCubeHeight;
    [SerializeField] private int baseCubeHeight;
    [SerializeField] private Vector2Int baseNoiseOffset;
    [SerializeField] private int seed;

    [Header("Terrain Noise")]
    [SerializeField] private Vector2Int terrainNoiseOffset;
    [Min(1)]
    [SerializeField] private int terrainOctaves;
    [SerializeField] private float terrainLacunarity;
    [Range(0, 1)]
    [SerializeField] private float terrainPersistence;
    [SerializeField] private float terrainScale;
    [SerializeField] private AnimationCurve terrainAdjustmentCurve;
    [SerializeField] private Color32 surfaceColor;
    [SerializeField] private Color32 belowSurfaceColor;

    [Header("Cave Noise")]
    [SerializeField] private bool onlyCaves = false;
    [SerializeField] private int wormNoisePaddingInChunks;
    [SerializeField] private Vector2Int wormNoiseOffset;
    [SerializeField] private float wormNoiseScale;
    [SerializeField] private float minWormRadius;
    [SerializeField] private float maxWormRadius;
    [Range(0f, 1f)]
    [SerializeField] private float wormRadiusNoiseRatio;
    [SerializeField] private int minWormLength;
    [SerializeField] private Color32 caveColor;

    [Header("Preview")]
    [SerializeField] private Renderer terrainRenderer;
    [SerializeField] private Renderer wormRenderer;

    [Header("Mesh rendering")]
    [SerializeField] private MeshGenerator meshGenerator;
    [SerializeField] private bool autoUpdate;
    [Min(0.2f)]
    [SerializeField] private float autoUpdateDelay;
    private CubeTerrain terrain;
    private bool needsUpdate = false;

    private void PreviewNoisePlane()
    {
        int sideLength = sideLengthInChunks * DataManager.CHUNK_SIZE;
        float[,] noisePlane = NoiseUtil.PerlinNoisePlane(sideLength, sideLength, baseNoiseOffset + terrainNoiseOffset, terrainScale, terrainOctaves, terrainLacunarity, terrainPersistence, seed);

        Texture2D texture = new Texture2D(sideLength, sideLength);
        Color[] colorMap = new Color[sideLength * sideLength];

        for (int i = 0; i < sideLength; i++)
            for (int j = 0; j < sideLength; j++)
                colorMap[j * sideLength + i] = Color.Lerp(Color.black, Color.white, terrainAdjustmentCurve.Evaluate(noisePlane[i, j]));

        texture.SetPixels(colorMap);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        terrainRenderer.sharedMaterial.mainTexture = texture;
        terrainRenderer.transform.localScale = new Vector3(sideLength / 10f, 1, sideLength / 10f);
    }

    private void PreviewNoiseWorm()
    {
        int sideLength = (wormNoisePaddingInChunks * 2 + sideLengthInChunks) * DataManager.CHUNK_SIZE;
        var wormStartingPoints = NoiseUtil.PerlinWormStartingPoints(sideLength, sideLength, baseNoiseOffset + wormNoiseOffset, wormNoiseScale, seed);

        Texture2D texture = new Texture2D(sideLength, sideLength);
        Color[] colorMap = new Color[sideLength * sideLength];

        for (int i = 0; i < sideLength; i++)
            for (int j = 0; j < sideLength; j++)
                colorMap[i * sideLength + j] = Color.black;

        foreach (Vector2Int point in wormStartingPoints)
            colorMap[point.y * sideLength + point.x] = Color.white;

        texture.SetPixels(colorMap);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        wormRenderer.sharedMaterial.mainTexture = texture;
        wormRenderer.transform.localScale = new Vector3(sideLength / 10f, 1, sideLength / 10f);
    }

    private void OnValidate()
    {
        if (this.isActiveAndEnabled)
        {
            PreviewNoisePlane();
            PreviewNoiseWorm();
            needsUpdate = true;
        }
    }

    private void GenerateTerrain()
    {
        int wormNoisePaddingLength = wormNoisePaddingInChunks * DataManager.CHUNK_SIZE;
        int sideLength = sideLengthInChunks * DataManager.CHUNK_SIZE;
        int wormNoiseLength = wormNoisePaddingLength * 2 + sideLength;

        bool[,,] wormNoise = NoiseUtil.PerlinWorms3D(wormNoiseLength, maxCubeHeight + 1, wormNoiseLength, baseNoiseOffset + wormNoiseOffset, wormNoiseScale, minWormRadius, maxWormRadius, wormRadiusNoiseRatio, minWormLength, wormNoisePaddingLength, seed);
        float[,] heightNoise = NoiseUtil.PerlinNoisePlane(sideLength, sideLength, baseNoiseOffset + terrainNoiseOffset, terrainScale, terrainOctaves, terrainLacunarity, terrainPersistence, seed);
        bool[,,] activeMap = new bool[sideLength, maxCubeHeight + 1, sideLength];
        Color32[,,] colorMap = new Color32[sideLength, maxCubeHeight + 1, sideLength];

        for (int x = 0; x < sideLength; x++)
        {
            for (int z = 0; z < sideLength; z++)
            {
                int relativeMaxHeight = Mathf.RoundToInt(terrainAdjustmentCurve.Evaluate(heightNoise[x, z]) * (maxCubeHeight - baseCubeHeight));
                // Value is clamped, in case result of noise is not actually between 0 and 1 (which can happen according to the documentation)
                relativeMaxHeight = Mathf.Clamp(relativeMaxHeight, 0, maxCubeHeight - baseCubeHeight);

                for (int y = 0; y <= maxCubeHeight; y++)
                {
                    if (onlyCaves)
                    {
                        if (!wormNoise[x + wormNoisePaddingLength, y, z + wormNoisePaddingLength])
                        {
                            activeMap[x, y, z] = true;
                            colorMap[x, y, z] = caveColor;
                        }
                        else
                            activeMap[x, y, z] = false;
                    }
                    else
                    {
                        if (!wormNoise[x + wormNoisePaddingLength, y, z + wormNoisePaddingLength])
                            activeMap[x, y, z] = false;
                        else if (y <= baseCubeHeight + relativeMaxHeight)
                        {
                            activeMap[x, y, z] = true;
                            if (y == baseCubeHeight + relativeMaxHeight)
                                colorMap[x, y, z] = surfaceColor;
                            else
                                colorMap[x, y, z] = belowSurfaceColor;
                        }
                        else
                            activeMap[x, y, z] = false;
                    }
                }
            }
        }

        terrain = new CubeTerrain(activeMap, colorMap, 1);
        meshGenerator.GenerateMesh(terrain);
    }

    private IEnumerator Start()
    {
        GenerateTerrain();
        while (true)
        {
            if (needsUpdate && autoUpdate)
            {
                needsUpdate = false;
                GenerateTerrain();
            }
            yield return new WaitForSeconds(autoUpdateDelay);
        }
    }
}
