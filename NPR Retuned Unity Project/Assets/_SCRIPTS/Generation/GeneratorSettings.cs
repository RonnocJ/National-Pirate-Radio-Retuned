using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
public class Tile
{
    public TileSurface TSurface = new TileSurface();
    public int SurfaceRevision;
    public TConType Type;
    public Vector2Int Position;
    public GameObject Object;
    public Mesh Meshes;
    public List<Foliage> Foliage = new();
    public List<PfCell> Cells = new();
    public bool GeneratedFoliage;
    public Tile(TConType type)
    {
        Type = type;
    }
}
public class TileSurface
{
    public int Resolution;
    public float HalfSize;
    public float[,] Heights;
    public float MinX;
    public float MaxX;
    public float MinZ;
    public float MaxZ;
    public float StepX;
    public float StepZ;
}
[CreateAssetMenu(fileName = "GeneratorSettings", menuName = "Objects/World/GeneratorSettings", order = 0)]
public class GeneratorSettings : ScriptableSingleton<GeneratorSettings>
{
    public Dictionary<Vector2Int, Tile> TileDict = new();
    [Header("Grid Settings")]
    public int PlaceDistance;
    public int ViewDistance;
    public int CullMargin;
    public int CellSize;
    public int ChunkSize;
    [Header("Noise Settings")]
    public float NoiseScale;
    public float HeightScale;
    public AnimationCurve InitialSlope;
    [Header("Advanced Noise")]
    [Range(1, 8)] public int Octaves = 5;
    [Range(1.5f, 3.5f)] public float Lacunarity = 2f;
    [Range(0.1f, 0.9f)] public float Gain = 0.5f;
    [Range(0f, 2f)] public float WarpStrength = 0.75f;
    [Range(0f, 1f)] public float RidgeBlend = 0.35f;
    [Tooltip("Low-frequency modulation for big landmasses (smaller = broader).")]
    public float MacroFrequency = 0.05f;
    [Range(0f, 2f)] public float MacroStrength = 0.6f;
    [Header("Flatten Area (X and Z < Threshold)")]
    [Tooltip("Below this X and Z, terrain flattens to ~0.")]
    public float FlatThreshold = 192f;
    [Tooltip("Fade-in distance after threshold to avoid a hard edge.")]
    public float FlatFade = 64f;
    [Header("Construct References")]
    public TileConstruct[] TCons;
    public RoadConstruct[] RCons;
    public FoliageConstruct[] FCons;
    public BuildingConstruct[] BCons;
    [Header("Construct Limits")]
    public int GrassLimit;
    public int RoadLimit;
    public int TIntersectionLimit;
    public int XIntersectionLimit;
    public int LargeTreeLimit;
    public int SmallTreeLimit;
    public int LargeHouseLimit;
    public int SmallHouseLimit;
    public int AntennaLimit;

    private HeightSampleParams _heightParams;

    public HeightSampleParams HeightParams
    {
        get
        {
            if (_heightParams.InvNoiseScale == 0f)
            {
                RecalculateHeightParams();
            }

            return _heightParams;
        }
    }

    private void OnEnable()
    {
        RecalculateHeightParams();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RecalculateHeightParams();
    }
#endif

    public void ForceRecalculateHeightParams()
    {
        RecalculateHeightParams();
    }

    public float GetPerlinHeight(Vector3 inPos)
    {
        return EvaluateHeight(new float3(inPos.x, inPos.y, inPos.z), _heightParams);
    }

    [BurstCompile]
    public struct PerlinHeightJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        public NativeArray<float> Results;
        public HeightSampleParams Params;

