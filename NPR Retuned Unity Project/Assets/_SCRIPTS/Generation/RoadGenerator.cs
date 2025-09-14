using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;

public class Road
{
    public List<RoadPiece> Segments = new();
    public int SegsSinceIntersection;
    public Vector3 NextPos;
    public Vector3 NextDir;
    public bool IsActive = true;

    public Road(Vector3 startPos, Vector3 startDir)
    {
        NextPos = startPos;
        NextDir = startDir;
    }
}
public class RoadPiece
{
    public RConType Type;
    public Vector3[] VertexPos;
    public Vector3 StartPos;
    public Vector3 StartDir;
    public Vector3 EndPos;
    public Vector3 EndDir;
    public GameObject Object;
}

public struct RoadNode
{
    public Vector3 pos;
    public Vector3 dir;
    public float height;
}

public class RoadGenerator : Singleton<RoadGenerator>
{
    [Header("Road Shape")]
    [SerializeField] private float bendVarience = 8f;
    [SerializeField] private int roadSteps = 12;
    [SerializeField] private Transform roadParent;
    [SerializeField] private Transform intersectionParent;

    [Header("Road Width / Height")]
    [SerializeField] private float halfWidth = 16f;
    [SerializeField] private float roadHeightOffset;
    [SerializeField] private LayerMask roadDetectMask;

    [Header("Intersections")]
    [SerializeField, Min(0)] private int minSegmentsBeforeIntersection = 8;
    [SerializeField, Range(0f, 1f)] private float intersectionChance = 0.2f;
    private ObjectPool _roadPool;
    private ObjectPool _tIntersectionPool;
    private ObjectPool _xIntersectionPool;


    private Dictionary<RConType, RConData> _rConDict = new();
    private readonly List<RoadNode> _roadNodes = new();
    private readonly List<Road> _roads = new();
    private readonly Dictionary<GameObject, RoadPiece> _roadObjMap = new();


    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;

    void Start()
    {
        foreach (var con in g.RCons)
        {
            if (con is RoadConstruct rc)
            {
                _rConDict[rc.constructName] = rc.data;
            }
        }
        _roadPool = new ObjectPool();
        _tIntersectionPool = new ObjectPool();
        _xIntersectionPool = new ObjectPool();

        _roads.Add(new Road(Vector3.forward * 96f, Vector3.forward));
        _roads[0].IsActive = true;
    }
    public IEnumerator GenerateRoads()
    {
        for (int rIndex = 0; rIndex < _roads.Count; rIndex++)
        {
            Road road = _roads[rIndex];
            Vector3 endPos = road.NextPos;
            Vector3 endDir = road.NextDir;

            if (Vector3.Distance(_playerPos, endPos) <= g.ViewDistance * g.CellSize)
            {
                for (int i = 0; i < roadSteps; i++)
                {
                    if (!road.IsActive) break; ;
                    if (TrySpawnIntersection(road)) break;

                    RoadPiece seg = new RoadPiece();
                    seg.Type = RConType.Road;

                    AddRoad(seg, endPos, endDir);

                    seg.StartPos = endPos;
                    road.Segments.Add(seg);

                    endPos = seg.EndPos;
                    endDir = seg.EndDir;

                    road.SegsSinceIntersection++;

                    road.NextPos = endPos;
                    road.NextDir = endDir;
                    yield return null;
                }

                foreach (RoadPiece seg in _roads[rIndex].Segments)
                {
                    if (Vector3.Distance(_playerPos, seg.StartPos) <= g.ViewDistance * g.CellSize)
                    {
                        PlaceRoads(seg);
                    }
                    yield return null;
                }
            }
        }

        PruneRoadNodes();
    }
    public void AddRoad(RoadPiece road, Vector3 startPos, Vector3 startDir)
    {
        var baseMesh = _rConDict[road.Type].SegmentPrefab.GetComponent<MeshFilter>().sharedMesh;
        road.VertexPos = baseMesh.vertices;

        Vector3 currentPos;
        Vector3 centerPos = startPos;
        Vector3 currentDir = new Vector3(startDir.x, 0f, startDir.z).normalized;
        road.StartDir = currentDir;

        bool isIntersection = road.Type != RConType.Road;
        Quaternion interRot = Quaternion.identity;
        if (isIntersection)
        {
            interRot = Quaternion.LookRotation(currentDir, Vector3.up);
        }

        for (int i = 0; i < baseMesh.vertices.Length; i++)
        {
            if (road.Type == RConType.Road)
            {
                Vector3 right = Vector3.Cross(Vector3.up, currentDir).normalized;

                if (i % 4 == 0)
                {
                    currentPos = centerPos - right * halfWidth * 2;
                    currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) - roadHeightOffset;
                }
                else if (i % 4 == 1)
                {
                    currentPos = centerPos - right * halfWidth;
                    currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) + roadHeightOffset;
                }
                else if (i % 4 == 2)
                {
                    currentPos = centerPos + right * halfWidth;
                    currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) + roadHeightOffset;
                }
                else
                {
                    currentPos = centerPos + right * halfWidth * 2;
                    currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) - roadHeightOffset;


