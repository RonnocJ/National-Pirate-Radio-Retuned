using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Foliage
{
    public FConType Type;
    public GameObject Object;
    public Vector3 Position;
    public float yRot;
    public Foliage(FConType type)
    {
        Type = type;
    }
}
public class FoliageGenerator : Singleton<FoliageGenerator>
{
    [Header("Foliage Settings")]
    [SerializeField] private float minSpacing = 1.25f;
    [SerializeField] private LayerMask terrainMask = Physics.DefaultRaycastLayers;
    [SerializeField] private LayerMask roadMask; // used to prevent placement where a road occludes ground
    [SerializeField] private float raycastHeight = 200f;

    [Header("Parents")]
    [SerializeField] private Transform treeParent;

    private ObjectPool _largeTreePool;
    private ObjectPool _smallTreePool;

    private readonly Dictionary<Vector3, Dictionary<FConType, List<Foliage>>> _tileFoliage = new();
    private readonly Dictionary<FConType, FConData> _folConDict = new();
    private readonly List<Vector3> _placedPositions = new();

    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;

    // --- Async removal queue for foliage intersecting new road segments ---
    private class RemoveTask
    {
        public Vector3 a;
        public Vector3 b;
        public float radius;
        public int minX, maxX, minZ, maxZ;
        public int curX, curZ;
    }
    private readonly Queue<RemoveTask> _removeQueue = new();
    [SerializeField] private int tilesToScanPerFrame = 48;
    IEnumerator Start()
    {
        foreach (var con in g.FCons)
        {
            if (con is FoliageConstruct fc)
            {
                _folConDict[fc.constructName] = fc.data;
            }
        }

        // Setup pools
        _largeTreePool = new ObjectPool();
        _smallTreePool = new ObjectPool();

        yield return null;
        yield return null;

        // Start background worker to clear foliage gradually
        StartCoroutine(RemoveWorker());

        while (true)
        {
            Vector3 p = _playerPos;
            int pX = Mathf.FloorToInt(p.x / g.CellSize);
            int pZ = Mathf.FloorToInt(p.z / g.CellSize);

            HashSet<Vector3> desiredTiles = new();

            for (int x = pX - g.ViewDistance- g.CullMargin; x <= pX + g.ViewDistance + g.CullMargin; x++)
            {
                for (int z = pZ - g.ViewDistance - g.CullMargin; z <= pZ + g.ViewDistance + g.CullMargin; z++)
                {
                    Vector3 pos = new Vector3(x * g.CellSize, 0, z * g.CellSize);
                    bool inView = Mathf.Abs(x - pX) <= g.ViewDistance && Mathf.Abs(z - pZ) <= g.ViewDistance;
                    bool inCull = Mathf.Abs(x - pX) <= g.ViewDistance + g.CullMargin && Mathf.Abs(z - pZ) <= g.ViewDistance+ g.CullMargin;

                    if (inCull)
                    {
                        desiredTiles.Add(pos);
                    }

                    if (inView)
                    {
                        EnsureTileLists(pos);
                        // For each type, try to populate up to MaxPerTile
                        foreach (var kvp in _folConDict)
                        {
                            FConType type = kvp.Key;
                            FConData data = kvp.Value;
                            int targetCount = Mathf.Max(0, data.MaxPerTile);

                            var list = _tileFoliage[pos][type];
                            int toAdd = targetCount - list.Count;

                            for (int i = 0; i < toAdd; i++)
                            {
                                TryPlaceFoliageOnTile(pos, type, data, maxAttempts: 8);
                            }
                        }
                    }
                }

                yield return null; // Spread workload
            }

            // Cull tiles outside desired range
            CullTilesOutside(desiredTiles);
        }
    }
    private void EnsureTileLists(Vector3 tilePos)
    {
        if (!_tileFoliage.TryGetValue(tilePos, out var perType))
        {
            perType = new Dictionary<FConType, List<Foliage>>();
            _tileFoliage[tilePos] = perType;
        }

        foreach (var t in System.Enum.GetValues(typeof(FConType)))
        {
            FConType type = (FConType)t;
            if (!perType.ContainsKey(type))
                perType[type] = new List<Foliage>();
        }
    }

    private void CullTilesOutside(HashSet<Vector3> desired)
    {
        // Collect keys to remove to avoid changing collection while iterating
        List<Vector3> toRemove = new();
        foreach (var kv in _tileFoliage)
        {
            if (!desired.Contains(kv.Key))
                toRemove.Add(kv.Key);
        }

        foreach (var tile in toRemove)
        {
            var perType = _tileFoliage[tile];
            foreach (var kvp in perType)
            {
                foreach (var fol in kvp.Value)
                {
                    if (fol.Object != null)
                    {
                        ReturnToPool(fol.Type, fol.Object);
                        _placedPositions.Remove(fol.Position);
                    }
                }
            }
            _tileFoliage.Remove(tile);
        }
    }

