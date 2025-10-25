using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;
using Unity.Mathematics;

public class Road
{
    public List<RoadPiece> Segments = new();
    public int SegsSinceIntersection;
    public Vector3 NextPos;
    public Vector3 NextDir;
    public bool IsActive = true;
    public List<Building> Buildings;
    public Road(Vector3 startPos, Vector3 startDir)
    {
        NextPos = startPos;
        NextDir = startDir;
    }
}
public class RoadPiece
{
    public Road Parent;
    public RConType Type;
    public Vector3[] VertexPos;
    public Vector3[] Centers;
    public Vector3 StartPos;
    public Vector3 EndPos;
    public Vector3 EndDir;
    public GameObject Object;
    public bool HasIntersected;
}

public struct RoadNode
{
    public Vector3 pos;
    public Vector3 dir;
    public float height;
}

public class RoadGenerator : Singleton<RoadGenerator>
{
    [SerializeField] private FoliageGenerator f;
    [SerializeField] private BuildingGenerator b;
    [Header("Road Shape")]
    [SerializeField] private float bendVarience = 8f;
    [SerializeField] private int _roadsteps = 12;
    [SerializeField] private Transform roadParent;
    [SerializeField] private Transform intersectionParent;

    [Header("Road Width / Height")]
    public float halfWidth = 16f;
    [SerializeField] private float roadHeightOffset;
    [SerializeField] private LayerMask roadDetectMask;

    [Header("Intersections")]
    [SerializeField, Min(0)] private int minSegmentsBeforeIntersection = 8;
    [SerializeField, Range(0f, 1f)] private float intersectionChance = 0.2f;
    private ObjectPool _roadPool;
    private ObjectPool _tIntersectionPool;
    private ObjectPool _xIntersectionPool;

    private List<Road> _roads = new();
    private Dictionary<RConType, RConData> _rConDict = new();
    private readonly List<RoadNode> _roadNodes = new();
    public readonly Dictionary<GameObject, RoadPiece> _roadObjMap = new();
    private readonly Dictionary<Vector2Int, RoadTileOccupancy> _roadTileSystem = new();

    private const float TileKeySize = 32f;

    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;

    private struct RoadTileOccupancy
    {
        public RoadPiece Primary;
        public RoadPiece Secondary;

        public void Add(RoadPiece piece)
        {
            if (piece == null) return;

            if (Primary == null)
            {
                Primary = piece;
                return;
            }

            if (Primary == piece) return;

            if (Secondary == null || Secondary == piece)
            {
                Secondary = piece;
                return;
            }

            Secondary = piece;
        }
    }

