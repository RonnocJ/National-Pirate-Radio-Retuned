using UnityEngine;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;
using Unity.VisualScripting;
using UnityEditor;
[Serializable]
public class VehicleAutopilot
{
    [SerializeField] private bool followRoads;   
    [SerializeField] private float arrivalDistance;
    [SerializeField] private float steerFactor;
    [SerializeField] private AnimationCurve steerToDriveCurve;
    [SerializeField] private Transform tr;
    private int _pathIndex;
    private float _timer;
    private List<Vector3> _pathList = new();
    public void RebuildDrivePath(Vector3? endPos = null)
    {
        Vector2Int fallback;

        if (followRoads)
        {
            if (!PfGraph.root.TryGetNearestRoadCell(PosUtil.V3FloorToInt(PosUtil.GetWorldPos(tr.position + (tr.forward * 128f) + (tr.right * (Random.value < 0.5f ? -32 : 32))) / PfGraph.root.CellSize) * PfGraph.root.CellSize, out fallback) && endPos == null)
            {
                _pathList = new List<Vector3>();

                return;
            }
        }
        else
        {
            fallback = PosUtil.V3FloorToInt(PosUtil.GetWorldPos(tr.position + (tr.forward * 128f)) / PfGraph.root.CellSize) * PfGraph.root.CellSize;
        }   

        List<Vector2Int> path = PfGraph.root.FindPath(tr.position + (followRoads ? (tr.forward * 32f) : Vector3.zero), endPos ?? PosUtil.GetLocalPos(new Vector3(fallback.x, 0, fallback.y)), followRoads);
        List<Vector3> worldPath = new();

        if (path == null)
        {
            _pathList = new List<Vector3>();
            return;
        }

        for (int i = 0; i < path.Count; i++)
        {
            worldPath.Add(PosUtil.GetLocalPos(new Vector3(path[i].x + (PfGraph.root.CellSize * 0.5f), 0, path[i].y + (PfGraph.root.CellSize * 0.5f))));
        }

        var smoothedList = new List<Vector3>(worldPath);

        for (int j = 0; j < 6; j++)
        {
            var prevList = new List<Vector3>(smoothedList);

            for (int k = 1; k < prevList.Count - 1; k++)
            {
                Vector3 neighborAvg = 0.5f * (prevList[k - 1] + prevList[k + 1]);
                Vector3 smoothed = Vector3.Lerp(prevList[k], neighborAvg, Mathf.Clamp01(0.7f));
                smoothedList[k] = Vector3.Lerp(smoothed, worldPath[k], Mathf.Clamp01(0.125f));
            }
        }

        _pathList = smoothedList;
        _pathIndex = 0;
    }
    public (Vector2 driveInput, float brakeInput) DriveToTarget(float repathInterval, Vector3? endPos = null)
    {
        _timer += Time.fixedDeltaTime;

        if (_timer > repathInterval || _pathIndex >= _pathList.Count - 1)
        {
            RebuildDrivePath(endPos);
            _timer = 0f;
        }

        if (_pathList.Count < 2) return (Vector2.zero, 0);

        Vector3 waypoint = _pathList[_pathIndex];
        waypoint.y = tr.position.y;

        Vector3 nextOnPlane = Vector3.ProjectOnPlane(waypoint - tr.position, tr.up);

        if (nextOnPlane.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            _pathIndex++;

            waypoint = _pathList[_pathIndex];
            waypoint.y = tr.position.y;

            nextOnPlane = Vector3.ProjectOnPlane(waypoint - tr.position, tr.up);
        }

        float steerInput = Mathf.Clamp(Vector3.SignedAngle(tr.forward, nextOnPlane, Vector3.up) / steerFactor, -1f, 1f);
        float driveInput = steerToDriveCurve.Evaluate(Mathf.Abs(steerInput));
        float brakeInput = 1 - Mathf.Clamp01(Mathf.Sqrt(nextOnPlane.sqrMagnitude - (arrivalDistance * arrivalDistance)) / 64f);

        for (int i = 1; i < _pathList.Count; i++)
        {
            Debug.DrawLine(_pathList[i - 1], _pathList[i], Color.orange);
        }

        return (new Vector2(steerInput, driveInput), brakeInput);
    }
}
