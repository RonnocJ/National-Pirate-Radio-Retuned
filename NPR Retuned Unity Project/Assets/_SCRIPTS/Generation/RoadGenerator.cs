using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;
using System.Linq;

public class Road
{
    public Vector3[] VertexPos;
    public Vector3 StartPos;
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

    [Header("Road Width / Terrain Blend")]
    [SerializeField] private float halfWidth = 16f;
    [SerializeField][Min(0f)] private float shoulder = 6f;
    [SerializeField][Min(0f)] private float terrainClearance = 0.03f;

    private ObjectPool _roadPool;

    private readonly List<RoadNode> _roadNodes = new();
    private readonly List<Road> _segments = new();
    public float HalfWidth => halfWidth;
    public float Shoulder => shoulder;
    public float TerrainClearance => terrainClearance;

    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;

    IEnumerator Start()
    {
        _roadPool = new ObjectPool();
        _roadPool.Prewarm(roadSteps, g.RCons[0].data.SegmentPrefab, roadParent);

        Vector3 p = _playerPos;
        Vector3 startPos = new Vector3(p.x, 0f, p.z) + Vector3.forward * 96f;
        Vector3 startDir = Vector3.forward;

        for (int i = 0; i < roadSteps; i++)
        {
            Road r = new Road();
            r.Object = _roadPool.Get(g.RCons[0].data.SegmentPrefab, roadParent);
            Mesh m = r.Object.GetComponent<MeshFilter>().mesh;
            r.VertexPos = m.vertices;
            CreateRoad(r, startPos, startDir);
            r.StartPos = startPos;
            _segments.Add(r);
            startPos = r.EndPos;
            startDir = r.EndDir;
        }

        yield return null;

        while (true)
        {
            p = _playerPos;

            Vector3 endPos;
            Vector3 endDir;
            if (_segments.Count == 0)
            {
                endPos = new Vector3(p.x, 0f, p.z) + Vector3.forward * 96f;
                endDir = Vector3.forward;
            }
            else
            {
                endPos = _segments[_segments.Count - 1].EndPos;
                endDir = _segments[_segments.Count - 1].EndDir;
            }

            float distToEnd = Vector3.Distance(p, endPos);
            if (distToEnd <= g.ViewDistance * g.CellSize * 0.5f)
            {
                int batch = Mathf.Max(1, roadSteps / 4);
                for (int i = 0; i < batch; i++)
                {
                    Road r = new Road();
                    r.Object = _roadPool.Get(g.RCons[0].data.SegmentPrefab, roadParent);
                    Mesh m = r.Object.GetComponent<MeshFilter>().mesh;
                    r.VertexPos = m.vertices;
                    CreateRoad(r, endPos, endDir);
                    r.StartPos = endPos;
                    _segments.Add(r);
                    endPos = r.EndPos;
                    endDir = r.EndDir;
                }
            }

            CullFarSegments(p);
            PruneRoadNodes();

            yield return null;
        }
    }
    public void CreateRoad(Road road, Vector3 startPos, Vector3 startDir)
    {
        Vector3 currentPos = startPos;
        Vector3 currentDir = startDir;

        for (int i = 0; i < 35; i += 4)
        {
            Vector3 right = Vector3.Cross(Vector3.up, currentDir).normalized;

            float centerH = g.GetPerlinHeight(currentPos);
            float y = centerH + g.RCons[0].data.HeightOffset;

            Vector3 farLeft = currentPos - (right * halfWidth * 2);
            Vector3 farRight = currentPos + (right * halfWidth * 2);

            road.VertexPos[i] = farLeft + Vector3.up * (g.GetPerlinHeight(farLeft) - 0.5f);
            road.VertexPos[i + 1] = currentPos - (right * halfWidth) + Vector3.up * (y + 1f);
            road.VertexPos[i + 2] = currentPos + (right * halfWidth) + Vector3.up * (y + 1f);
            road.VertexPos[i + 3] = farRight + Vector3.up * (g.GetPerlinHeight(farRight) - 0.5f);

            _roadNodes.Add(new RoadNode
            {
                pos = currentPos,
                dir = currentDir.normalized,
                height = centerH
            });

            currentDir = Quaternion.AngleAxis(Random.Range(-bendVarience, bendVarience), Vector3.up) * currentDir;
            currentPos += 8f * currentDir.normalized;

            if (i == 28)
            {
                road.EndPos = currentPos;
                road.EndDir = currentDir;
            }
        }

        Mesh m = road.Object.GetComponent<MeshFilter>().mesh;
        m.vertices = road.VertexPos;
        m.RecalculateBounds();
        m.RecalculateNormals();
        road.Object.GetComponent<MeshCollider>().sharedMesh = m;
    }

    private void CullFarSegments(Vector3 playerPos)
    {
        float activeRange = (g.ViewDistance + g.CullMargin) * g.CellSize + 64f;
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            Road r = _segments[i];
            float d = DistancePointToSegmentXZ(playerPos, r.StartPos, r.EndPos);
            if (d > activeRange)
            {
                _roadPool.Return(r.Object);
                _segments.RemoveAt(i);
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
}
