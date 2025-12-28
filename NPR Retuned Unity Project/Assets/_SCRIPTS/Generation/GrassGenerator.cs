using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class GrassGenerator : MonoBehaviour
{
    private const int MaxBatchSize = 1023;
    private const int ColliderBufferSize = 64;

    [SerializeField] private Mesh grassMesh;
    [SerializeField] private Material grassMat;
    [SerializeField, Min(0.1f)] private float minBladeSpacing = 1.5f;
    [SerializeField, Range(128, 4096)] private int maxBladeCount = 2048;
    [SerializeField, Range(1, 8)] private int maxTilesRebuiltPerFrame = 2;
    [Header("Exclusion Settings")]
    [SerializeField] private LayerMask grassBlockerMask;
    [SerializeField, Min(0.1f)] private float blockerCheckRadius = 0.75f;
    [SerializeField] private float blockerCheckHeightOffset = 0.25f;
    [SerializeField, Min(0.5f)] private float blockerSignatureHeight = 6f;

    private enum GrassExclusionType
    {
        Circle,
        Rectangle
    }

    private struct GrassExclusion
    {
        public GrassExclusionType Type;
        public float2 Center;
        public float2 Extents;
        public float Radius;
        public float2 AxisX;
        public float2 AxisY;
    }

    private readonly List<Vector2> _poissonPoints = new(1024);
    private readonly Queue<int> _activeList = new();
    private readonly Dictionary<Vector2Int, GrassTileCache> _tileCache = new();
    private readonly List<GrassTileCache> _activeTiles = new();
    private readonly List<Matrix4x4> _matrixBuildBuffer = new(4096);
    private readonly Collider[] _overlapBuffer = new Collider[ColliderBufferSize];
    private readonly Collider[] _signatureBuffer = new Collider[ColliderBufferSize];
    private readonly List<int> _signatureIds = new(32);
    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;


    private GrassTileCache GetOrCreateCache(Tile tile)
    {
        if (tile == null) return null;

        if (!_tileCache.TryGetValue(tile.Position, out var cache))
        {
            cache = new GrassTileCache(tile);
            _tileCache[tile.Position] = cache;
        }
        else if (cache.Tile != tile)
        {
            cache.Tile = tile;
        }

        return cache;
    }
    public IEnumerator GenerateGrass()
    {
        Vector3 p = _playerPos;
        int pX = Mathf.FloorToInt(p.x / g.CellSize);
        int pZ = Mathf.FloorToInt(p.z / g.CellSize);

        for (int x = pX - g.PlaceDistance - g.CullMargin; x <= pX + g.PlaceDistance + g.CullMargin; x++)
        {
            for (int z = pZ - g.PlaceDistance - g.CullMargin; z <= pZ + g.PlaceDistance + g.CullMargin; z++)
            {
                if (g.TileDict.TryGetValue(PosUtil.GetWorldPos(new Vector2Int(x, z)), out Tile tile))
                {
                    if (tile.Object != null && Mathf.Abs(x - pX) < g.ViewDistance * 0.25f && Mathf.Abs(z - pZ) < g.ViewDistance * 0.25f)
                    {
                        PopulateGrass(tile);
                    }
                    else DepopulateGrass(tile);
                }
            }
            if(x % 3 ==0) yield return null;
        }
    }
    private void LateUpdate()
    {
        if (grassMesh == null || grassMat == null) return;

        int rebuildsThisFrame = 0;

        for (int i = 0; i < _activeTiles.Count; i++)
        {
            var cache = _activeTiles[i];

            if (cache.Tile?.Object == null)
            {
                RemoveActiveAt(i);
                i--;
                continue;
            }

            if (grassBlockerMask.value != 0)
            {
                int signature = ComputeObstacleSignature(cache);
                if (signature != cache.ObstacleSignature)
                {
                    cache.ObstacleSignature = signature;
                    cache.NeedsRebuild = true;
                }
            }

            if ((cache.Tile.SurfaceRevision != cache.Revision || cache.NeedsRebuild) && !cache.IsBuilding && rebuildsThisFrame < maxTilesRebuiltPerFrame)
            {
                ScheduleBuild(cache.Tile, cache);
                rebuildsThisFrame++;
            }

            if (cache.IsBuilding && cache.BuildHandle.IsCompleted)
            {
                cache.BuildHandle.Complete();
                FinalizeBuild(cache);
            }

            if (cache.Batches.Count == 0) continue;

            for (int b = 0; b < cache.Batches.Count; b++)
            {
                var batch = cache.Batches[b];
                if (batch.Length == 0) continue;
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMat, batch);
            }
        }
    }

    public void PopulateGrass(Tile tile)
    {
        if (tile == null || tile.Object == null) return;

        var cache = GetOrCreateCache(tile);

        if (cache.Tile != tile)
        {
            CancelPendingBuild(cache, applyResult: false);
            cache.Tile = tile;
            cache.Revision = -1;
            cache.NeedsRebuild = true;
        }

        if (!cache.IsActive)
        {
            cache.IsActive = true;
            _activeTiles.Add(cache);
        }

        if ((tile.SurfaceRevision != cache.Revision || cache.NeedsRebuild) && !cache.IsBuilding)
        {
            ScheduleBuild(tile, cache);
        }
    }

    public void DepopulateGrass(Tile tile)
    {
        if (tile == null) return;
        if (!_tileCache.TryGetValue(tile.Position, out var cache)) return;
        if (!cache.IsActive) return;

        CancelPendingBuild(cache, applyResult: false);

        int index = _activeTiles.IndexOf(cache);
        if (index >= 0)
        {
            RemoveActiveAt(index);
        }
        else
        {
            cache.IsActive = false;
        }
    }

    public void ApplyWorldOffset(Vector3 delta)
    {
        if (delta == Vector3.zero) return;

        float3 offset = new float3(delta.x, delta.y, delta.z);

        foreach (var cache in _tileCache.Values)
        {
            if (cache == null) continue;

            if (cache.IsBuilding)
            {
                cache.BuildHandle.Complete();
            }

            if (cache.PendingWorldPositions.IsCreated)
            {
                for (int i = 0; i < cache.PendingWorldPositions.Length; i++)
                {
                    cache.PendingWorldPositions[i] = cache.PendingWorldPositions[i] + offset;
                }
            }

            for (int b = 0; b < cache.Batches.Count; b++)
            {
                var batch = cache.Batches[b];
                if (batch == null || batch.Length == 0) continue;

                for (int i = 0; i < batch.Length; i++)
                {
                    Matrix4x4 matrix = batch[i];
                    matrix.m03 += delta.x;
                    matrix.m13 += delta.y;
                    matrix.m23 += delta.z;
                    batch[i] = matrix;
                }
            }
        }
    }

    private void OnDisable()
    {
        foreach (var cache in _tileCache.Values)
        {
            CancelPendingBuild(cache, applyResult: false);
            cache.Batches.Clear();
            cache.IsActive = false;
        }

        _activeTiles.Clear();
    }

    private void ScheduleBuild(Tile tile, GrassTileCache cache)
    {
        CancelPendingBuild(cache, applyResult: false);

        cache.Revision = tile.SurfaceRevision;
        cache.PendingInstanceCount = 0;
        cache.NeedsRebuild = false;

        TileSurface s = tile.TSurface;

        float minX = s.MinX;
        float maxX = s.MaxX;
        float minZ = s.MinZ;
        float maxZ = s.MaxZ;

        if (maxX - minX < 0.0001f)
        {
            minX = -s.HalfSize;
            maxX = s.HalfSize;
        }

        if (maxZ - minZ < 0.0001f)
        {
            minZ = -s.HalfSize;
            maxZ = s.HalfSize;
        }

        float stepX = s.StepX > 0.0001f ? s.StepX : (maxX - minX) / Mathf.Max(1, s.Resolution - 1);
        float stepZ = s.StepZ > 0.0001f ? s.StepZ : (maxZ - minZ) / Mathf.Max(1, s.Resolution - 1);

        int seed = (int)math.hash(new int3(tile.Position.x, tile.Position.y, tile.SurfaceRevision));
        if (seed == 0) seed = 1;

        var points = GeneratePoissonPoints(minX, maxX, minZ, maxZ, minBladeSpacing, seed, maxBladeCount);

        if (cache.Exclusions.Count > 0)
        {
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (IsExcluded(cache, new float2(points[i].x, points[i].y)))
                {
                    points.RemoveAt(i);
                }
            }
        }
        int total = points.Count;
        cache.PendingInstanceCount = total;

        if (total == 0)
        {
            cache.InstanceCount = 0;
            cache.Batches.Clear();
            return;
        }

        var localPositions = new NativeArray<float2>(total, Allocator.TempJob);
        for (int i = 0; i < total; i++)
        {
            Vector2 p = points[i];
            localPositions[i] = new float2(p.x, p.y);
        }

        int resolution = s.Resolution;
        var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                heights[z * resolution + x] = s.Heights[x, z];
            }
        }

        var worldPositions = new NativeArray<float3>(total, Allocator.TempJob);
        float4x4 localToWorld = tile.Object.transform.localToWorldMatrix;

        var job = new GrassWorldPositionJob
        {
            LocalPositions = localPositions,
            Heights = heights,
            WorldPositions = worldPositions,
            Resolution = resolution,
            MinX = minX,
            MinZ = minZ,
            StepX = math.max(stepX, 0.0001f),
            StepZ = math.max(stepZ, 0.0001f),
            LocalToWorld = localToWorld
        };

        cache.BuildHandle = job.Schedule(total, 64);
        cache.PendingLocalPositions = localPositions;
        cache.PendingWorldPositions = worldPositions;
        cache.PendingHeightSamples = heights;
        cache.IsBuilding = true;
    }

    private int ComputeObstacleSignature(GrassTileCache cache)
    {
        if (grassBlockerMask.value == 0 || cache?.Tile?.Object == null) return 0;

        float halfSize = cache.Tile.TSurface.HalfSize > 0.0001f ? cache.Tile.TSurface.HalfSize : g.CellSize * 0.5f;
        float halfHeight = blockerSignatureHeight * 0.5f;
        Vector3 center = cache.Tile.Object.transform.position + Vector3.up * halfHeight;
        Vector3 halfExtents = new Vector3(halfSize, halfHeight, halfSize);
        Quaternion rotation = cache.Tile.Object.transform.rotation;

        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _signatureBuffer, rotation, grassBlockerMask, QueryTriggerInteraction.Ignore);
        if (count <= 0) return 0;

        _signatureIds.Clear();
        Transform tileRoot = cache.Tile.Object.transform;

        for (int i = 0; i < count; i++)
        {
            Collider col = _signatureBuffer[i];
            if (col == null) continue;

            Transform owner = col.transform;
            if (owner == tileRoot || owner.IsChildOf(tileRoot)) continue;

            _signatureIds.Add(col.GetInstanceID());
        }

        if (_signatureIds.Count == 0) return 0;

        _signatureIds.Sort();
        int signature = _signatureIds.Count;
        for (int i = 0; i < _signatureIds.Count; i++)
        {
            signature = unchecked(signature * 486187739 + _signatureIds[i]);
        }

        return signature;
    }

    private static bool IsExcluded(GrassTileCache cache, float2 localPos)
    {
        if (cache.Exclusions.Count == 0) return false;

        for (int i = 0; i < cache.Exclusions.Count; i++)
        {
            var exclusion = cache.Exclusions[i];
            switch (exclusion.Type)
            {
                case GrassExclusionType.Circle:
                    float2 diff = localPos - exclusion.Center;
                    if (math.lengthsq(diff) <= exclusion.Radius * exclusion.Radius)
                        return true;
                    break;

                case GrassExclusionType.Rectangle:
                    float2 delta = localPos - exclusion.Center;
                    float2 axisX = exclusion.AxisX;
                    float2 axisY = exclusion.AxisY;
                    float lenX = math.length(axisX);
                    float lenY = math.length(axisY);

                    if (lenX < 0.0001f || lenY < 0.0001f)
                    {
                        float2 ext = exclusion.Extents;
                        if (ext.x <= 0f && ext.y <= 0f) break;
                        if (math.abs(delta.x) <= ext.x && math.abs(delta.y) <= ext.y)
                            return true;
                    }
                    else
                    {
                        float2 dirX = axisX / lenX;
                        float2 dirY = axisY / lenY;
                        float distX = math.dot(delta, dirX);
                        float distY = math.dot(delta, dirY);
                        if (math.abs(distX) <= lenX && math.abs(distY) <= lenY)
                            return true;
                    }
                    break;
            }
        }

        return false;
    }

    private void FinalizeBuild(GrassTileCache cache)
    {
        cache.IsBuilding = false;
        cache.NeedsRebuild = false;

        int total = cache.PendingInstanceCount;
        cache.Batches.Clear();
        cache.InstanceCount = 0;

        if (total > 0)
        {
            _matrixBuildBuffer.Clear();

            for (int i = 0; i < total; i++)
            {
                float3 world = cache.PendingWorldPositions[i];
                Vector3 worldPos = new Vector3(world.x, world.y, world.z);

                if (BlocksGrassPlacement(cache, worldPos)) continue;

                float randomYaw = Random.Range(0f, 359f);
                float randomScale = Random.Range(0.6f, 1.4f);
                _matrixBuildBuffer.Add(Matrix4x4.TRS(worldPos, Quaternion.Euler(Vector3.up * randomYaw), Vector3.one * randomScale));
            }

            int filteredCount = _matrixBuildBuffer.Count;
            cache.InstanceCount = filteredCount;

            if (filteredCount > 0)
            {
                int neededBatches = Mathf.CeilToInt(filteredCount / (float)MaxBatchSize);
                EnsureBatchCapacity(cache, neededBatches);

                int offset = 0;
                for (int b = 0; b < neededBatches; b++)
                {
                    int count = Mathf.Min(MaxBatchSize, filteredCount - offset);
                    Matrix4x4[] batch = cache.Batches[b];
                    if (batch.Length != count)
                    {
                        batch = new Matrix4x4[count];
                    }

                    for (int i = 0; i < count; i++)
                    {
                        batch[i] = _matrixBuildBuffer[offset + i];
                    }

                    cache.Batches[b] = batch;
                    offset += count;
                }

                while (cache.Batches.Count > neededBatches)
                {
                    cache.Batches.RemoveAt(cache.Batches.Count - 1);
                }
            }
            else
            {
                cache.Batches.Clear();
            }

            _matrixBuildBuffer.Clear();
        }

        DisposePendingJobData(cache);
    }

    private void CancelPendingBuild(GrassTileCache cache, bool applyResult)
    {
        if (!cache.IsBuilding) return;

        cache.BuildHandle.Complete();
        if (applyResult)
        {
            FinalizeBuild(cache);
        }
        else
        {
            cache.IsBuilding = false;
            cache.NeedsRebuild = true;
            DisposePendingJobData(cache);
        }
    }

    private static void DisposePendingJobData(GrassTileCache cache)
    {
        if (cache.PendingLocalPositions.IsCreated) cache.PendingLocalPositions.Dispose();
        if (cache.PendingWorldPositions.IsCreated) cache.PendingWorldPositions.Dispose();
        if (cache.PendingHeightSamples.IsCreated) cache.PendingHeightSamples.Dispose();

        cache.PendingLocalPositions = default;
        cache.PendingWorldPositions = default;
        cache.PendingHeightSamples = default;
        cache.PendingInstanceCount = 0;
    }

    private bool BlocksGrassPlacement(GrassTileCache cache, Vector3 worldPos)
    {
        if (grassBlockerMask.value == 0) return false;

        Vector3 probe = worldPos;
        probe.y += blockerCheckHeightOffset;

        int hitCount = Physics.OverlapSphereNonAlloc(probe, blockerCheckRadius, _overlapBuffer, grassBlockerMask, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0) return false;

        Transform tileRoot = cache.Tile?.Object != null ? cache.Tile.Object.transform : null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            Transform owner = col.transform;
            if (tileRoot != null && (owner == tileRoot || owner.IsChildOf(tileRoot))) continue;

            return true;
        }

        return false;
    }

    private IList<Vector2> GeneratePoissonPoints(float minX, float maxX, float minZ, float maxZ, float radius, int seed, int maxCount)
    {
        _poissonPoints.Clear();
        _activeList.Clear();

        float width = Mathf.Max(0.0001f, maxX - minX);
        float height = Mathf.Max(0.0001f, maxZ - minZ);
        float cell = radius / Mathf.Sqrt(2f);

        int gridSizeX = Mathf.Max(1, Mathf.CeilToInt(width / cell));
        int gridSizeZ = Mathf.Max(1, Mathf.CeilToInt(height / cell));
        var grid = new int[gridSizeX, gridSizeZ];
        for (int gx = 0; gx < gridSizeX; gx++)
        {
            for (int gz = 0; gz < gridSizeZ; gz++)
            {
                grid[gx, gz] = -1;
            }
        }

        uint state = (uint)seed;
        if (state == 0) state = 1;
        Unity.Mathematics.Random rng = new(state);
        Vector2 first = new(rng.NextFloat(minX, maxX), rng.NextFloat(minZ, maxZ));
        AddPoint(first);

        while (_activeList.Count > 0 && _poissonPoints.Count < maxCount)
        {
            int idx = _activeList.Dequeue();
            Vector2 center = _poissonPoints[idx];

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = rng.NextFloat(0f, Mathf.PI * 2f);
                float dist = rng.NextFloat(radius, radius * 2f);
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                if (candidate.x < minX || candidate.x > maxX || candidate.y < minZ || candidate.y > maxZ)
                    continue;
                if (!IsValid(candidate))
                    continue;

                AddPoint(candidate);
            }
        }

        return _poissonPoints;

        void AddPoint(Vector2 pos)
        {
            _poissonPoints.Add(pos);
            _activeList.Enqueue(_poissonPoints.Count - 1);
            int gx = Mathf.Clamp(Mathf.FloorToInt((pos.x - minX) / cell), 0, gridSizeX - 1);
            int gz = Mathf.Clamp(Mathf.FloorToInt((pos.y - minZ) / cell), 0, gridSizeZ - 1);
            grid[gx, gz] = _poissonPoints.Count - 1;
        }

        bool IsValid(Vector2 pos)
        {
            int gx = Mathf.Clamp(Mathf.FloorToInt((pos.x - minX) / cell), 0, gridSizeX - 1);
            int gz = Mathf.Clamp(Mathf.FloorToInt((pos.y - minZ) / cell), 0, gridSizeZ - 1);

            for (int nx = -2; nx <= 2; nx++)
            {
                int cx = gx + nx;
                if (cx < 0 || cx >= gridSizeX) continue;
                for (int nz = -2; nz <= 2; nz++)
                {
                    int cz = gz + nz;
                    if (cz < 0 || cz >= gridSizeZ) continue;

                    int sampleIdx = grid[cx, cz];
                    if (sampleIdx < 0) continue;
                    if (Vector2.SqrMagnitude(pos - _poissonPoints[sampleIdx]) < radius * radius)
                        return false;
                }
            }

            return true;
        }
    }

    private void RemoveActiveAt(int index)
    {
        int last = _activeTiles.Count - 1;
        if (index < 0 || index > last) return;

        GrassTileCache cache = _activeTiles[index];
        CancelPendingBuild(cache, applyResult: false);
        cache.IsActive = false;

        if (index != last)
        {
            _activeTiles[index] = _activeTiles[last];
        }

        _activeTiles.RemoveAt(last);
    }

    private static void EnsureBatchCapacity(GrassTileCache cache, int neededBatches)
    {
        while (cache.Batches.Count < neededBatches)
        {
            cache.Batches.Add(Array.Empty<Matrix4x4>());
        }
    }

    [BurstCompile]
    private struct GrassWorldPositionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> LocalPositions;
        [ReadOnly] public NativeArray<float> Heights;
        public NativeArray<float3> WorldPositions;
        public int Resolution;
        public float MinX;
        public float MinZ;
        public float StepX;
        public float StepZ;
        public float4x4 LocalToWorld;

        public void Execute(int index)
        {
            float2 local = LocalPositions[index];

            if (Resolution <= 1 || Heights.Length == 0)
            {
                float3 worldFallback = math.transform(LocalToWorld, new float3(local.x, Heights.Length > 0 ? Heights[0] : 0f, local.y));
                WorldPositions[index] = worldFallback;
                return;
            }

            float scaledU = (local.x - MinX) / StepX;
            float scaledV = (local.y - MinZ) / StepZ;

            float maxCoord = Resolution - 1;
            scaledU = math.clamp(scaledU, 0f, maxCoord);
            scaledV = math.clamp(scaledV, 0f, maxCoord);

            int x0 = math.clamp((int)math.floor(scaledU), 0, Resolution - 2);
            int z0 = math.clamp((int)math.floor(scaledV), 0, Resolution - 2);
            float tx = scaledU - x0;
            float tz = scaledV - z0;

            float h00 = Heights[z0 * Resolution + x0];
            float h10 = Heights[z0 * Resolution + (x0 + 1)];
            float h01 = Heights[(z0 + 1) * Resolution + x0];
            float h11 = Heights[(z0 + 1) * Resolution + (x0 + 1)];

            float h0 = math.lerp(h00, h10, tx);
            float h1 = math.lerp(h01, h11, tx);
            float height = math.lerp(h0, h1, tz);

            float3 world = math.transform(LocalToWorld, new float3(local.x, height, local.y));
            WorldPositions[index] = world;
        }
    }

    private class GrassTileCache
    {
        public GrassTileCache(Tile tile)
        {
            Tile = tile;
        }

        public Tile Tile;
        public readonly List<Matrix4x4[]> Batches = new();
        public readonly List<GrassExclusion> Exclusions = new();
        public int InstanceCount;
        public int Revision = -1;
        public bool IsActive;
        public bool NeedsRebuild;

        public bool IsBuilding;
        public JobHandle BuildHandle;
        public NativeArray<float2> PendingLocalPositions;
        public NativeArray<float3> PendingWorldPositions;
        public NativeArray<float> PendingHeightSamples;
        public int PendingInstanceCount;
        public int ObstacleSignature;
    }
}
