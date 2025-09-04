using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
public class TerrainGenerator : MonoBehaviour
{
    [Header("Tile Settings")]
    [SerializeField] private int cellSize;
    [SerializeField] private int viewDistance;
    [SerializeField] private int cullMargin;
    [SerializeField] private ScriptableObject[] constructs;
    [Header("Noise Settings")]
    [SerializeField] private float noiseScale;
    [SerializeField] private float heightScale;
    [Header("Grass Settings")]
    [SerializeField] private Transform grassParent;
    private ObjectPool _grassPool;
    private Dictionary<Vector3, Tile> _tileDict = new();
    private Dictionary<TConType, TConData> _tileConDict = new();
    private Vector3 _playerPos => VanController.root.transform.position;

    IEnumerator Start()
    {
        foreach (var con in constructs)
        {
            if (con is TileConstruct)
            {
                TileConstruct tCon = (TileConstruct)con;

                _tileConDict.Add(tCon.constructName, tCon.data);
                _tileConDict[tCon.constructName].ReqCon = 0;
            }
        }

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Tile garageTile = AddTile(TConType.Garage, new Vector3(x * cellSize, 0, z * cellSize));
            }
        }


        int poolCount = (2 * (viewDistance + cullMargin) + 1) * (2 * (viewDistance + cullMargin) + 1);
        _grassPool = new ObjectPool(poolCount, _tileConDict[TConType.Grass].Prefab, grassParent);

        while (true)
        {
            Vector3 p = _playerPos;
            int pX = Mathf.FloorToInt(p.x / cellSize);
            int pZ = Mathf.FloorToInt(p.z / cellSize);

            for (int x = pX - viewDistance - cullMargin; x <= pX + viewDistance + cullMargin; x++)
            {
                for (int z = pZ - viewDistance - cullMargin; z <= pZ + viewDistance + cullMargin; z++)
                {
                    Vector3 pos = new Vector3(x * cellSize, 0, z * cellSize);
                    if (Mathf.Abs(x - pX) <= viewDistance && Mathf.Abs(z - pZ) <= viewDistance)
                    {
                        Tile tile = AddTile(TConType.Grass, pos);

                        if (tile.Object == null && tile.Type == TConType.Grass)
                        {
                            PlaceTile(tile, _grassPool, pos);
                        }
                    }
                }
            }

            yield return null;
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

        GameObject newObj = poolType.Retrive(tile);

        ResetMesh(newObj);
        MoveObject(newObj, tile.Position, newPos);
    }
    public void ResetMesh(GameObject movingObj)
    {
        if (_tileDict.TryGetValue(movingObj.transform.position, out Tile oldTile))
        {
            // Only clear and reset if this object actually belonged to that tile
            if (oldTile.Object == movingObj)
            {
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
    public void MoveObject(GameObject movingObj, Vector3 oldPos, Vector3 newPos)
    {
        if (_tileDict.TryGetValue(newPos, out Tile newTile))
        {
            newTile.Object = movingObj;

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
    [BurstCompile]
    private struct HeightJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> original;
        public NativeArray<float3> deformed;
        public float3 tilePos;
        public float cosYaw;
        public float sinYaw;
        public float noiseScale;
        public float heightScale;

        public void Execute(int index)
        {
            float3 v = original[index];

            float worldX = tilePos.x + (v.x * cosYaw) - (v.z * sinYaw);
            float worldZ = tilePos.z + (v.x * sinYaw) + (v.z * cosYaw);
            float xCoord = worldX / noiseScale;
            float zCoord = worldZ / noiseScale;
            float amplitude = Mathf.Min(Vector3.Distance(Vector3.zero, new Vector3(worldX, 0, worldZ)) - 192f, 1024f) / 1024f * heightScale;

            if (amplitude < 0) amplitude = 0;

            float n = noise.snoise(new float2(xCoord, zCoord)) * 0.5f + 0.5f;
            float baseY = original[index].y + n * amplitude;


            deformed[index] = new float3(v.x, baseY, v.z);
        }
    }

    public void GenerateHeight(Tile tile)
    {
        float yaw = tile.Object != null ? tile.Object.transform.eulerAngles.y : tile.YRot;
        float cosYaw = Mathf.Cos(yaw * Mathf.Deg2Rad);
        float sinYaw = Mathf.Sin(yaw * Mathf.Deg2Rad);
        Vector3 worldPos = tile.Object != null ? tile.Object.transform.position : tile.Position;
        float3 tilePos = new float3(worldPos.x, worldPos.y, worldPos.z);

        Vector3[] originalVerts = tile.OriginalVertices;
        var orig = new NativeArray<float3>(originalVerts.Length, Allocator.TempJob);
        var deformed = new NativeArray<float3>(originalVerts.Length, Allocator.TempJob);
        for (int i = 0; i < originalVerts.Length; i++)
        {
            Vector3 ov = originalVerts[i];
            orig[i] = new float3(ov.x, ov.y, ov.z);
        }

        var job = new HeightJob
        {
            original = orig,
            deformed = deformed,
            tilePos = tilePos,
            cosYaw = cosYaw,
            sinYaw = sinYaw,
            noiseScale = noiseScale,
            heightScale = heightScale,
        };

        JobHandle handle = job.Schedule(originalVerts.Length, 64);
        handle.Complete();

        Vector3[] newVerts = new Vector3[originalVerts.Length];
        for (int i = 0; i < newVerts.Length; i++)
        {
            float3 v = deformed[i];
            newVerts[i] = new Vector3(v.x, v.y, v.z);
        }

        tile.MeshVertices = newVerts;
        tile.Meshes.vertices = newVerts;
        tile.Meshes.RecalculateNormals();
        tile.Meshes.RecalculateBounds();

        orig.Dispose();
        deformed.Dispose();

        var meshCols = tile.Object.GetComponentsInChildren<MeshCollider>();
        for (int m = 0; m < meshCols.Length; m++)
        {
            if (meshCols[m] != null)
                meshCols[m].sharedMesh = tile.Meshes;
        }
    }
}
