using System.Collections.Generic;
using UnityEngine;

public class PfCell
{
    public bool IsRoad = false;
    public bool Blocked = false;
    public int FCost = int.MaxValue;
    public int GCost = int.MaxValue;
    public int HCost = int.MaxValue;
    public Vector2Int Position;
    public Vector2Int Connection;
    public List<Vector2Int> Obstacles = new();

    public PfCell(Vector2Int inPos)
    {
        Position = inPos;
    }

    public bool CheckPath(Vector2Int startPos, Vector2 startDir)
    {
        foreach (var o in Obstacles)
        {
            float t = Mathf.Clamp(Vector2.Dot(o - startPos, startDir) / startDir.sqrMagnitude, 0f, 1f);
            Vector2 closestPoint = startPos + startDir * t;

            if (Vector2.Distance(o, closestPoint) < 4f)
            {
                return false;
            }
        }
        return true;
    }
}
public class PfGraph : Singleton<PfGraph>
{
    public int CellSize;
    public Dictionary<Vector2Int, PfCell> CellDict = new();
    private Queue<PfCell> _cellsToReset = new();
    public List<Vector2Int> FindPath(Vector3 sPos, Vector3 ePos, bool followRoads)
    {
        var _sPos = PosUtil.V3FloorToInt(PosUtil.GetWorldPos(sPos) / CellSize) * CellSize;
        var _ePos = PosUtil.V3FloorToInt(PosUtil.GetWorldPos(ePos) / CellSize) * CellSize;

        if (!CellDict.ContainsKey(_sPos) || !CellDict.ContainsKey(_ePos))
        {
            return null;
        }

        if (!followRoads)
        {
            return RunSearch(_sPos, _ePos, false, out _);
        }

        if (!TryGetNearestRoadCell(_sPos, out var startRoadPos))
        {
            return null;
        }

        List<Vector2Int> approachPath = null;
        Vector2Int approachReached;

        if (startRoadPos != _sPos)
        {
            approachPath = RunSearch(_sPos, startRoadPos, false, out approachReached);

            if (approachReached != startRoadPos)
            {
                return approachPath;
            }
        }

        var roadPath = RunSearch(startRoadPos, _ePos, true, out _);

        if (approachPath != null && approachPath.Count > 0)
        {
            for (int i = 1; i < approachPath.Count; i++)
            {
                roadPath.Add(approachPath[i]);
            }
        }

        return roadPath;
    }
    private List<Vector2Int> RunSearch(Vector2Int startPos, Vector2Int targetPos, bool requireRoads, out Vector2Int bestReached)
    {
        ResetTileCosts();

        if (!CellDict.TryGetValue(startPos, out var startCell))
        {
            bestReached = startPos;
            return new List<Vector2Int>();
        }

        HashSet<Vector2Int> searchedCells = new();
        List<Vector2Int> cellsToSearch = new() { startPos };

        startCell.GCost = 0;
        startCell.HCost = GetTileDistance(startPos, targetPos);
        startCell.FCost = startCell.HCost;

        bestReached = startPos;
        int bestH = startCell.HCost;
        int bestG = startCell.GCost;

        for (int i = 0; i < 256; i++)
        {
            if (cellsToSearch.Count == 0) break;

            Vector2Int posToSearch = cellsToSearch[0];
            var cellToSearch = CellDict[posToSearch];

            foreach (var pos in cellsToSearch)
            {
                var candidate = CellDict[pos];

                if (candidate.FCost < cellToSearch.FCost || (candidate.FCost == cellToSearch.FCost && candidate.HCost < cellToSearch.HCost))
                {
                    posToSearch = pos;
                    cellToSearch = candidate;
                }
            }

            cellsToSearch.Remove(posToSearch);
            searchedCells.Add(posToSearch);

            if (cellToSearch.HCost < bestH || (cellToSearch.HCost == bestH && cellToSearch.GCost < bestG))
            {
                bestH = cellToSearch.HCost;
                bestReached = posToSearch;
                bestG = cellToSearch.GCost;
            }

            if (posToSearch == targetPos)
            {
                bestReached = posToSearch;
                return BuildPath(posToSearch, startPos);
            }

            SearchCellNeighbors(posToSearch, targetPos, searchedCells, requireRoads, ref cellsToSearch);
        }

        return BuildPath(bestReached, startPos);
    }
    private void SearchCellNeighbors(Vector2Int cellPos, Vector2Int ePos, HashSet<Vector2Int> _searchedCells, bool requireRoads, ref List<Vector2Int> _cellsToSearch)
    {
        int step = Mathf.Max(1, CellSize);

        for (int x = cellPos.x - step; x <= cellPos.x + step; x += step)
            for (int z = cellPos.y - step; z <= cellPos.y + step; z += step)
            {
                Vector2Int neighborPos = new Vector2Int(x, z);

                if (neighborPos == cellPos) continue;

                if (CellDict.TryGetValue(neighborPos, out var neighborCell) && !_searchedCells.Contains(neighborPos))
                {
                    if (neighborCell.Blocked) continue;
                    if (requireRoads && !neighborCell.IsRoad) continue;

                    if (neighborCell.CheckPath(cellPos, neighborPos - cellPos))
                    {
                        int GCosttoNeighbor = CellDict[cellPos].GCost + GetTileDistance(cellPos, neighborPos);

                        if (GCosttoNeighbor < neighborCell.GCost)
                        {
                            neighborCell.Connection = cellPos;

                            neighborCell.GCost = GCosttoNeighbor;
                            neighborCell.HCost = GetTileDistance(neighborPos, ePos);
                            neighborCell.FCost = neighborCell.GCost + neighborCell.HCost;
                            _cellsToReset.Enqueue(neighborCell);

                            if (!_cellsToSearch.Contains(neighborPos)) _cellsToSearch.Add(neighborPos);
                        }
                    }
                }
            }
    }
    private int GetTileDistance(Vector2Int pos1, Vector2Int pos2)
    {
        Vector2Int d = new Vector2Int(Mathf.Abs(pos1.x - pos2.x), Mathf.Abs(pos1.y - pos2.y));
        int lowest = Mathf.Min(d.x, d.y);
        return lowest * 14 + (Mathf.Max(d.x, d.y) - lowest) * 10;
    }
    public bool TryGetNearestRoadCell(Vector2Int origin, out Vector2Int roadPos)
    {
        if (CellDict.TryGetValue(origin, out var originCell) && originCell.IsRoad && !originCell.Blocked)
        {
            roadPos = origin;
            return true;
        }

        Queue<Vector2Int> toCheck = new();
        HashSet<Vector2Int> visited = new();

        toCheck.Enqueue(origin);
        visited.Add(origin);

        int step = Mathf.Max(1, CellSize);

        for (int i = 0; i < 512; i++)
        {
            if (toCheck.Count == 0) break;
            var current = toCheck.Dequeue();

            for (int x = current.x - step; x <= current.x + step; x += step)
                for (int z = current.y - step; z <= current.y + step; z += step)
                {
                    Vector2Int neighborPos = new Vector2Int(x, z);
                    if (neighborPos == current || visited.Contains(neighborPos)) continue;
                    visited.Add(neighborPos);

                    if (!CellDict.TryGetValue(neighborPos, out var neighborCell) || neighborCell.Blocked) continue;

                    if (neighborCell.IsRoad)
                    {
                        roadPos = neighborPos;
                        return true;
                    }

                    toCheck.Enqueue(neighborPos);
                }
        }

        roadPos = default;
        return false;
    }
    private List<Vector2Int> BuildPath(Vector2Int reachedPos, Vector2Int startPos)
    {
        List<Vector2Int> finalPath = new();
        if (!CellDict.TryGetValue(reachedPos, out var pathCell)) return finalPath;

        while (pathCell.Position != startPos)
        {
            finalPath.Add(pathCell.Position);
            _cellsToReset.Enqueue(pathCell);

            if (!CellDict.TryGetValue(pathCell.Connection, out pathCell))
            {
                break;
            }
        }

        if (CellDict.TryGetValue(startPos, out var startCell))
        {
            _cellsToReset.Enqueue(startCell);
            finalPath.Add(startPos);
        }

        return finalPath;
    }
    private void ResetTileCosts()
    {
        while (_cellsToReset.Count > 0)
        {
            var cell = _cellsToReset.Dequeue();

            cell.FCost = int.MaxValue;
            cell.GCost = int.MaxValue;
            cell.HCost = int.MaxValue;
        }
    }

    void OnDrawGizmosSelected()
    {
        foreach (var cell in CellDict.Values)
        {
            if (cell.IsRoad)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(PosUtil.GetLocalPos(new Vector3(cell.Position.x, 0, cell.Position.y)), CellSize * Vector3.one);
            }
        }
    }
}