    void Start()
    {
        // Adds all road constructs to dictionary

        foreach (var con in g.RCons)
        {
            if (con is RoadConstruct rc)
            {
                _rConDict[rc.constructName] = rc.data;
            }
        }

        // Instantiate object pools for each type of road

        _roadPool = new ObjectPool();
        _tIntersectionPool = new ObjectPool();
        _xIntersectionPool = new ObjectPool();

        // Seed the system with two starter roads pointing in opposite directions

        Road road1 = new Road(Vector3.forward * 64f, Vector3.right);
        Road road2 = new Road(Vector3.forward * 64f, -Vector3.right);
        road1.IsActive = true;
        road2.IsActive = true;

        _roads.Add(road1);
        _roads.Add(road2);

    }
    public IEnumerator GenerateRoads()
    {
        // Track player position in world space to determine when to stop growing roads

        Vector3 p = PosUtil.GetWorldPos(_playerPos);
        int roadCount = _roads.Count;

        // Extend each active road a few steps per frame.
        for (int i = 0; i < roadCount; i++)
        {
            Vector3 endPos = _roads[i].NextPos;
            Vector3 endDir = _roads[i].NextDir;

            // Grow the current road one segment at a time until we hit a stop condition

            for (int j = 0; j < _roadsteps; j++)
            {
                p = PosUtil.GetWorldPos(_playerPos);

                if (Vector3.Distance(p, endPos) > g.PlaceDistance * g.CellSize) break;
                if (!_roads[i].IsActive) break;

                // Branch off into intersections when the current road qualifies

                if (TrySpawnIntersection(_roads[i])) break;

                // Create a new road segment that continues from the previous endpoint

                RoadPiece seg = new RoadPiece();
                seg.Parent = _roads[i];
                seg.Type = RConType.Road;

                AddRoad(seg, endPos, endDir, _roads[i].Segments.Count > 0 ? _roads[i].Segments[_roads[i].Segments.Count - 1] : null);

                seg.StartPos = endPos;
                _roads[i].Segments.Add(seg);

                b.UncheckedSegments.Add(seg);

                endPos = seg.EndPos;
                endDir = seg.EndDir;

                _roads[i].SegsSinceIntersection++;

                _roads[i].NextPos = endPos;
                _roads[i].NextDir = endDir;

            }
            yield return null;

            // Activate pooled meshes for segments that move within the view distance

            foreach (RoadPiece seg in _roads[i].Segments)
            {
                if (Vector3.Distance(p, seg.StartPos) <= g.ViewDistance * g.CellSize * 0.75f)
                {
                    PlaceRoads(seg);
                }

            }
            yield return null;

        }

        Vector3 sortPos = PosUtil.GetWorldPos(_playerPos);

        // Remove far nodes and reprioritize pooled objects around the current player position

        PruneRoadNodes();
        _roadPool.SortActiveByDistance(sortPos);
        _tIntersectionPool.SortActiveByDistance(sortPos);
        _xIntersectionPool.SortActiveByDistance(sortPos);
    }
    public void AddRoad(RoadPiece road, Vector3 startPos, Vector3 startDir, RoadPiece previousRoad = null)
    {
        // Build geometry for this road piece and update bookkeeping used by the generators

        var baseMesh = _rConDict[road.Type].SegmentPrefab.GetComponent<MeshFilter>().sharedMesh;
        int vertexCount = baseMesh.vertices.Length;
        int crossSectionCount = Mathf.Max(1, vertexCount / 4);

        road.StartPos = startPos;

        // Flatten direction so roads stay level even if startDir has vertical noise

        Vector3 flatStartDir = new Vector3(startDir.x, 0f, startDir.z);
        if (flatStartDir.sqrMagnitude < 0.0001f)
        {
            flatStartDir = Vector3.forward;
        }
        flatStartDir = flatStartDir.normalized;

        Vector3 rerouteTarget = Vector3.zero;
        Vector3 rerouteDir = Vector3.zero;
        bool needsReroute = false;

        // Prevent the road from clipping into tiles it already owns

        bool TryFindSelfBlocker(Vector3 samplePosition, out RoadPiece blocker)
        {
            blocker = null;

            if (!TryGetRoadTile(samplePosition, out var occupancy))
                return false;

            return TryGetRoadWithParent(occupancy, road.Parent, road, previousRoad, out blocker);
        }

        // Discourage roads from orbiting the origin so the network keeps spreading outward

        Vector3 SteerAwayFromOrigin(Vector3 direction, Vector3 anchor)
        {
            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
            if (flatDir.sqrMagnitude < 0.0001f)
                return flatDir;

            flatDir = flatDir.normalized;

            Vector3 flatAnchor = new Vector3(anchor.x, 0f, anchor.z);
            float radius = flatAnchor.magnitude;
            if (radius < 0.0001f)
                return flatDir;

            float threshold = Mathf.Max(g.FlatThreshold, 0.01f);
            if (radius >= threshold)
                return flatDir;

            Vector3 away = flatAnchor.normalized;
            float heading = Vector3.Dot(flatDir, -away);
            if (heading <= 0f)
                return flatDir;

            float influence = (1f - radius / threshold) * heading;
            Vector3 adjusted = Vector3.Slerp(flatDir, away, Mathf.Clamp01(influence) * 0.6f);
            return adjusted.normalized;
        }

        // When we detect a collision with our own road, steer sideways to stay separated

        Vector3 ResolveSelfCollision(Vector3 direction, Vector3 center)
        {
            Vector3 candidateDir = new Vector3(direction.x, 0f, direction.z);
            if (candidateDir.sqrMagnitude < 0.0001f)
                return candidateDir;

            candidateDir = candidateDir.normalized;

            Vector3 sample = center + candidateDir * halfWidth;
            if (!TryFindSelfBlocker(sample, out var blocker))
                return candidateDir;

            Vector3 TryOffset(float angle)
            {
                Vector3 offsetDir = Quaternion.AngleAxis(angle, Vector3.up) * candidateDir;
                offsetDir = new Vector3(offsetDir.x, 0f, offsetDir.z);
                if (offsetDir.sqrMagnitude < 0.0001f)
                    return Vector3.zero;

                offsetDir = offsetDir.normalized;
                if (TryFindSelfBlocker(center + offsetDir * halfWidth, out _))
                    return Vector3.zero;

                return SteerAwayFromOrigin(offsetDir, center);
            }

            Vector3 left = TryOffset(60f);
            if (left.sqrMagnitude > 0.001f)
                return left.normalized;

            Vector3 right = TryOffset(-60f);
            if (right.sqrMagnitude > 0.001f)
                return right.normalized;

            Vector3 blockerDir = new Vector3(blocker.EndDir.x, 0f, blocker.EndDir.z);
            if (blockerDir.sqrMagnitude < 0.0001f)
            {
                blockerDir = blocker.EndPos - blocker.StartPos;
                blockerDir.y = 0f;
            }
            if (blockerDir.sqrMagnitude < 0.0001f)
            {
                blockerDir = candidateDir;
            }

            blockerDir = blockerDir.normalized;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, blockerDir);
            if (perpendicular.sqrMagnitude < 0.0001f)
            {
                perpendicular = Quaternion.AngleAxis(90f, Vector3.up) * candidateDir;
            }
            if (Vector3.Dot(perpendicular, candidateDir) < 0f)
            {
                perpendicular = -perpendicular;
            }

            return SteerAwayFromOrigin(perpendicular.normalized, center);
        }

