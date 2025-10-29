// File: Assets/Scripts/FoliageGenerator.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Foliage
{
    public FConType Type;
    public GameObject Object;
    public Vector3Int Position;
    public float yRot;
    public Foliage(FConType type) { Type = type; }
}

public class FoliageGenerator : MonoBehaviour
{
    [Header("Foliage Settings")]
    [SerializeField] private float minSpacing = 1.25f;
    [SerializeField] private LayerMask terrainMask = Physics.DefaultRaycastLayers;
    [SerializeField] private LayerMask roadMask;
    [SerializeField] private int raycastHeight = 200;

    [Header("Parents")]
    [SerializeField] private Transform treeParent;

    private ObjectPool _largeTreePool;
    private ObjectPool _smallTreePool;

    private Dictionary<GameObject, (Vector2Int tilePos, Vector3Int folPos)> _folObjDict = new();
    private Dictionary<FConType, FConData> _folConDict = new();

    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;

    public enum DistanceMetric { Euclidean = 0, Manhattan = 1, Chebyshev = 2 }
    public enum VoronoiValue { F1 = 0, F2 = 1, F2MinusF1 = 2 }

    [Header("Voronoi Gating")]

    [Tooltip("Size of a Voronoi cell in WORLD units. e.g. 256 for ~4× 64u tiles.")]
    [SerializeField] private float voronoiCellSize = 256f;

    [Tooltip("Global world-space offset to slide the pattern (prevents lining up with world origin).")]
    [SerializeField] private Vector2 voronoiWorldOffset = new Vector2(1000f, -500f);

    [Range(0f, 1.5f)]
    [SerializeField] private float voronoiJitter = 0.8f;

    [SerializeField] private DistanceMetric voronoiMetric = DistanceMetric.Euclidean;

    [Range(0f, 1f)]
    [Tooltip("Accept only points this deep inside cells. 0.2–0.35 is a good range.")]
    [SerializeField] private float interiorThreshold = 0.25f;
    private int voronoiSeed;

    void Start()
    {
        // Sets random voroni seed
        voronoiSeed = Mathf.RoundToInt(Random.value * 1000);

        // Adds all foliage constructs to dictionary

        foreach (var con in g.FCons)
        {
            if (con is FoliageConstruct fc)
            {
                _folConDict[fc.constructName] = fc.data;
            }
        }

        // Instantiate object pools for each type of foliage

        _largeTreePool = new ObjectPool();
        _smallTreePool = new ObjectPool();
    }

    public IEnumerator GenerateFoliage()
    {
        // Rounds player position to 1 / 64th scale

        Vector3 p = _playerPos;
        int pX = Mathf.FloorToInt(p.x / g.CellSize);
        int pZ = Mathf.FloorToInt(p.z / g.CellSize);

        // Iterates through all positons in a square around the player 

        for (int x = pX - g.PlaceDistance - g.CullMargin; x <= pX + g.PlaceDistance + g.CullMargin; x++)
        {
            for (int z = pZ - g.PlaceDistance - g.CullMargin; z <= pZ + g.CullMargin + g.PlaceDistance; z++)
            {
                // Only adds & places foliage within view distance, & if the tile at the position has not yet had a foliage generation pass

                if (Mathf.Abs(x - pX) > g.ViewDistance || Mathf.Abs(z - pZ) > g.ViewDistance) continue;

                Vector2Int pos = PosUtil.GetWorldPos(new Vector2Int(x, z));

                if (g.TileDict.ContainsKey(pos) && !g.TileDict[pos].GeneratedFoliage)
                {
                    // Adds all types of foliage to tile, as long as it's under the maximum

                    foreach (var kvp in _folConDict)
                    {
                        for (int i = 0; i < kvp.Value.MaxPerTile; i++)
                        {
                            var fol = AddFoliage(pos, kvp.Key);
                        }
                    }

                    g.TileDict[pos].GeneratedFoliage = true;
                }

                // Places foliage at stored position if such a position exists & has foliage on the tile

                if (g.TileDict.ContainsKey(pos) && g.TileDict[pos].Foliage.Count > 0) PlaceFoliage(pos);
            }
            yield return null;
        }

        // Sorts all objects in pool by distance to player (improves enqueuing / dequeuing behavior)

        _largeTreePool.SortActiveByDistance(p);
        _smallTreePool.SortActiveByDistance(p);
    }

