using System;
using System.Collections.Generic;
using UnityEngine;
public class PfTile
{
    public Vector2Int Position;
    public bool Navigable = false;
    public int FCost = int.MaxValue;
    public int GCost = int.MaxValue;
    public int HCost = int.MaxValue;
    public Vector2Int Connection;
    public PfTile(Vector2Int newPos)
    {
        Position = newPos;
    }
}
public class PfGraph : Singleton<PfGraph>
{
    public Dictionary<Vector2Int, PfTile> PfDict = new();
    private GeneratorSettings g => GeneratorSettings.root;

    public Vector2Int V3ToInt(Vector3 inPos)
    {
        inPos.x = Mathf.Round(inPos.x / GeneratorSettings.root.CellSize) * GeneratorSettings.root.CellSize;
        inPos.z = Mathf.Round(inPos.z / GeneratorSettings.root.CellSize) * GeneratorSettings.root.CellSize;
        return new Vector2Int(Mathf.RoundToInt(inPos.x), Mathf.RoundToInt(inPos.z));
    }
    public int GetTileDistance(Vector2Int pos1, Vector2Int pos2)
    {
        Vector2Int d = new Vector2Int(Mathf.Abs(pos1.x - pos2.x), Mathf.Abs(pos1.y - pos2.y));
        int lowest = Mathf.Min(d.x, d.y);
        return lowest * 14 + (Mathf.Max(d.x, d.y) - lowest) * 10;
    }
    public bool IsNavigable(Vector3 worldPos)
    {
        var key = V3ToInt(worldPos);
        return PfDict.TryGetValue(key, out var tile) && tile.Navigable;
    }

    public List<Vector2Int> FindPath(Vector3 startPos, Vector3 endPos)
    {
        Vector2Int sP = V3ToInt(startPos);
        Vector2Int eP = V3ToInt(endPos);

        // Validate start/end exist and are navigable
        if (!PfDict.TryGetValue(sP, out PfTile startTile) || !PfDict.TryGetValue(eP, out PfTile endTile))
            return null;
        if (!startTile.Navigable || !endTile.Navigable)
            return null;

        // Reset all tiles' pathfinding state to avoid stale data/cycles
        foreach (var kv in PfDict)
        {
            kv.Value.FCost = int.MaxValue;
            kv.Value.GCost = int.MaxValue;
            kv.Value.HCost = int.MaxValue;
            kv.Value.Connection = default;
        }

        List<Vector2Int> cellsToSearch = new List<Vector2Int> { sP };
        List<Vector2Int> searchedCells = new();
        List<Vector2Int> finalPath = new();

        PfTile tile = startTile;
        tile.GCost = 0;
        tile.HCost = GetTileDistance(sP, eP);
        tile.FCost = tile.HCost;

        while (cellsToSearch.Count > 0)
        {
            var searchCell = cellsToSearch[0];

            foreach (Vector2Int pos in cellsToSearch)
            {
                PfTile t = PfDict[pos];

                if (t.FCost < PfDict[searchCell].FCost || (t.FCost == PfDict[searchCell].FCost && t.HCost < PfDict[searchCell].HCost))
                {
                    searchCell = pos;
                }
            }

            cellsToSearch.Remove(searchCell);
            searchedCells.Add(searchCell);

            if (searchCell == eP)
            {
                PfTile pathCell = endTile;
                // Guard against cycles or broken connections during backtrack
                HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
                int guard = Mathf.Max(1, PfDict.Count + 4);

                while (pathCell.Position != sP)
                {
                    if (!visited.Add(pathCell.Position))
                    {
                        // Cycle detected
                        Debug.LogWarning("PfGraph.FindPath: cycle detected while reconstructing path.");
                        return null;
                    }
                    finalPath.Add(pathCell.Position);
                    if (!PfDict.TryGetValue(pathCell.Connection, out PfTile nextCell))
                    {
                        // Broken connection chain
                        Debug.LogWarning("PfGraph.FindPath: broken connection while reconstructing path.");
                        return null;
                    }
                    pathCell = nextCell;

                    if (--guard <= 0)
                    {
                        Debug.LogWarning("PfGraph.FindPath: guard limit reached during path reconstruction.");
                        return null;
                    }
                }

                finalPath.Add(sP);

                return finalPath;
            }


            SearchCellNeighbors(searchCell, eP, searchedCells, ref cellsToSearch);
        }

        Debug.LogWarning("No path found");
        return null;
    }
    private void SearchCellNeighbors(Vector2Int cellPos, Vector2Int endPos, List<Vector2Int> searchedCells, ref List<Vector2Int> cellsToSearch)
    {
        for (int x = cellPos.x - g.CellSize; x <= g.CellSize + cellPos.x; x += g.CellSize)
        {
            for (int y = cellPos.y - g.CellSize; y <= g.CellSize + cellPos.y; y += g.CellSize)
            {
                Vector2Int nPos = new Vector2Int(x, y);

                if (PfDict.TryGetValue(nPos, out PfTile t) && !searchedCells.Contains(nPos) && t.Navigable)
                {
                    int gCostN = PfDict[cellPos].GCost + GetTileDistance(cellPos, nPos);

                    if (gCostN < t.GCost)
                    {
                        t.Connection = cellPos;
                        t.GCost = gCostN;
                        t.HCost = GetTileDistance(nPos, endPos);
                        t.FCost = t.GCost + t.HCost;
                    }

                    if (!cellsToSearch.Contains(nPos))
                    {
                        cellsToSearch.Add(nPos);
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        foreach (var kvp in PfDict)
        {
            if (kvp.Value.Navigable)
            {
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.red;
            }
            Gizmos.DrawSphere(new Vector3(kvp.Key.x, 0, kvp.Key.y), 1);
        }
    }
}