        // Commit generated vertices and tile occupancy back onto the road piece

        void ApplyGeometry(Vector3[] vertices, List<RoadNode> nodes, Dictionary<Vector2Int, RoadPiece> tiles, Vector3 endPos, Vector3 endDir, RoadPiece sharedOwner = null)
        {
            road.VertexPos = vertices;
            road.EndPos = endPos;

            Vector3 flatEndDir = new Vector3(endDir.x, 0f, endDir.z);
            if (flatEndDir.sqrMagnitude < 0.0001f)
            {
                flatEndDir = flatStartDir;
            }
            road.EndDir = flatEndDir.normalized;

            for (int i = 0; i < nodes.Count; i++)
            {
                _roadNodes.Add(nodes[i]);
            }

            foreach (var kvp in tiles)
            {
                RegisterRoadTile(kvp.Key, kvp.Value, sharedOwner);
            }
        }

        Vector3 Hermite(Vector3 p0, Vector3 p1, Vector3 m0, Vector3 m1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0 + (-2f * t3 + 3f * t2) * p1 + (t3 - 2f * t2 + t) * m0 + (t3 - t2) * m1;
        }

        Vector3 HermiteDerivative(Vector3 p0, Vector3 p1, Vector3 m0, Vector3 m1, float t)
        {
            float t2 = t * t;
            return (6f * t2 - 6f * t) * p0 + (-6f * t2 + 6f * t) * p1 + (3f * t2 - 4f * t + 1f) * m0 + (3f * t2 - 2f * t) * m1;
        }

        // Build the flat prefab-based geometry for T and X intersections