        public void Execute(int index)
        {
            Results[index] = EvaluateHeight(Positions[index], Params);
        }
    }

    public struct HeightSampleParams
    {
        public float InvNoiseScale;
        public float FlatInnerX;
        public float FlatInnerZ;
        public float FlatEdgeScaleX;
        public float FlatEdgeScaleZ;
        public float RidgeBlend;
        public float HeightScale;
        public float WarpStrength;
        public int Octaves;
        public float Lacunarity;
        public float Gain;
        public float MacroFrequency;
        public float MacroMinAmp;
        public float MacroMaxAmp;
    }

    private void RecalculateHeightParams()
    {
        float noiseScaleSafe = math.max(NoiseScale, 0.0001f);
        float flatFadeSafe = math.max(FlatFade, 0.0001f);
        float macroFrequencySafe = math.max(MacroFrequency, 0.0001f);

        _heightParams = new HeightSampleParams
        {
            InvNoiseScale = math.rcp(noiseScaleSafe),
            FlatInnerX = FlatThreshold,
            FlatInnerZ = FlatThreshold,
            FlatEdgeScaleX = math.rcp(flatFadeSafe),
            FlatEdgeScaleZ = math.rcp(flatFadeSafe),
            RidgeBlend = RidgeBlend,
            HeightScale = HeightScale,
            WarpStrength = WarpStrength,
            Octaves = math.max(1, Octaves),
            Lacunarity = Lacunarity,
            Gain = Gain,
            MacroFrequency = macroFrequencySafe,
            MacroMinAmp = 1f - MacroStrength * 0.5f,
            MacroMaxAmp = 1f + MacroStrength * 0.5f
        };
    }

    internal static float EvaluateHeight(float3 inPos, HeightSampleParams p)
    {
        if (inPos.x < p.FlatInnerX && inPos.z < p.FlatInnerZ)
        {
            return inPos.y;
        }

        float2 basePos = new float2(inPos.x * p.InvNoiseScale, inPos.z * p.InvNoiseScale);

        float flatMaskX = SmoothStep(inPos.x, p.FlatInnerX, p.FlatEdgeScaleX);
        float flatMaskZ = SmoothStep(inPos.z, p.FlatInnerZ, p.FlatEdgeScaleZ);
        float flatMask = math.max(flatMaskX, flatMaskZ);
        if (flatMask <= 0f)
        {
            return inPos.y;
        }

        float2 warped = DomainWarp(basePos, p.WarpStrength);

        SampleCombinedFBM(warped, p.Octaves, p.Lacunarity, p.Gain, out float baseSum, out float ridgeSum, out float ampSum);

        float invAmp = math.rcp(math.max(ampSum, 0.0001f));
        float fbmBase = math.saturate(baseSum * invAmp);
        float fbmRidge = math.saturate(ridgeSum * invAmp);
        float terrain = math.lerp(fbmBase, fbmRidge, math.saturate(p.RidgeBlend));

        float macro = SampleFBM(basePos * p.MacroFrequency, 2, 2f, 0.5f);
        float macroAmp = math.lerp(p.MacroMinAmp, p.MacroMaxAmp, macro);

        float height = terrain * p.HeightScale * macroAmp * flatMask;

        return inPos.y + height;
    }

    // --- Noise utilities ---
    private static float2 DomainWarp(float2 p, float strength)
    {
        if (strength <= 0.0001f) return p;
        // Two offset directions to decorrelate warp
        float2 o1 = new float2(5.2f, 1.3f);
        float2 o2 = new float2(8.5f, 2.8f);

        float wx = noise.snoise(p + o1);
        float wz = noise.snoise(p + o2);
        float2 warp = new float2(wx, wz) * strength;
        return p + warp;
    }

    private static void SampleCombinedFBM(float2 p, int octaves, float lacunarity, float gain, out float baseSum, out float ridgeSum, out float ampSum)
    {
        octaves = math.max(1, octaves);

        baseSum = 0f;
        ridgeSum = 0f;
        ampSum = 0f;

        float freq = 1f;
        float amp = 0.5f;

        for (int i = 0; i < octaves; i++)
        {
            float sample = noise.snoise(p * freq);
            float baseVal = sample * 0.5f + 0.5f;
            float ridgeVal = 1f - math.abs(sample);
            ridgeVal *= ridgeVal;

            baseSum += baseVal * amp;
            ridgeSum += ridgeVal * amp;
            ampSum += amp;

            freq *= lacunarity;
            amp *= gain;
        }

        if (ampSum <= 0f)
        {
            ampSum = 1f;
        }
    }

    private static float SampleFBM(float2 p, int octaves, float lacunarity, float gain)
    {
        octaves = math.max(1, octaves);

        float sum = 0f;
        float accAmp = 0f;
        float amp = 0.5f;
        float freq = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float sample = noise.snoise(p * freq) * 0.5f + 0.5f;
            sum += sample * amp;
            accAmp += amp;

            freq *= lacunarity;
            amp *= gain;
        }

        float inv = math.rcp(math.max(accAmp, 0.0001f));
        return math.saturate(sum * inv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float value, float edge0, float edgeScale)
    {
        float t = (value - edge0) * edgeScale;
        t = math.saturate(t);
        return t * t * (3f - 2f * t);
    }
}