    private void TryPlaceFoliageOnTile(Vector3 tilePos, FConType type, FConData data, int maxAttempts)
    {
        // Tile square extents centered at tile origin
        float half = g.CellSize * 0.5f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float rx = Random.Range(-half, half);
            float rz = Random.Range(-half, half);
            Vector3 probe = new Vector3(tilePos.x + rx, raycastHeight, tilePos.z + rz);

            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, raycastHeight * 2f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 placePos = hit.point;

                // If a road lies between the probe and ground, skip this point
                float downDist = hit.distance;
                if (roadMask.value != 0 && Physics.SphereCast(probe, 3f, Vector3.down, out _, downDist, roadMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // Simple overlap check against all placed positions
                bool overlaps = false;
                float spacing = Mathf.Max(0.01f, minSpacing);
                for (int i = 0; i < _placedPositions.Count; i++)
                {
                    if ((placePos - _placedPositions[i]).sqrMagnitude < spacing * spacing)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps) continue;

                var (pool, parent) = GetPoolAndParent(type);
                if (pool == null || parent == null) return;

                GameObject go = pool.Get(data.Prefabs, parent);
                ApplyRandomTransform(go.transform, placePos, data);

                var fol = new Foliage(type)
                {
                    Object = go,
                    Position = placePos,
                    yRot = go.transform.eulerAngles.y
                };
                _tileFoliage[tilePos][type].Add(fol);
                _placedPositions.Add(placePos);

                return;
            }
        }
    }

    private void ApplyRandomTransform(Transform t, Vector3 position, FConData data)
    {
        t.position = position;
        float yRot = Random.Range(0f, 360f);
        t.rotation = Quaternion.Euler(0f, yRot, 0f);

        Vector3 min = data.MinScale;
        Vector3 max = data.MaxScale;

        float sx = Random.Range(Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x));
        float sy = Random.Range(Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y));
        float sz = Random.Range(Mathf.Min(min.z, max.z), Mathf.Max(min.z, max.z));
        t.localScale = new Vector3(sx, sy, sz);
    }

    private (ObjectPool pool, Transform parent) GetPoolAndParent(FConType type)
    {
        switch (type)
        {
            case FConType.LargeTree:
                return (_largeTreePool, treeParent);
            case FConType.SmallTree:
                return (_smallTreePool, treeParent);
            default:
                return (null, null);
        }
    }

    private void ReturnToPool(FConType type, GameObject go)
    {
        switch (type)
        {
            case FConType.LargeTree:
                _largeTreePool.Return(go);
                break;
            case FConType.SmallTree:
                _smallTreePool.Return(go);
                break;
        }
    }

    /// <summary>
    /// Schedule foliage removal along a road segment within given radius.
    /// </summary>
    public void ScheduleRemoveFoliageForSegment(Vector3 a, Vector3 b, float radius)
    {
        int cell = Mathf.Max(1, g.CellSize);
        float minXW = Mathf.Min(a.x, b.x) - radius;
        float maxXW = Mathf.Max(a.x, b.x) + radius;
        float minZW = Mathf.Min(a.z, b.z) - radius;
        float maxZW = Mathf.Max(a.z, b.z) + radius;

        var task = new RemoveTask
        {
            a = a,
            b = b,
            radius = radius,
            minX = Mathf.FloorToInt(minXW / cell),
            maxX = Mathf.FloorToInt(maxXW / cell),
            minZ = Mathf.FloorToInt(minZW / cell),
            maxZ = Mathf.FloorToInt(maxZW / cell),
            curX = 0,
            curZ = 0
        };
        _removeQueue.Enqueue(task);
    }

    private IEnumerator RemoveWorker()
    {
        while (true)
        {
            int budget = Mathf.Max(1, tilesToScanPerFrame);
            while (budget > 0 && _removeQueue.Count > 0)
            {
                var t = _removeQueue.Peek();
                if (t.curX == 0 && t.curZ == 0)
                {
                    t.curX = t.minX;
                    t.curZ = t.minZ;
                }

                int used = 0;
                for (; t.curX <= t.maxX && used < budget; t.curX++)
                {
                    for (; t.curZ <= t.maxZ && used < budget; t.curZ++)
                    {
                        Vector3 tilePos = new Vector3(t.curX * g.CellSize, 0f, t.curZ * g.CellSize);
                        RemoveFoliageOnTile(tilePos, t.a, t.b, t.radius);
                        used++;
                    }
                    t.curZ = t.minZ;
                }

                budget -= used;
                if (t.curX > t.maxX)
                {
                    _removeQueue.Dequeue();
                }
            }

            yield return null;
        }
    }

    private void RemoveFoliageOnTile(Vector3 tilePos, Vector3 a, Vector3 b, float radius)
    {
        if (!_tileFoliage.TryGetValue(tilePos, out var perType)) return;

        float r = Mathf.Max(0.01f, radius);
        List<Vector3> positionsToRemove = null;

        foreach (var kvp in perType)
        {
            var list = kvp.Value;
            if (list == null || list.Count == 0) continue;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                if (f?.Object == null) { list.RemoveAt(i); continue; }

                float d = DistancePointToSegmentXZ(f.Object.transform.position, a, b);
                if (d <= r)
                {
                    ReturnToPool(f.Type, f.Object);
                    if (positionsToRemove == null) positionsToRemove = new List<Vector3>();
                    positionsToRemove.Add(f.Position);
                    list.RemoveAt(i);
                }
            }
        }

        if (positionsToRemove != null)
        {
            for (int i = 0; i < positionsToRemove.Count; i++)
            {
                _placedPositions.Remove(positionsToRemove[i]);
            }
        }
    }

    private static float DistancePointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z);
        Vector2 A = new Vector2(a.x, a.z);
        Vector2 B = new Vector2(b.x, b.z);
        Vector2 AB = B - A;
        float ab2 = AB.sqrMagnitude;
        if (ab2 < 1e-6f) return Vector2.Distance(P, A);
        float t = Mathf.Clamp01(Vector2.Dot(P - A, AB) / ab2);
        Vector2 Q = A + t * AB;
        return Vector2.Distance(P, Q);
    }
}
