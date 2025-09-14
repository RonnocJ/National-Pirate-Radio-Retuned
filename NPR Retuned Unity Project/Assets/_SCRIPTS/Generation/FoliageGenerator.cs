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

    private Dictionary<Vector3, Dictionary<Vector3, Foliage>> _foliageDict = new();
    private Dictionary<GameObject, (Vector3 tilePos, Vector3 folPos)> _folObjDict = new();
    private Dictionary<FConType, FConData> _folConDict = new();

    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;
    void Start()
    {
        foreach (var con in g.FCons)
        {
            if (con is FoliageConstruct fc)
            {
                _folConDict[fc.constructName] = fc.data;
            }
        }

        _largeTreePool = new ObjectPool();
        _smallTreePool = new ObjectPool();
    }
    public IEnumerator GenerateFoliage()
    {
        Vector3 p = _playerPos;
        int pX = Mathf.FloorToInt(p.x / g.CellSize);
        int pZ = Mathf.FloorToInt(p.z / g.CellSize);

        HashSet<Vector3> desiredTiles = new();

        for (int x = pX - g.ViewDistance - g.CullMargin; x <= pX + g.ViewDistance + g.CullMargin; x++)
        {
            for (int z = pZ - g.ViewDistance - g.CullMargin; z <= pZ + g.ViewDistance + g.CullMargin; z++)
            {
                Vector3 pos = new Vector3(x * g.CellSize, 0, z * g.CellSize);

                if (!_foliageDict.ContainsKey(pos))
                {
                    foreach (var kvp in _folConDict)
                    {
                        for (int i = 0; i < kvp.Value.MaxPerTile; i++)
                        {
                            AddFoliage(pos, kvp.Key);
                        }
                    }
                }

                foreach (var _ in _folConDict)
                {
                    if(_foliageDict.ContainsKey(pos)) PlaceFoliage(pos);
                }
            }

            yield return null;
        }
    }
    private void AddFoliage(Vector3 tilePos, FConType type)
    {
        var (checkPool, checkParent) = GetPoolAndParent(type);
        if (checkPool == null || checkParent == null) return;

        float half = g.CellSize * 0.5f;

        float rx = Random.Range(-half, half);
        float rz = Random.Range(-half, half);
        Vector3 probe = new Vector3(tilePos.x + rx, raycastHeight, tilePos.z + rz);

        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, raycastHeight * 2f, terrainMask))
        {
            Vector3 placePos = hit.point - Vector3.up;

            if (Physics.SphereCast(probe, 3f, Vector3.down, out _, raycastHeight * 2f, roadMask)) return;

            if(!_foliageDict.ContainsKey(tilePos)) _foliageDict[tilePos] = new Dictionary<Vector3, Foliage>();

            bool overlaps = false;
            foreach (var f in _foliageDict[tilePos].Values)
            {
                if ((placePos - f.Position).sqrMagnitude < minSpacing)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps) return;

            float yRot = Random.Range(0f, 360f);

            var fol = new Foliage(type)
            {
                Object = null,
                Position = placePos,
                yRot = yRot
            };

            _foliageDict[tilePos][placePos] = fol;
        }
    }
    private void PlaceFoliage(Vector3 tilePos)
    {
        foreach (var kvp in _foliageDict[tilePos])
        {
            var keyPos = kvp.Key;
            var fol = kvp.Value;
            if (fol.Object != null) continue;


            if (!Physics.Raycast(fol.Position + Vector3.up * raycastHeight, Vector3.down, out RaycastHit hit, raycastHeight * 2f, terrainMask)) return;

            fol.Position.y = hit.point.y;

            if (Physics.SphereCast(fol.Position + Vector3.up * raycastHeight, 3f, Vector3.down, out _, raycastHeight * 2f, roadMask)) return;

            var (pool, parent) = GetPoolAndParent(fol.Type);

            GameObject obj = pool.Get(_folConDict[fol.Type].Prefabs, parent);

            if (_folObjDict.TryGetValue(obj, out var positions))
            {
                if (_foliageDict.TryGetValue(positions.tilePos, out var inner) && inner.ContainsKey(positions.folPos))
                {
                    inner[positions.folPos].Object = null;
                }
                
                _folObjDict.Remove(obj);
            }

            obj.transform.position = fol.Position;
            obj.transform.rotation = Quaternion.Euler(0f, fol.yRot, 0f);
            obj.transform.localScale = Vector3.one * Random.Range(_folConDict[fol.Type].MinScale, _folConDict[fol.Type].MaxScale);
            fol.Object = obj;

            _folObjDict[obj] = (tilePos, keyPos);
        }
    }
    private (ObjectPool pool, Transform parent) GetPoolAndParent(FConType type)
    {
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
    public void RemoveFoliage(GameObject obj)
    {
        // Find the mapped pooled object by walking up the hierarchy
        GameObject key = null;
        var t = obj != null ? obj.transform : null;
        while (t != null)
        {
            if (_folObjDict.ContainsKey(t.gameObject))
            {
                key = t.gameObject;
                break;
            }
            t = t.parent;
        }

        if (key == null) return;

        var positions = _folObjDict[key];
        if (!_foliageDict.TryGetValue(positions.tilePos, out var perTile))
        {
            _folObjDict.Remove(key);
            return;
        }

        if (!perTile.TryGetValue(positions.folPos, out var fol))
        {
            _folObjDict.Remove(key);
            _foliageDict[positions.tilePos].Remove(positions.folPos);
            return;
        }

        ReturnToPool(fol.Type, fol.Object);
        perTile.Remove(positions.folPos);
        _folObjDict.Remove(key);
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

}