        void BuildIntersectionSegment()
        {
            Vector3 forward = flatStartDir;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Quaternion interRot = Quaternion.LookRotation(forward, Vector3.up);

            Vector3[] vertices = new Vector3[vertexCount];
            road.Centers = new Vector3[road.Type == RConType.TIntersection ? 12 : 16];
            var nodes = new List<RoadNode>(vertexCount);
            var tiles = new Dictionary<Vector2Int, RoadPiece>();

            Vector3 centerPos = startPos + (forward * 4f * halfWidth);
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 currentPos = centerPos + interRot * baseMesh.vertices[i] * 2f;

                float a = Mathf.RoundToInt(Vector3.Dot(currentPos - centerPos, right));
                float b = Mathf.RoundToInt(Vector3.Dot(currentPos - centerPos, forward));

                bool isInner = road.Type == RConType.XIntersection
                    ? Mathf.Abs(a) <= halfWidth || Mathf.Abs(b) <= halfWidth
                    : (Mathf.Abs(a) <= halfWidth && b < halfWidth) || Mathf.Abs(b) <= halfWidth;

                currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) + (isInner ? roadHeightOffset : -roadHeightOffset);

                vertices[i] = currentPos;
                nodes.Add(new RoadNode { pos = currentPos, dir = forward, height = currentPos.y });

