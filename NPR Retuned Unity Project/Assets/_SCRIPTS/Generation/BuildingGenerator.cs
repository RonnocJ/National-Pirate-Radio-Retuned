using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class Building
{
    public BConType Type;
    public Vector3 Position;
    public Vector3 Normal;
    public GameObject Object;
    public Quaternion Rotation;
}
public class BuildingGenerator : MonoBehaviour
{
    [SerializeField] private float maxHeightDifference;
    [SerializeField] private LayerMask onTerrainMask;
    [SerializeField] private FoliageGenerator f;
    [SerializeField] private Transform houseParent;
    [SerializeField] private Transform antennaParent;

    public HashSet<RoadPiece> UncheckedSegments = new();
    private Dictionary<BConType, BConData> _bConDict = new();
    private Dictionary<(Vector2Int, BConType), Building> _buildingDict = new();
    private Dictionary<GameObject, Building> _buildingObjDict = new();
    private HashSet<Vector2Int> _checkedTiles = new();
    private RoadGenerator _r;
    private GeneratorSettings g => GeneratorSettings.root;
    private ObjectPool _housePool;
    private ObjectPool _antennaPool;
    private Vector3 _playerPos => PosUtil.GetWorldPos(VanController.root.transform.position);
    void Awake()
    {
        _r = GetComponentInParent<RoadGenerator>();

        foreach (var bCon in g.BCons)
        {
            _bConDict[bCon.constructName] = bCon.data;
        }
        _housePool = new ObjectPool();
        _antennaPool = new ObjectPool();
    }
    public IEnumerator GenerateBuildings()
    {
        foreach (var seg in UncheckedSegments)
        {
            AddBuildingOnRoad(seg, (BConType)Random.Range(0, _bConDict.Count));
        }

        Vector3 p = _playerPos;
        int pX = Mathf.FloorToInt(p.x / g.CellSize);
        int pZ = Mathf.FloorToInt(p.z / g.CellSize);

        for (int x = pX - g.PlaceDistance - g.CullMargin; x <= pX + g.PlaceDistance + g.CullMargin; x++)
        {
            for (int z = pZ - g.PlaceDistance - g.CullMargin; z <= pZ + g.PlaceDistance + g.CullMargin; z++)
            {
                AddBuildingOnTerrain(new Vector2Int(x * g.CellSize, z * g.CellSize), (BConType)Random.Range(0, _bConDict.Count));
            }            
        }

        UncheckedSegments.Clear();

        foreach (var b in _buildingDict.Values)
        {
            if (b.Object == null && Vector3.Distance(_playerPos, b.Position) < g.ViewDistance * g.CellSize * 0.75f) PlaceBuilding(b);
        }

        yield return null;
    }
    private void AddBuildingOnRoad(RoadPiece seg, BConType type)
    {
        BConData data = _bConDict[type];

        if (!data.GeneratesOnRoads) return;

        Vector3 forward = seg.EndPos - seg.StartPos;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        right.Normalize();

        Vector3 segmentMid = Vector3.Lerp(seg.StartPos, seg.EndPos, 0.5f);

        if (Vector3.Distance(segmentMid, Vector3.zero) < g.FlatFade) return;

        float sideSign = Random.value < 0.5f ? -1f : 1f;

        Vector3 lateralDir = right * sideSign;
        float lateralOffset = _r.halfWidth + data.Area.extents.z * 2f;
        Vector3 anchor = segmentMid + lateralDir * lateralOffset;

        Vector3 anchorHit = CheckPos(anchor);
        if (anchorHit == Vector3.zero) return;

        if (_buildingDict.ContainsKey((PosUtil.V3FloorToInt(anchorHit / data.MinSpacing), type))) return;

        List<Vector3> footprint = new()
        {
            CheckPos(anchor + lateralDir * data.Area.extents.z),
            CheckPos(anchor - lateralDir * data.Area.extents.z),
            CheckPos(anchor + forward * data.Area.extents.x),
            CheckPos(anchor - forward * data.Area.extents.x)
        };

        Vector3 n0 = Vector3.Cross(footprint[2] - footprint[0], footprint[1] - footprint[0]);
        Vector3 n1 = Vector3.Cross(footprint[3] - footprint[1], footprint[0] - footprint[1]);
        Vector3 normal = -(n0 + n1).normalized * sideSign;

        float minH = float.MaxValue;
        float maxH = float.MinValue;

        foreach (var p in footprint)
        {
            float h = Vector3.Dot(p - anchorHit, normal);
            minH = Mathf.Min(minH, h);
            maxH = Mathf.Max(maxH, h);
        }

        if (maxH - minH > maxHeightDifference) return;

        if (Random.value > data.SpawnChance) return;

        Vector3 facing = Vector3.ProjectOnPlane(-lateralDir, normal);

        facing.Normalize();

        Quaternion rotation = Quaternion.LookRotation(facing, normal);

        Building b = new Building
        {
            Type = type,
            Position = anchorHit,
            Normal = normal,
            Rotation = rotation
        };

        _buildingDict[(PosUtil.V3FloorToInt(anchorHit / data.MinSpacing), type)] = b;
    }
    private void AddBuildingOnTerrain(Vector2Int pos, BConType type)
    {
        if (!_checkedTiles.Add(pos)) return;

        BConData data = _bConDict[type];

        if (data.GeneratesOnRoads) return;

        Vector3 anchor = new Vector3(pos.x, 0, pos.y);

        Vector3 anchorHit = CheckPos(anchor);
        if (anchorHit == Vector3.zero) return;
        if (_buildingDict.ContainsKey((PosUtil.V3FloorToInt(anchorHit / data.MinSpacing), type))) return;
        if (Vector3.Distance(anchorHit, Vector3.zero) < g.FlatFade) return;

        var cols = Physics.OverlapSphere(PosUtil.GetLocalPos(anchorHit), Mathf.Max(data.Area.extents.z / 2f, data.Area.extents.x / 2f), onTerrainMask).ToList();
        cols.ForEach(c => { if ((onTerrainMask & (1 << c.gameObject.layer)) != 0 && c.gameObject.layer != 6) return; });

        List<Vector3> footprint = new()
        {
            CheckPos(anchor + Vector3.right * data.Area.extents.z),
            CheckPos(anchor - Vector3.right * data.Area.extents.z),
            CheckPos(anchor + Vector3.forward * data.Area.extents.x),
            CheckPos(anchor - Vector3.forward * data.Area.extents.x)
        };

        if (footprint.Exists(p => p == Vector3.zero)) return;

        Vector3 n0 = Vector3.Cross(footprint[2] - footprint[0], footprint[1] - footprint[0]);
        Vector3 n1 = Vector3.Cross(footprint[3] - footprint[1], footprint[0] - footprint[1]);
        Vector3 normal = -(n0 + n1).normalized;

        if (normal == Vector3.zero) return;

        Vector3 up = normal;
        Vector3 forwardSeed = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forwardSeed.sqrMagnitude < 0.001f)
        {
            forwardSeed = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        if (forwardSeed.sqrMagnitude < 0.001f)
        {
            forwardSeed = Vector3.forward;
        }

        forwardSeed.Normalize();

        Quaternion randomAroundNormal = Quaternion.AngleAxis(Random.Range(0f, 360f), up);
        Vector3 facing = randomAroundNormal * forwardSeed;

        Quaternion rotation = Quaternion.LookRotation(facing, up);

        float minH = float.MaxValue;
        float maxH = float.MinValue;

        foreach (var p in footprint)
        {
            float h = Vector3.Dot(p - anchorHit, normal);
            minH = Mathf.Min(minH, h);
            maxH = Mathf.Max(maxH, h);
        }

        if (maxH - minH > maxHeightDifference) return;

        if (Random.value > data.SpawnChance) return;

        foreach (var c in cols)
        {
            if (c.gameObject.layer == 6)
            {
                Vector3 tPos = PosUtil.GetWorldPos(c.transform.position);
                Vector2Int tilePos = new Vector2Int(
                    Mathf.FloorToInt(tPos.x / g.CellSize),
                    Mathf.FloorToInt(tPos.z / g.CellSize));

                if (g.TileDict.TryGetValue(tilePos, out var tile))
                {
                    foreach (var fol in tile.Foliage)
                    {
                        if (fol.Object != null) f.GetPoolAndParent(fol.Type).pool.Return(fol.Object);
                    }

                    tile.Foliage.Clear();
                }
            }
        }

        Building b = new Building
        {
            Type = type,
            Position = anchorHit,
            Normal = normal,
            Rotation = rotation
        };

        _buildingDict[(PosUtil.V3FloorToInt(anchorHit / data.MinSpacing), type)] = b;
    }
    private Vector3 CheckPos(Vector3 inPos)
    {
        if (Physics.Raycast(inPos + Vector3.up * 256f, Vector3.down, out RaycastHit hit, 512f, 1 << 6))
        {
            return hit.point;
        }
        return Vector3.zero;
    }
    private void PlaceBuilding(Building b)
    {
        var (pool, parent) = GetPoolAndParent(b.Type);
        GameObject buildingObj = pool.Get(_bConDict[b.Type].Prefabs, parent);

        if (_buildingObjDict.ContainsKey(buildingObj))
        {
            _buildingObjDict[buildingObj].Object = null;
            _buildingObjDict.Remove(buildingObj);
        }

        buildingObj.transform.SetPositionAndRotation(b.Position, b.Rotation);

        b.Object = buildingObj;
        _buildingObjDict[buildingObj] = b;
    }
    public (ObjectPool pool, Transform parent) GetPoolAndParent(BConType type)
    {
        switch (type)
        {
            case BConType.HouseSmall:
            case BConType.HouseLarge:
                if (_housePool.CreatedCount < g.SmallHouseLimit)
                {
                    _housePool.Prewarm(2, _bConDict[type].Prefabs, houseParent);
                }

                return (_housePool, houseParent);

            case BConType.AntennaTower:
                if (_antennaPool.CreatedCount < g.AntennaLimit)
                {
                    _antennaPool.Prewarm(2, _bConDict[BConType.AntennaTower].Prefabs, antennaParent);
                }

                return (_antennaPool, antennaParent);

            default:
                return (null, null);
        }
    }

}