                    currentDir = Quaternion.AngleAxis(Random.Range(-bendVarience, bendVarience), Vector3.up) * currentDir;
                    currentDir = new Vector3(currentDir.x, 0f, currentDir.z).normalized;
                    centerPos += halfWidth * currentDir;
                }
                if (i == 32)
                {
                    road.EndPos = centerPos;
                    road.EndDir = currentDir;
                }
            }
            else
            {
                Vector3 center = startPos + (currentDir * 4f * halfWidth);
                currentPos = center + interRot * baseMesh.vertices[i] * 2f;

                Vector3 right = Vector3.Cross(Vector3.up, currentDir).normalized;
                float a = Mathf.RoundToInt(Vector3.Dot(currentPos - center, right));
                float b = Mathf.RoundToInt(Vector3.Dot(currentPos - center, currentDir));

                bool isInner;

                if (road.Type == RConType.XIntersection) isInner = Mathf.Abs(a) <= halfWidth || Mathf.Abs(b) <= halfWidth;
                else isInner = Mathf.Abs(a) <= halfWidth && b < halfWidth || Mathf.Abs(b) <= halfWidth;

                currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) + (isInner ? roadHeightOffset : -roadHeightOffset);
            }

            road.VertexPos[i] = currentPos;

            _roadNodes.Add(new RoadNode { pos = currentPos, dir = currentDir.normalized, height = currentPos.y });
        }
    }
    private bool TrySpawnIntersection(Road road)
    {
        if (road.SegsSinceIntersection < minSegmentsBeforeIntersection)
            return false;
        if (Random.value > intersectionChance)
            return false;

        RConType interType = Random.value < 0.5f ? RConType.XIntersection : RConType.TIntersection;

        Vector3 leftDir = Quaternion.Euler(Vector3.up * -90f) * road.NextDir;
        Vector3 rightDir = Quaternion.Euler(Vector3.up * 90f) * road.NextDir;

        RoadPiece seg = new RoadPiece();
        seg.Type = interType;
        seg.StartPos = road.NextPos;

        AddRoad(seg, road.NextPos, road.NextDir);

        road.Segments.Add(seg);
        road.IsActive = false;

        _roads.Add(CreateBranch(road.NextPos + (road.NextDir * halfWidth * 4) + (leftDir * halfWidth * 4), leftDir));
        _roads.Add(CreateBranch(road.NextPos + (road.NextDir * halfWidth * 4) + (rightDir * halfWidth * 4), rightDir));
        if (seg.Type == RConType.XIntersection) _roads.Add(CreateBranch(road.NextPos + road.NextDir * halfWidth * 8, road.NextDir));

        Road CreateBranch(Vector3 start, Vector3 dir)
        {
            Road newRoad = new Road(start, dir);
            RoadPiece seg = new RoadPiece();

            seg.Type = RConType.Road;
            seg.StartPos = start;
            newRoad.Segments.Add(seg);

            AddRoad(seg, start, dir);

            newRoad.NextPos = seg.EndPos;
            newRoad.NextDir = seg.EndDir;
            newRoad.SegsSinceIntersection = 0;

            return newRoad;
        }

        return true;
    }

    private void PlaceRoads(RoadPiece seg)
    {
        if (seg.Object != null) return;

        var (pool, parent, data, limit) = GetPoolAndParent(seg.Type);
        if (pool.CreatedCount < limit) pool.Prewarm(2, data.SegmentPrefab, parent);

        GameObject obj = pool.Get(data.SegmentPrefab, parent);

        if (_roadObjMap.TryGetValue(obj, out var previousSeg) && previousSeg != null)
        {
            previousSeg.Object = null;
            _roadObjMap.Remove(obj);
        }

        Vector3 min = seg.VertexPos[0];
        Vector3 max = seg.VertexPos[0];
        for (int i = 1; i < seg.VertexPos.Length; i++)
        {
            var v = seg.VertexPos[i];
            if (v.x < min.x) min.x = v.x; if (v.y < min.y) min.y = v.y; if (v.z < min.z) min.z = v.z;
            if (v.x > max.x) max.x = v.x; if (v.y > max.y) max.y = v.y; if (v.z > max.z) max.z = v.z;
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 halfExtents = (max - min) * 0.5f;
        // Pad a bit to ensure full coverage, especially for intersections
        halfExtents += new Vector3(halfWidth, halfWidth, halfWidth);

        var overlaps = Physics.OverlapBox(center, halfExtents, Quaternion.identity, roadDetectMask);
        for (int j = 0; j < overlaps.Length; j++)
        {
            switch (overlaps[j].gameObject.layer)
            {
                case 6:
                    PfGraph.root.PfDict[PfGraph.root.V3ToInt(overlaps[j].transform.position)].Navigable = true;

                    break;

                case 8:
                    FoliageGenerator.root.RemoveFoliage(overlaps[j].gameObject);

                    break;
            }

        }

        Mesh m = obj.GetComponent<MeshFilter>().mesh;
        m.vertices = seg.VertexPos;
        m.RecalculateBounds();
        m.RecalculateNormals();

        var col = obj.GetComponent<MeshCollider>();
        if (col != null)
        {
            col.sharedMesh = null;
            col.sharedMesh = m;
        }

        seg.Object = obj;
        _roadObjMap[obj] = seg;

    }

    private void PruneRoadNodes()
    {
        float keepDist = (g.ViewDistance + g.CullMargin) * g.CellSize + 64f;
        Vector3 p = _playerPos;
        for (int i = _roadNodes.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(p, _roadNodes[i].pos) > keepDist)
                _roadNodes.RemoveAt(i);

        }
    }

    private (ObjectPool pool, Transform parent, RConData data, int limit) GetPoolAndParent(RConType type)
    {
        switch (type)
        {
            case RConType.Road:
                return (_roadPool, roadParent, _rConDict[RConType.Road], g.RoadLimit);

            case RConType.TIntersection:
                return (_tIntersectionPool, intersectionParent, _rConDict[RConType.TIntersection], g.TIntersectionLimit);

            case RConType.XIntersection:
                return (_xIntersectionPool, intersectionParent, _rConDict[RConType.XIntersection], g.XIntersectionLimit);

            default:
                return (null, null, null, 0);
        }
    }
}