                Vector2Int roadKey = WorldToRoadKey(currentPos);
                tiles[roadKey] = road;
            }

            AddCenterPoints(centerPos, -right, 0);
            AddCenterPoints(centerPos, right, 4);
            AddCenterPoints(centerPos, -forward, 8);
            if (road.Type == RConType.XIntersection) AddCenterPoints(centerPos, forward, 12);

            ApplyGeometry(vertices, nodes, tiles, centerPos, forward, previousRoad);
        }
        
        // Store centerline positions used later for pathfinding updates

        void AddCenterPoints(Vector3 centerPos, Vector3 direction, int startIdx)
        {
            for (int i = 0; i < 4; i++)
            {
                road.Centers[startIdx + i] = centerPos + (direction * (i + 1) * halfWidth);
            }
        }

        // Grow a road segment either along a forced path or via a guided random walk

        void BuildRoadGeometry(bool forcePath, Vector3 forcedEndPos, Vector3 forcedEndDir)
        {
            Vector3[] vertices = new Vector3[vertexCount];
            var nodes = new List<RoadNode>(vertexCount);
            var tiles = new Dictionary<Vector2Int, RoadPiece>();

            Vector3[] centers = new Vector3[crossSectionCount];
            Vector3[] dirs = new Vector3[crossSectionCount];

            if (forcePath)
            {
                // Follow a Hermite spline toward the forced target to merge cleanly

                Vector3 targetDir = new Vector3(forcedEndDir.x, 0f, forcedEndDir.z);
                if (targetDir.sqrMagnitude < 0.0001f)
                {
                    targetDir = forcedEndPos - startPos;
                }

                if (targetDir.sqrMagnitude < 0.0001f)
                {
                    targetDir = flatStartDir;
                }

                targetDir = targetDir.normalized;

                float distance = Vector3.Distance(startPos, forcedEndPos);
                Vector3 m0 = flatStartDir * distance;
                Vector3 m1 = targetDir * distance;

                for (int s = 0; s < crossSectionCount; s++)
                {
                    float t = crossSectionCount == 1 ? 0f : (float)s / (crossSectionCount - 1);
                    Vector3 point = Hermite(startPos, forcedEndPos, m0, m1, t);
                    Vector3 derivative = HermiteDerivative(startPos, forcedEndPos, m0, m1, t);

                    centers[s] = point;

                    Vector3 flatDerivative = new Vector3(derivative.x, 0f, derivative.z);
                    if (flatDerivative.sqrMagnitude < 0.0001f)
                    {
                        flatDerivative = targetDir;
                    }
                    dirs[s] = flatDerivative.normalized;
                }
            }
            else
            {
                // Randomly bend forward while steering away from collisions and the origin

                Vector3 currentCenter = startPos;
                Vector3 currentDir = flatStartDir;

                for (int s = 0; s < crossSectionCount; s++)
                {
                    centers[s] = currentCenter;
                    dirs[s] = currentDir;

                    if (s == crossSectionCount - 1) break;

                    Vector3 candidateDir = Quaternion.AngleAxis(Random.Range(-bendVarience, bendVarience), Vector3.up) * currentDir;
                    candidateDir = new Vector3(candidateDir.x, 0f, candidateDir.z);

                    if (candidateDir.sqrMagnitude < 0.0001f)
                    {
                        candidateDir = currentDir;
                    }

                    candidateDir = candidateDir.normalized;
                    candidateDir = SteerAwayFromOrigin(candidateDir, currentCenter);
                    candidateDir = ResolveSelfCollision(candidateDir, currentCenter);

                    if (candidateDir.sqrMagnitude < 0.0001f)
                    {
                        candidateDir = SteerAwayFromOrigin(currentDir, currentCenter);
                        if (candidateDir.sqrMagnitude < 0.0001f)
                        {
                            candidateDir = flatStartDir;
                        }
                    }

                    currentDir = candidateDir.normalized;
                    currentCenter += halfWidth * currentDir;
                }
            }

            bool intersectionTriggered = false;

            road.Centers = centers;

            // Sweep across each cross-section to lay down vertices and register tile occupancy

            for (int section = 0; section < crossSectionCount; section++)
            {
                Vector3 center = centers[section];
                Vector3 dir = dirs[section];
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = flatStartDir;
                }
                dir = dir.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                for (int corner = 0; corner < 4; corner++)
                {
                    int vertexIndex = section * 4 + corner;
                    Vector3 currentPos;

                    switch (corner)
                    {
                        case 0:
                            currentPos = center - right * halfWidth * 2f;
                            currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) - roadHeightOffset;
                            break;
                        case 1:
                            currentPos = center - right * halfWidth;
                            currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) + roadHeightOffset;
                            break;
                        case 2:
                            currentPos = center + right * halfWidth;
                            currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) + roadHeightOffset;
                            break;
                        default:
                            currentPos = center + right * halfWidth * 2f;
                            currentPos.y = g.GetPerlinHeight(new Vector3(currentPos.x, 0f, currentPos.z)) - roadHeightOffset;
                            break;
                    }

                    vertices[vertexIndex] = currentPos;
                    nodes.Add(new RoadNode { pos = currentPos, dir = dir, height = currentPos.y });

                    Vector2Int roadKey = WorldToRoadKey(currentPos);
                    bool alreadyClaimed = tiles.ContainsKey(roadKey);
                    tiles[roadKey] = road;

                    bool ignoreFirstStepCollision = !forcePath && section == 0 && previousRoad != null && previousRoad.Type != RConType.Road;

                    if (forcePath || alreadyClaimed || previousRoad == null || road.HasIntersected || ignoreFirstStepCollision)
                        continue;

                    if (!_roadTileSystem.TryGetValue(roadKey, out var occupancy))
                        continue;

                    if (!TryGetBlockingRoad(occupancy, road, previousRoad, out var hitRoad))
                        continue;

                    // Convert the collision into an intersection and mark that a reroute is needed

                    road.Parent.IsActive = false;
                    road.HasIntersected = true;

                    if (hitRoad.Parent != null)
                    {
                        int index = hitRoad.Parent.Segments.IndexOf(hitRoad);
                        if (index >= 0)
                        {
                            hitRoad.Parent.Segments.RemoveAt(index);
                        }
                    }

                    if (hitRoad.Object != null)
                    {
                        GetPoolAndParent(hitRoad.Type).pool.Return(hitRoad.Object);
                    }

                    Vector3 hitForward = (hitRoad.EndPos - hitRoad.StartPos).normalized;
                    if (hitForward.sqrMagnitude < 0.0001f)
                    {
                        hitForward = Vector3.forward;
                    }

                    Vector3 hitRight = Vector3.Cross(Vector3.up, hitForward).normalized;
                    Vector3 hitCenter = Vector3.Lerp(hitRoad.StartPos, hitRoad.EndPos, 0.5f);

                    Vector3 incomingOffset = startPos - hitCenter;
                    if (incomingOffset.sqrMagnitude < 0.01f)
                    {
                        incomingOffset = dirs[section];
                    }
                    if (incomingOffset.sqrMagnitude < 0.01f && previousRoad != null)
                    {
                        incomingOffset = previousRoad.EndDir;
                    }

                    Vector3 branchDir = Vector3.Dot(incomingOffset, hitRight) >= 0f ? -hitRight : hitRight;

                    rerouteTarget = hitCenter - (branchDir * halfWidth * 4f);
                    rerouteDir = branchDir;
                    needsReroute = true;

                    // Replace the hit segment with a new intersection so both roads connect cleanly

                    var replacementInter = new RoadPiece
                    {
                        Parent = hitRoad.Parent,
                        Type = RConType.TIntersection,
                        StartPos = rerouteTarget
                    };

                    AddRoad(replacementInter, rerouteTarget, branchDir);
                    replacementInter.Parent?.Segments.Add(replacementInter);

                    intersectionTriggered = true;
                    break;
                }

                if (intersectionTriggered)
                {
                    break;
                }
            }

            if (intersectionTriggered && !forcePath)
            {
                return;
            }

            Vector3 endPos = forcePath ? forcedEndPos : centers[crossSectionCount - 1];
            Vector3 endDir = forcePath ? forcedEndDir : dirs[crossSectionCount - 1];

            ApplyGeometry(vertices, nodes, tiles, endPos, endDir);
        }

        if (road.Type != RConType.Road)
        {
            BuildIntersectionSegment();
            return;
        }

        BuildRoadGeometry(false, Vector3.zero, Vector3.zero);
        if (needsReroute)
        {
            BuildRoadGeometry(true, rerouteTarget, rerouteDir);
        }
    }

    // Decide whether this road should branch into an intersection and spawn new roads

    private bool TrySpawnIntersection(Road road)
    {
        if (road.SegsSinceIntersection < minSegmentsBeforeIntersection)
            return false;
        if (Mathf.Abs(road.NextPos.x) < 128f || Mathf.Abs(road.NextPos.z) < 128f)
            return false;
        if (Random.value > intersectionChance)
            return false;

        RConType interType = Random.value < 0.5f ? RConType.XIntersection : RConType.TIntersection;

        Vector3 leftDir = Quaternion.Euler(Vector3.up * -90f) * road.NextDir;
        Vector3 rightDir = Quaternion.Euler(Vector3.up * 90f) * road.NextDir;

        RoadPiece seg = new RoadPiece();
        seg.Parent = road;
        seg.Type = interType;
        seg.StartPos = road.NextPos;

        AddRoad(seg, road.NextPos, road.NextDir, road.Segments[road.Segments.Count - 1]);

        road.Segments.Add(seg);
        road.IsActive = false;

        _roads.Add(CreateBranch(road.NextPos + (road.NextDir * halfWidth * 4) + (leftDir * halfWidth * 4), leftDir, seg));
        _roads.Add(CreateBranch(road.NextPos + (road.NextDir * halfWidth * 4) + (rightDir * halfWidth * 4), rightDir, seg));
        if (seg.Type == RConType.XIntersection) _roads.Add(CreateBranch(road.NextPos + road.NextDir * halfWidth * 8, road.NextDir, seg));

        Road CreateBranch(Vector3 start, Vector3 dir, RoadPiece intersection)
        {
            Road newRoad = new Road(start, dir);
            RoadPiece seg = new RoadPiece();

            seg.Parent = newRoad;
            seg.Type = RConType.Road;
            seg.StartPos = start;
            newRoad.Segments.Add(seg);

            AddRoad(seg, start, dir, intersection);

            newRoad.NextPos = seg.EndPos;
            newRoad.NextDir = seg.EndDir;
            newRoad.SegsSinceIntersection = 0;

            return newRoad;
        }

        return true;
    }

    // Lazily instantiate or recycle a mesh for the generated road segment
    
    private void PlaceRoads(RoadPiece seg)
    {
        if (seg.Object != null) return;

        var (pool, parent, data, limit) = GetPoolAndParent(seg.Type);
        if (pool.CreatedCount < limit) pool.Prewarm(2, data.SegmentPrefab, parent);

        GameObject obj = pool.Get(data.SegmentPrefab, parent);
        obj.transform.localPosition = Vector3.zero;

        if (_roadObjMap.TryGetValue(obj, out var previousSeg) && previousSeg != null)
        {
            previousSeg.Object = null;
            _roadObjMap.Remove(obj);
        }

        for (int i = 1; i < seg.VertexPos.Length; i++)
        {
            Vector3 localVert = PosUtil.GetLocalPos(seg.VertexPos[i]);

            // Clear any foliage or obstacles that now fall beneath the road footprint

            var overlaps = Physics.OverlapSphere(localVert, halfWidth * Random.Range(0.5f, 1.5f), roadDetectMask);
            for (int j = 0; j < overlaps.Length; j++)
            {
                switch (overlaps[j].gameObject.layer)
                {
                    case 6:
                        Vector3 tPos = PosUtil.GetWorldPos(overlaps[j].transform.position);
                        Vector2Int tilePos = new Vector2Int(
                            Mathf.FloorToInt(tPos.x / g.CellSize),
                            Mathf.FloorToInt(tPos.z / g.CellSize));

                        if (!g.TileDict.TryGetValue(tilePos, out var tile)) break;

                        foreach (var fol in tile.Foliage)
                        {
                            if (fol.Object != null) f.GetPoolAndParent(fol.Type).pool.Return(fol.Object);
                        }

                        tile.Foliage.Clear();

                        foreach (var c in tile.Cells)
                        {
                            c.Obstacles.Clear();
                        }

                        break;
                }
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

        // Flag the affected pathfinding cells so AI treats them as road

        for(int j = 0; j < seg.Centers.Length; j++)
        {
            PfGraph.root.CellDict[PosUtil.V3RoundToInt(seg.Centers[j] / 16) * 16].IsRoad = true;
        }

        seg.Object = obj;
        _roadObjMap[obj] = seg;
    }

    private void PruneRoadNodes()
    {
        // Remove cached road nodes that fall outside the retention distance

        float keepDist = (g.ViewDistance + g.CullMargin) * g.CellSize + 64f;
        Vector3 p = PosUtil.GetWorldPos(_playerPos);
        for (int i = _roadNodes.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(p, _roadNodes[i].pos) > keepDist)
                _roadNodes.RemoveAt(i);
        }
    }

    private void RegisterRoadTile(Vector2Int key, RoadPiece owner, RoadPiece sharedOwner)
    {
        if (!_roadTileSystem.TryGetValue(key, out var occupancy))
        {
            occupancy = default;
        }

        occupancy.Add(owner);
        occupancy.Add(sharedOwner);

        _roadTileSystem[key] = occupancy;
    }

    private bool TryGetRoadTile(Vector3 position, out RoadTileOccupancy occupancy)
    {
        return _roadTileSystem.TryGetValue(WorldToRoadKey(position), out occupancy);
    }

    private (ObjectPool pool, Transform parent, RConData data, int limit) GetPoolAndParent(RConType type)
    {
        // Return the pool, prefab data, and reuse limit for a given road piece type

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

    private static Vector2Int WorldToRoadKey(Vector3 position)
    {
        return new Vector2Int(
            Mathf.RoundToInt(position.x / TileKeySize),
            Mathf.RoundToInt(position.z / TileKeySize));
    }

    private static bool TryGetBlockingRoad(RoadTileOccupancy occupancy, RoadPiece candidate, RoadPiece previous, out RoadPiece result)
    {
        if (occupancy.Primary != null && occupancy.Primary != candidate && occupancy.Primary != previous)
        {
            result = occupancy.Primary;
            return true;
        }

        if (occupancy.Secondary != null && occupancy.Secondary != candidate && occupancy.Secondary != previous)
        {
            result = occupancy.Secondary;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryGetRoadWithParent(RoadTileOccupancy occupancy, Road owner, RoadPiece candidate, RoadPiece previous, out RoadPiece result)
    {
        if (occupancy.Primary != null && occupancy.Primary != candidate && occupancy.Primary != previous && occupancy.Primary.Parent == owner)
        {
            result = occupancy.Primary;
            return true;
        }

        if (occupancy.Secondary != null && occupancy.Secondary != candidate && occupancy.Secondary != previous && occupancy.Secondary.Parent == owner)
        {
            result = occupancy.Secondary;
            return true;
        }

        result = null;
        return false;
    }
}