    private Foliage AddFoliage(Vector2Int tilePos, FConType type)
    {
        // Gets random position on tile

        float half = g.CellSize * 0.5f;
        float rx = Random.Range(-half, half);
        float rz = Random.Range(-half, half);

        // Candidate world position (XZ)

        float worldX = (tilePos.x * g.CellSize) + rx;
        float worldZ = (tilePos.y * g.CellSize) + rz;

        // Early Voronoi reject; world-space & tile-agnostic

        if(Mathf.Abs(worldX) < 32f && Mathf.Abs(worldZ) < 32f) return null;

        if (!PassesVoronoiGate(worldX, worldZ) && Random.value > 0.01f) return null;

        Vector3 globalProbe = new Vector3(worldX, raycastHeight, worldZ);
        Vector3 probe = PosUtil.GetLocalPos(globalProbe);

        // Checks if foliage is within minimum distance of other foliage on the tile

        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, raycastHeight * 2f, terrainMask))
        {
            Vector3Int placePos = Vector3Int.FloorToInt(hit.point) - Vector3Int.up;

            if (Physics.SphereCast(probe, 3f, Vector3.down, out _, raycastHeight * 2f, roadMask)) return null;

            foreach (var f in g.TileDict[tilePos].Foliage)
            {
                if (Vector3Int.Distance(placePos, f.Position) < minSpacing) return null;
            }
            float yRot = Random.Range(0f, 360f);

            var fol = new Foliage(type)
            {
                Position = placePos,
                yRot = yRot
            };

            g.TileDict[tilePos].Foliage.Add(fol);

            // Sets y position to raycast hit point on tile

            Vector3 point = hit.point; point.y = 0f;

            // Adds obstacle at foliage position to pathfinding graph

            Vector3 worldPoint = PosUtil.GetWorldPos(point);
            Vector2Int cellKey = PosUtil.V3RoundToInt(worldPoint / PfGraph.root.CellSize) * PfGraph.root.CellSize;

            if (!PfGraph.root.CellDict.TryGetValue(cellKey, out PfCell cell))
            {
                cell = new PfCell(cellKey);
                PfGraph.root.CellDict[cellKey] = cell;
            }

            Vector3 worldObstacle = PosUtil.GetWorldPos((Vector3)fol.Position);
            Vector2Int obstaclePos = new Vector2Int(Mathf.RoundToInt(worldObstacle.x), Mathf.RoundToInt(worldObstacle.z));
            cell.Obstacles.Add(obstaclePos);

            return fol;
        }

        return null;
    }

    private void PlaceFoliage(Vector2Int tilePos)
    {
        // Places and rotates all foliage needed of a tile if it has not already been placed
        
        if (!g.TileDict.TryGetValue(tilePos, out var tile)) return;

        foreach (var f in tile.Foliage)
        {
            if (f.Object != null) continue;

            if (!Physics.Raycast(f.Position + Vector3.up * raycastHeight, Vector3.down, raycastHeight * 2f, terrainMask)) return;

            if (Physics.SphereCast(f.Position + Vector3.up * raycastHeight, 3f, Vector3.down, out _, raycastHeight * 2f, roadMask)) return;

            var (pool, parent) = GetPoolAndParent(f.Type);

            GameObject obj = pool.Get(_folConDict[f.Type].Prefabs, parent);

            if (_folObjDict.TryGetValue(obj, out var positions))
            {
                if (g.TileDict[positions.tilePos].Foliage.Contains(f))
                {
                    int i = g.TileDict[positions.tilePos].Foliage.IndexOf(f);
                    g.TileDict[positions.tilePos].Foliage[i].Object = null;
                }

                _folObjDict.Remove(obj);
            }

            obj.transform.position = f.Position;
            obj.transform.rotation = Quaternion.Euler(0f, f.yRot, 0f);
            obj.transform.localScale = Vector3.one * Random.Range(_folConDict[f.Type].MinScale, _folConDict[f.Type].MaxScale);
            f.Object = obj;

            _folObjDict[obj] = (tilePos, f.Position);
        }
    }

    public (ObjectPool pool, Transform parent) GetPoolAndParent(FConType type)
    {
        //Quickly access transform target and object pool from construct

        switch (type)
        {
            case FConType.LargeTree:
                if (_largeTreePool.CreatedCount < g.LargeTreeLimit)
                {
                    _largeTreePool.Prewarm(2, _folConDict[FConType.LargeTree].Prefabs, treeParent);
                }
                return (_largeTreePool, treeParent);

            case FConType.SmallTree:
                if (_smallTreePool.CreatedCount < g.SmallTreeLimit)
                {
                    _smallTreePool.Prewarm(2, _folConDict[FConType.SmallTree].Prefabs, treeParent);
                }
                return (_smallTreePool, treeParent);

            default:
                return (null, null);
        }
    }

    //Voroni noise generator stuff

    private bool PassesVoronoiGate(float worldX, float worldZ)
    {
        float n = VoronoiCPU.Sample(
            worldX + voronoiWorldOffset.x,
            worldZ + voronoiWorldOffset.y,
            1f / voronoiCellSize,
            voronoiSeed,
            Mathf.Max(0f, voronoiJitter),
            voronoiMetric,
            VoronoiValue.F2MinusF1
        );

        return n >= interiorThreshold;
    }

    private static class VoronoiCPU
    {
        static float Denom(DistanceMetric m) =>
            m == DistanceMetric.Euclidean ? 1.41421356237f :
            (m == DistanceMetric.Manhattan ? 2f : 1f);

        public static float Sample(
            float worldX, float worldZ,
            float scale, int seed, float jitter,
            DistanceMetric metric, VoronoiValue valueType)
        {
            float px = worldX * scale;
            float pz = worldZ * scale;

            int ix = FastFloor(px);
            int iz = FastFloor(pz);

            float f1 = float.PositiveInfinity;
            float f2 = float.PositiveInfinity;

            for (int dz = -1; dz <= 1; dz++)
            {
                int cz = iz + dz;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx;

                    uint h = Hash2D(cx, cz, seed);
                    Vector2 r = Rand2(h);
                    float fx = cx + 0.5f + (r.x - 0.5f) * jitter;
                    float fz = cz + 0.5f + (r.y - 0.5f) * jitter;

                    float d = Dist(px, pz, fx, fz, metric);

                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) { f2 = d; }
                }
            }

            float raw = valueType == VoronoiValue.F1 ? f1 :
                        (valueType == VoronoiValue.F2 ? f2 : (f2 - f1));

            return Mathf.Clamp01(raw / Denom(metric));
        }

        static int FastFloor(float x) => (x >= 0) ? (int)x : (int)x - 1;

        static float Dist(float ax, float az, float bx, float bz, DistanceMetric m)
        {
            float dx = Mathf.Abs(ax - bx);
            float dz = Mathf.Abs(az - bz);
            if (m == DistanceMetric.Manhattan) return dx + dz;
            if (m == DistanceMetric.Chebyshev) return (dx > dz) ? dx : dz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static uint SplitMix32(uint z)
        {
            z += 0x9E3779B9u;
            z = (z ^ (z >> 16)) * 0x85ebca6bu;
            z = (z ^ (z >> 13)) * 0xc2b2ae35u;
            z ^= z >> 16;
            return z;
        }

        static uint Hash2D(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 0x27d4eb2du;
                h = (h << 15) | (h >> 17);
                h ^= (uint)y * 0x165667b1u;
                h *= 0x85ebca6bu;
                h ^= h >> 13;
                h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        static Vector2 Rand2(uint h)
        {
            uint hx = SplitMix32(h);
            uint hy = SplitMix32(h ^ 0x9E3779B9u);
            return new Vector2((hx & 0x00FFFFFFu) * (1f / 16777216f),
                               (hy & 0x00FFFFFFu) * (1f / 16777216f));
        }
    }
}
