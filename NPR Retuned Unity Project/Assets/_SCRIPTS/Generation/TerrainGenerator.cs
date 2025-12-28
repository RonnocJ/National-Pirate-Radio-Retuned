using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private Transform grassParent;
    [SerializeField] private GrassGenerator grassGen;
    private ObjectPool _grassPool;
    private Dictionary<TConType, TConData> _tileConDict = new();
    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;
    void Start()
    {
        // Add all tile constructs to the dictionary

        foreach (var con in g.TCons)
        {
            _tileConDict[con.constructName] = con.data;
        }

        // Place garage tile at 0,0

        AddTile(TConType.Garage, Vector2Int.zero);

        // Instantiate object pools for each tile

        int poolCount = (2 * (g.ViewDistance + g.CullMargin) + 1) * (2 * (g.ViewDistance + g.CullMargin) + 1);
        _grassPool = new ObjectPool();
    }
    public IEnumerator GenerateTerrain()
    {
        // Rounds player position to 1 / 32nd scale

        Vector3 p = _playerPos;
        int pX = Mathf.FloorToInt(p.x / g.CellSize);
        int pZ = Mathf.FloorToInt(p.z / g.CellSize);

        // Iterates through all positons in a square around the player

        for (int x = pX - g.PlaceDistance - g.CullMargin; x <= pX + g.PlaceDistance + g.CullMargin; x++)
        {
            for (int z = pZ - g.PlaceDistance - g.CullMargin; z <= pZ + g.PlaceDistance + g.CullMargin; z++)
            {
                // Tries to add tile data at position

                Vector2Int pos = PosUtil.GetWorldPos(new Vector2Int(x, z));

                Tile tile = AddTile(TConType.Grass, pos);

                // Won't place a tile if beyond the view distance or there is an object already present

                if (Mathf.Abs(x - pX) > g.ViewDistance || Mathf.Abs(z - pZ) > g.ViewDistance) continue;

                if (tile.Object == null && tile.Type == TConType.Grass)
                {
                    // Adds 2 tiles to the pool if pool isn't at capacity

                    if (_grassPool.CreatedCount < g.GrassLimit)
                    {
                        _grassPool.Prewarm(2, _tileConDict[TConType.Grass].Prefab, grassParent);
                    }

                    // Places and deforms grass tile

                    PlaceTile(tile, _grassPool, pos);
                }
            }

            yield return null;

        }

        // Sorts all objects in pool by distance to player (improves enqueuing / dequeuing behavior)

        _grassPool?.SortActiveByDistance(p);
    }

    Tile AddTile(TConType type, Vector2Int checkPos)
    {
        // Looks for existing tile at position, if none are present creates a new one

        if (!g.TileDict.TryGetValue(checkPos, out Tile tile))
        {
            tile = new Tile(type);
            g.TileDict[checkPos] = tile;

            tile.Position = checkPos;

            GeneratePfCells(tile, checkPos);
        }

        return tile;
    }

    void PlaceTile(Tile tile, ObjectPool poolType, Vector2Int newPos)
    {
        if (tile == null) return;

        GameObject newObj = poolType.Get(_tileConDict[TConType.Grass].Prefab, grassParent);

        ResetMesh(newObj);
        MoveObject(newObj, newPos);
    }

    public void ResetMesh(GameObject movingObj)
    {
        if (movingObj == null) return;

        // Finds old tile entry from object's original position

        Vector3 worldPos = PosUtil.GetWorldPos(movingObj.transform.position);
        Vector2Int tileKey = new Vector2Int(
            Mathf.RoundToInt(worldPos.x / g.CellSize),
            Mathf.RoundToInt(worldPos.z / g.CellSize));

        Tile oldTile;
        if (!g.TileDict.TryGetValue(tileKey, out oldTile))
        {
            foreach (var kvp in g.TileDict)
            {
                if (kvp.Value.Object == movingObj)
                {
                    oldTile = kvp.Value;
                    break;
                }
            }
        }

        if (oldTile == null || oldTile.Object != movingObj) return;

        // Sets object to null and removes entries from the pathfinding cell grid

        oldTile.Object = null;

        foreach (var cell in oldTile.Cells)
            {
                PfGraph.root.CellDict.Remove(cell.Position);
            }

        oldTile.Cells.Clear();

        // Sets mesh back to its original state as stored in the corresponding tile construct

        MeshFilter oldFilter = movingObj.GetComponent<MeshFilter>();
        if (oldFilter == null) return;

        Mesh mesh = oldFilter.mesh;
        mesh.Clear();
        mesh.vertices = _tileConDict[oldTile.Type].DefaultMeshes.vertices;
        mesh.triangles = _tileConDict[oldTile.Type].DefaultMeshes.triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public void MoveObject(GameObject newObj, Vector2Int newPos)
    {
        // Sets new position for the tile
        
        if (g.TileDict.TryGetValue(newPos, out Tile newTile))
        {
            newTile.Object = newObj;

            Vector3 globalPos = new Vector3(newPos.x * g.CellSize, 0f, newPos.y * g.CellSize);
            newTile.Object.transform.position = PosUtil.GetLocalPos(globalPos);

            MeshFilter meshFilter = newTile.Object.GetComponent<MeshFilter>();

            Mesh mesh = meshFilter.mesh;
            newTile.Meshes = mesh;

            GenerateHeight(newTile);
        }
    }
    private void GeneratePfCells(Tile tile, Vector2Int inPos)
    {
        if (tile.Cells.Count > 0)
        {
            // Clears stale references before repopulating.

            tile.Cells.Clear();
        }

        // Iterates through pathfinding cell positions as they fit within a tile (16x16 cells against 64x64 tiles)

        for (int x = 0; x < g.CellSize / PfGraph.root.CellSize; x++)
        {
            float sampleX = inPos.x * g.CellSize - g.CellSize * 0.5f + (x + 0.5f) * PfGraph.root.CellSize;
            for (int z = 0; z < g.CellSize / PfGraph.root.CellSize; z++)
            {
                float sampleZ = inPos.y * g.CellSize - g.CellSize * 0.5f + (z + 0.5f) * PfGraph.root.CellSize;
                Vector2Int cellPos = PosUtil.V3FloorToInt(new Vector3(sampleX, 0, sampleZ) / PfGraph.root.CellSize) * PfGraph.root.CellSize;

                if (!PfGraph.root.CellDict.TryGetValue(cellPos, out PfCell cell))
                {
                    cell = new PfCell(cellPos);
                    PfGraph.root.CellDict[cellPos] = cell;
                }

                tile.Cells.Add(cell);
            }
        }
    }
    public void GenerateHeight(Tile tile)
    {
        // Stuff that deforms mesh height and stores data so that the grass generator can place grass correctly

        float yaw = tile.Object.transform.eulerAngles.y;
        float cosYaw = Mathf.Cos(yaw * Mathf.Deg2Rad);
        float sinYaw = Mathf.Sin(yaw * Mathf.Deg2Rad);
        Vector3 tileLocalPos = tile.Object.transform.position;
        Vector3 tileWorldPos = PosUtil.GetWorldPos(tileLocalPos);

        Vector3[] originalVerts = _tileConDict[tile.Type].DefaultMeshes.vertices;
        Vector3[] newVerts = new Vector3[originalVerts.Length];

        NativeArray<float3> samplePositions = new NativeArray<float3>(originalVerts.Length, Allocator.TempJob);
        NativeArray<float> heightResults = new NativeArray<float>(originalVerts.Length, Allocator.TempJob);

        int resolution = Mathf.RoundToInt(Mathf.Sqrt(newVerts.Length));
        if (tile.TSurface.Heights == null || tile.TSurface.Heights.GetLength(0) != resolution)
        {
            tile.TSurface.Heights = new float[resolution, resolution];
        }
        tile.TSurface.Resolution = resolution;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        try
        {
            for (int i = 0; i < originalVerts.Length; i++)
            {
                Vector3 ov = originalVerts[i];
                float worldX = tileWorldPos.x + (ov.x * cosYaw) - (ov.z * sinYaw);
                float worldZ = tileWorldPos.z + (ov.x * sinYaw) + (ov.z * cosYaw);

                samplePositions[i] = new float3(worldX, ov.y, worldZ);

                if (ov.x < minX) minX = ov.x;
                if (ov.x > maxX) maxX = ov.x;
                if (ov.z < minZ) minZ = ov.z;
                if (ov.z > maxZ) maxZ = ov.z;
            }

            if (maxX - minX < 0.0001f)
            {
                float half = g.CellSize * 0.5f;
                minX = -half;
                maxX = half;
            }

            if (maxZ - minZ < 0.0001f)
            {
                float half = g.CellSize * 0.5f;
                minZ = -half;
                maxZ = half;
            }

            float width = maxX - minX;
            float depth = maxZ - minZ;
            float stepX = width / Mathf.Max(1, resolution - 1);
            float stepZ = depth / Mathf.Max(1, resolution - 1);

            tile.TSurface.MinX = minX;
            tile.TSurface.MaxX = maxX;
            tile.TSurface.MinZ = minZ;
            tile.TSurface.MaxZ = maxZ;
            tile.TSurface.StepX = stepX;
            tile.TSurface.StepZ = stepZ;
            tile.TSurface.HalfSize = Mathf.Max(Mathf.Abs(maxX), Mathf.Abs(minX), Mathf.Abs(maxZ), Mathf.Abs(minZ));

            Array.Clear(tile.TSurface.Heights, 0, tile.TSurface.Heights.Length);

            var job = new GeneratorSettings.PerlinHeightJob
            {
                Positions = samplePositions,
                Results = heightResults,
                Params = g.HeightParams
            };

            JobHandle handle = job.Schedule(samplePositions.Length, 64);
            handle.Complete();

            for (int i = 0; i < originalVerts.Length; i++)
            {
                Vector3 ov = originalVerts[i];
                float newY = heightResults[i];
                Vector3 nv = new Vector3(ov.x, newY, ov.z);
                newVerts[i] = nv;

                float nx = width > 0.0001f ? Mathf.InverseLerp(minX, maxX, nv.x) : 0f;
                float nz = depth > 0.0001f ? Mathf.InverseLerp(minZ, maxZ, nv.z) : 0f;

                int xIdx = Mathf.Clamp(Mathf.RoundToInt(nx * (resolution - 1)), 0, resolution - 1);
                int zIdx = Mathf.Clamp(Mathf.RoundToInt(nz * (resolution - 1)), 0, resolution - 1);

                tile.TSurface.Heights[xIdx, zIdx] = nv.y;
            }
        }
        finally
        {
            if (heightResults.IsCreated) heightResults.Dispose();
            if (samplePositions.IsCreated) samplePositions.Dispose();
        }

        tile.SurfaceRevision++;

        tile.Meshes.vertices = newVerts;
        tile.Meshes.RecalculateNormals();
        tile.Meshes.RecalculateBounds();

        var meshCols = tile.Object.GetComponent<MeshCollider>();
        meshCols.sharedMesh = tile.Meshes;
    }
}
