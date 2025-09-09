using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "GeneratorSettings", menuName = "Objects/World/GeneratorSettings", order = 0)]
public class GeneratorSettings : ScriptableSingleton<GeneratorSettings>
{
    [Header("Grid Settings")]
    public int ViewDistance;
    public int CullMargin;
    public int CellSize;
    [Header("Noise Settings")]
    public float NoiseScale;
    public float HeightScale;
    public AnimationCurve InitialSlope;
    [Header("Construct References")]
    public TileConstruct[] TCons;
    public RoadConstruct[] RCons;
    public FoliageConstruct[] FCons;

    public float GetPerlinHeight(Vector3 inPos)
    {
        float xCoord = inPos.x / NoiseScale;
        float zCoord = inPos.z / NoiseScale;

        float amplitude = InitialSlope.Evaluate(inPos.sqrMagnitude / NoiseScale) * Mathf.PerlinNoise(xCoord / 8, zCoord / 8) * HeightScale;
        float n = noise.snoise(new float2(xCoord, zCoord)) * 0.5f + 0.5f;

        return inPos.y + n * amplitude;
    }
}