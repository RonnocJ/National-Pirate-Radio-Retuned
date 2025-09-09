using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile
{
    public TConType Type;
    public GameObject Object;
    public Vector3 Position;
    public float YRot;
    public Mesh Meshes;
    public int[] MeshTriangles;
    public Vector3[] MeshVertices;
    public Vector3[] OriginalVertices;
    public Tile(TConType type)
    {
        Type = type;
    }
}

public class TerrainGenerator : Singleton<TerrainGenerator>
{
    public Dictionary<GameObject, Tile> TileObjDict = new();
    [Header("Grass Settings")]
    [SerializeField] private Transform grassParent;
    private ObjectPool _grassPool;
    private Dictionary<Vector3, Tile> _tileDict = new();
    private Dictionary<TConType, TConData> _tileConDict = new();
    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;

    IEnumerator Start()
    {
        foreach (var con in g.TCons)
        {
            _tileConDict[con.constructName] = con.data;
        }

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Tile garageTile = AddTile(TConType.Garage, new Vector3(x * g.CellSize, 0, z * g.CellSize));
            }
        }

        int poolCount = (2 * (g.ViewDistance + g.CullMargin) + 1) * (2 * (g.ViewDistance + g.CullMargin) + 1);
        _grassPool = new ObjectPool();

        while (true)
        {
            Vector3 p = _playerPos;
            int pX = Mathf.FloorToInt(p.x / g.CellSize);
            int pZ = Mathf.FloorToInt(p.z / g.CellSize);

            for (int x = pX - g.ViewDistance - g.CullMargin; x <= pX + g.ViewDistance + g.CullMargin; x++)
            {
                for (int z = pZ - g.ViewDistance - g.CullMargin; z <= pZ + g.ViewDistance + g.CullMargin; z++)
                {
                    Vector3 pos = new Vector3(x * g.CellSize, 0, z * g.CellSize);
                    if (Mathf.Abs(x - pX) <= g.ViewDistance && Mathf.Abs(z - pZ) <= g.ViewDistance)
                    {
                        Tile tile = AddTile(TConType.Grass, pos);

                        if (tile.Object == null && tile.Type == TConType.Grass)
                        {
                            if (_grassPool.CreatedCount < Mathf.Min(Mathf.Pow(g.ViewDistance * 4f, 2), _tileDict.Count - 2))
                            {
                                _grassPool.Prewarm(1, _tileConDict[TConType.Grass].Prefab, grassParent);
                            }

                            PlaceTile(tile, _grassPool, pos);
                        }
                    }
                }

                yield return null;
            }

        }
    }

    Tile AddTile(TConType type, Vector3 checkPos, float yRot = 0)
    {
        if (!_tileDict.ContainsKey(checkPos))
        {
            Tile tile = new Tile(type);
            tile.YRot = yRot;
            tile.Position = checkPos;
            tile.OriginalVertices = _tileConDict[type].DefaultMeshes.vertices;
            _tileDict[checkPos] = tile;
            return tile;
        }
        else
        {
            return _tileDict[checkPos];
        }
    }

    void PlaceTile(Tile tile, ObjectPool poolType, Vector3 newPos)
    {
        if (tile == null) return;

        GameObject newObj = poolType.Get(_tileConDict[TConType.Grass].Prefab, grassParent);

        ResetMesh(newObj);
        MoveObject(newObj, newPos);
    }

    public void ResetMesh(GameObject movingObj)
    {
        if (_tileDict.TryGetValue(movingObj.transform.position, out Tile oldTile))
        {
            if (oldTile.Object == movingObj)
            {
                TileObjDict.Remove(oldTile.Object);
                oldTile.Object = null;

                MeshFilter oldFilter = movingObj.GetComponent<MeshFilter>();

                var mesh = oldFilter.mesh;
                mesh.Clear();
                mesh.vertices = _tileConDict[oldTile.Type].DefaultMeshes.vertices;
                mesh.triangles = _tileConDict[oldTile.Type].DefaultMeshes.triangles;

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }
    }

    public void MoveObject(GameObject movingObj, Vector3 newPos)
    {
        if (_tileDict.TryGetValue(newPos, out Tile newTile))
        {
            newTile.Object = movingObj;
            TileObjDict[newTile.Object] = newTile;

            newTile.Object.transform.position = newPos;
            newTile.Object.transform.localRotation = Quaternion.Euler(0f, newTile.YRot, 0f);
            newTile.Position = newPos;

            MeshFilter meshFilter = newTile.Object.GetComponent<MeshFilter>();

            Mesh mesh = meshFilter.mesh;
            newTile.Meshes = mesh;
            newTile.MeshTriangles = mesh.triangles;
            newTile.MeshVertices = mesh.vertices;

            GenerateHeight(newTile);
        }
    }

    public void GenerateHeight(Tile tile)
    {
        if (tile?.Object == null) return;

        float yaw = tile.Object.transform.eulerAngles.y;
        float cosYaw = Mathf.Cos(yaw * Mathf.Deg2Rad);
        float sinYaw = Mathf.Sin(yaw * Mathf.Deg2Rad);
        Vector3 tileWorldPos = tile.Object.transform.position;

        Vector3[] originalVerts = tile.OriginalVertices;
        Vector3[] newVerts = new Vector3[originalVerts.Length];

        for (int i = 0; i < originalVerts.Length; i++)
        {
            Vector3 ov = originalVerts[i];
            float worldX = tileWorldPos.x + (ov.x * cosYaw) - (ov.z * sinYaw);
            float worldZ = tileWorldPos.z + (ov.x * sinYaw) + (ov.z * cosYaw);

            float baseY = g.GetPerlinHeight(new Vector3(worldX, ov.y, worldZ));
            float newY = baseY;

            newVerts[i] = new Vector3(ov.x, newY, ov.z);
        }

        tile.MeshVertices = newVerts;
        tile.Meshes.vertices = newVerts;
        tile.Meshes.RecalculateNormals();
        tile.Meshes.RecalculateBounds();

        var meshCols = tile.Object.GetComponent<MeshCollider>();
        meshCols.sharedMesh = tile.Meshes;
    }
}