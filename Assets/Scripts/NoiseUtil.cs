using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseUtil
{
    public static float[,] PerlinNoisePlane(int width, int depth, Vector2Int offset, float scale, int octaves, float lacunarity, float persistence, int seed)
    {
        float[,] plane = new float[width, depth];

        // Initialisation
        float maxNoiseHeight = 0;
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        float octaveMaxAmplitude = 1;
        for (int i = 0; i < octaves; i++)
        {
            maxNoiseHeight += octaveMaxAmplitude;
            // each octave has a different offset
            float xOffset = prng.Next(-100000, 100000) + offset.x;
            float yOffset = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(xOffset, yOffset);
            octaveMaxAmplitude *= persistence;
        }

        if (scale <= 0)
            scale = 0.01f;

        // Compute several octaves of noise for each point on plane
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseResult = 0;

                for (int k = 0; k < octaves; k++)
                {
                    // Values are divided by prime number to reduce "risks" of getting an integer
                    float noiseX = (i - width / 2f + octaveOffsets[k].x) / 29f * scale * frequency;
                    float noiseY = (j - depth / 2f + octaveOffsets[k].y) / 29f * scale * frequency;
                    noiseResult += Mathf.PerlinNoise(noiseX, noiseY) * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                // Put the values back between [0,1]
                plane[i, j] = Mathf.InverseLerp(0, maxNoiseHeight, noiseResult);
            }
        }

        return plane;
    }

    public static IEnumerable<Vector2Int> PerlinWormStartingPoints(int width, int depth, Vector2Int offset, float scale, int seed)
    {
        List<Vector2Int> startingPoints = new List<Vector2Int>();

        // Starting points are computed by finding local maximum on a 1-octave Perlin noise plane
        int startingPointsNoiseSeed = seed * 169259;
        float[,] noisePlane = PerlinNoisePlane(width + 2, depth + 2, offset, scale, 1, 1, 1, startingPointsNoiseSeed);
        for (int i = 1; i <= width; i++)
        {
            for (int j = 1; j <= depth; j++)
            {
                if (noisePlane[i, j] > noisePlane[i, j + 1] &&
                    noisePlane[i, j] > noisePlane[i, j - 1] &&
                    noisePlane[i, j] > noisePlane[i + 1, j + 1] &&
                    noisePlane[i, j] > noisePlane[i + 1, j - 1] &&
                    noisePlane[i, j] > noisePlane[i + 1, j] &&
                    noisePlane[i, j] > noisePlane[i - 1, j + 1] &&
                    noisePlane[i, j] > noisePlane[i - 1, j - 1] &&
                    noisePlane[i, j] > noisePlane[i - 1, j])
                {
                    startingPoints.Add(new Vector2Int(i - 1, j - 1));
                }
            }
        }

        return startingPoints;
    }

    private static IEnumerable<Worm> ComputeWorms(IEnumerable<Vector2Int> startingPoints, int height, Vector2Int offset, float scale, float maxRadius, int minWormLength, float maxWormRange, int seed)
    {
        const float minXAngle = -80f;
        const float maxXAngle = -10f;
        const float stepLength = 1.375f;

        List<Worm> worms = new List<Worm>();
        Vector2 maxAngleChanges = new Vector2(12f, 36f);

        foreach (Vector2Int point in startingPoints)
        {
            // Initialize worm
            // Create unique seed for worm's pseudo-RNG
            int wormSeed = (point.x + offset.x) * 148867;
            wormSeed ^= (wormSeed >> 8);
            wormSeed *= 4895351;
            wormSeed += seed;
            wormSeed ^= (wormSeed << 8);
            wormSeed *= 878023;
            wormSeed += point.y + offset.y;
            wormSeed ^= (wormSeed >> 8);
            System.Random wormPrng = new System.Random(wormSeed);
            // Create worm with a unique max radius (used for carving the caves)
            Worm worm = new Worm(maxRadius * (float)(0.4 + (wormPrng.NextDouble() * 0.6)));

            // Initialise worm starting height, which is biased to be low
            double startHeightNormalized = wormPrng.NextDouble() - 0.05f;
            startHeightNormalized *= startHeightNormalized;
            int startPointHeight = (int)(startHeightNormalized * (height + 1));
            Vector3 startPoint = new Vector3(point.x, startPointHeight, point.y);
            worm.trajectory.Add(startPoint);

            // Initialise random offsets for rotation noise angles
            Vector2 angleNoises1 = new Vector2(
                wormPrng.Next(-100000, 100000),
                wormPrng.Next(-100000, 100000)
            ) * 0.987431477f;
            Vector2 angleNoises2 = new Vector2(
                wormPrng.Next(-100000, 100000),
                wormPrng.Next(-100000, 100000)
            ) * 0.845458483f;
            Vector2 rotationAngles = new Vector2(
                Mathf.Lerp(minXAngle, maxXAngle, Mathf.PerlinNoise(angleNoises1.x, angleNoises2.x)),
                Mathf.Lerp(-180f, 180f, Mathf.PerlinNoise(angleNoises1.y, angleNoises2.y))
            );

            // Prepare computation of trajectory
            Vector3 currentPoint = startPoint;
            float survivalChance = 1f;

            float minX = currentPoint.x;
            float maxX = currentPoint.x;

            float minZ = currentPoint.z;
            float maxZ = currentPoint.z;

            // Max range is used to get rid of worms that go too far (could be a problem when generating map chunk by chunk)
            float maxRangeSquared = maxWormRange * maxWormRange;

            while ((currentPoint.y > 0) &&
                   (survivalChance > wormPrng.NextDouble()) &&
                   ((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ) < maxRangeSquared))
            {
                // Compute step for current iteration and add result to trajectory
                Vector3 wormStep = Quaternion.Euler(rotationAngles.x, rotationAngles.y, 0) * Vector3.forward * stepLength;
                currentPoint += wormStep;
                worm.trajectory.Add(currentPoint);

                // Change angle of worm according to perlin noise
                float xNoiseResult = Mathf.PerlinNoise(angleNoises1.x, angleNoises2.x);
                float yNoiseResult = Mathf.PerlinNoise(angleNoises1.y, angleNoises2.y);
                float xAngleChange = Mathf.Lerp(-maxAngleChanges.x, maxAngleChanges.x, xNoiseResult);
                float yAngleChange = Mathf.Lerp(-maxAngleChanges.y, maxAngleChanges.y, yNoiseResult);
                rotationAngles = new Vector2(
                    Mathf.Clamp(rotationAngles.x + xAngleChange, maxXAngle, minXAngle),
                    rotationAngles.y + yAngleChange
                );

                // Update t for next loop's sample (values are arbitrary but not integers)
                angleNoises1.x += 0.1219f;
                angleNoises1.y += 0.0737f;
                angleNoises2.x += 0.08723f;
                angleNoises2.y += 0.093541f;

                // Survival chances indicate the odds of the worm continuing to carve a path
                // The more time a worm is alive, the lower are its chances of survival
                // The lower (in height) a worm is, the lower are its chances of survival
                survivalChance *= 0.99925f + 0.00075f * currentPoint.y / height;

                // Update range covered by worm
                if (currentPoint.x < minX)
                    minX = currentPoint.x;
                if (currentPoint.x > maxX)
                    maxX = currentPoint.x;

                if (currentPoint.z < minZ)
                    minZ = currentPoint.z;
                if (currentPoint.z > maxZ)
                    maxZ = currentPoint.z;
            }

            // Any trajectory that covers a too wide range is excluded
            if ((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ) >= maxRangeSquared)
                continue;

            // A worm that is longer than the required length is added to the collection. Otherwise, it's ignored
            if (worm.trajectory.Count > minWormLength)
                worms.Add(worm);
        }

        return worms;
    }

    public static bool[,,] PerlinWorms3D(int width, int height, int depth, Vector2Int offset, float scale, float minRadius, float maxRadius, float radiusNoiseRatio, int minWormLength, float maxWormsRange, int seed)
    {
        bool[,,] wormVolume = new bool[width, height, depth];

        // Initialisation
        for (int i = 0; i < wormVolume.GetLength(0); i++)
            for (int j = 0; j < wormVolume.GetLength(1); j++)
                for (int k = 0; k < wormVolume.GetLength(2); k++)
                    wormVolume[i, j, k] = true;

        IEnumerable<Vector2Int> startingPoints = PerlinWormStartingPoints(width, depth, offset, scale, seed);

        IEnumerable<Worm> worms = ComputeWorms(startingPoints, height, offset, scale, maxRadius, minWormLength, maxWormsRange, seed);

        // Carve volume with worms
        radiusNoiseRatio = Mathf.Clamp(radiusNoiseRatio, 0, 1);
        foreach (Worm worm in worms)
        {
            for (int i = 0; i < worm.trajectory.Count; i++)
            {
                Vector3 point = worm.trajectory[i];

                int pointX = (int)point.x;
                int pointY = (int)point.y;
                int pointZ = (int)point.z;

                float noiseResult = Mathf.PerlinNoise((point.x) / 13f + point.y * 0.12785f, (point.z) / 7f + point.y * 0.07893f);
                // Formula to compute a radius that is thinner on the two ends of the trajectory
                float normalizedRadius = -4 * ((float)i / worm.trajectory.Count - 0.5f) * ((float)i / worm.trajectory.Count - 0.5f) + 1;
                float normalizedNoisyRadius = radiusNoiseRatio * noiseResult + normalizedRadius * (1 - radiusNoiseRatio);
                float noisyRadius = Mathf.Lerp(minRadius, worm.maxRadius, normalizedNoisyRadius);

                int ceiledRadius = Mathf.CeilToInt(noisyRadius);
                for (int x = pointX - ceiledRadius; x <= pointX + ceiledRadius; x++)
                    for (int y = pointY - ceiledRadius; y <= pointY + ceiledRadius; y++)
                        for (int z = pointZ - ceiledRadius; z <= pointZ + ceiledRadius; z++)
                            if ((x >= 0 && x < width) && (y >= 0 && y < height) && (z >= 0 && z < depth))
                            {
                                Vector3 difference = new Vector3(x - pointX, y - pointY, z - pointZ);
                                if (difference.magnitude <= noisyRadius)
                                    wormVolume[x, y, z] = false;
                            }
            }
        }

        return wormVolume;
    }

    private class Worm
    {
        public List<Vector3> trajectory;
        public float maxRadius;

        public Worm(float maxRadius)
        {
            this.maxRadius = maxRadius;
            trajectory = new List<Vector3>();
        }
    }
}
