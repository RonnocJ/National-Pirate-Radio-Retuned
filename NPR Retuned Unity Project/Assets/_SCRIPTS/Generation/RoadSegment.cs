using UnityEngine;

[DisallowMultipleComponent]
public class RoadSegment : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshCollider meshCollider;

    private Vector3[] _baseVertices; // template plane verts (local)
    private Vector3[] _workVertices;
    private float _baseMinX, _baseMaxX, _baseMinZ, _baseMaxZ;

    private void Awake()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        var mesh = meshFilter.sharedMesh;
        _baseVertices = mesh.vertices;
        _workVertices = new Vector3[_baseVertices.Length];
        // Cache bounds along X and Z in local space
        _baseMinX = float.PositiveInfinity; _baseMaxX = float.NegativeInfinity;
        _baseMinZ = float.PositiveInfinity; _baseMaxZ = float.NegativeInfinity;
        foreach (var v in _baseVertices)
        {
            if (v.x < _baseMinX) _baseMinX = v.x;
            if (v.x > _baseMaxX) _baseMaxX = v.x;
            if (v.z < _baseMinZ) _baseMinZ = v.z;
            if (v.z > _baseMaxZ) _baseMaxZ = v.z;
        }

        // Ensure this object is in world space (no extra transforms)
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
        if (transform.parent != null)
        {
            transform.parent.position = Vector3.zero;
            transform.parent.rotation = Quaternion.identity;
            transform.parent.localScale = Vector3.one;
        }
    }

    // Deform this segment along a circular arc from start to end with yaw0->yaw1.
    // Uses raycasts to sample terrain height for each vertex, then offsets by heightOffset.
    public void DeformToArc(
        Vector3 startWorld,
        float yaw0Deg,
        Vector3 endWorld,
        float yaw1Deg,
        float heightOffset,
        float raycastHeight,
        LayerMask terrainMask)
    {
        var mesh = meshFilter.mesh;
        float L = Vector3.Distance(startWorld, endWorld);
        if (L < 0.001f) return;

        float yaw0 = yaw0Deg * Mathf.Deg2Rad;
        float yaw1 = yaw1Deg * Mathf.Deg2Rad;
        float dYaw = Mathf.DeltaAngle(yaw0Deg, yaw1Deg) * Mathf.Deg2Rad; // shortest signed delta in radians
        float minZ = _baseMinZ; float maxZ = _baseMaxZ; float dz = Mathf.Max(0.0001f, maxZ - minZ);
        float minX = _baseMinX; float maxX = _baseMaxX; // we preserve width from prefab

        Quaternion startRot = Quaternion.Euler(0f, yaw0Deg, 0f);

        for (int i = 0; i < _baseVertices.Length; i++)
        {
            Vector3 v = _baseVertices[i];
            float s = (v.z - minZ) / dz; // 0..1 along length
            float xOffset = v.x;         // lateral from prefab, kept as-is

            // Centerline displacement in start frame
            Vector3 centerLocal;
            if (Mathf.Abs(dYaw) < 1e-4f)
            {
                centerLocal = new Vector3(0f, 0f, L * s);
            }
            else
            {
                float R = L / dYaw; // radius signed by dYaw
                float a = dYaw * s; // angle at s
                centerLocal = new Vector3(R * Mathf.Sin(a), 0f, R * (1f - Mathf.Cos(a)));
            }

            // World center point
            Vector3 centerWorld = startWorld + (startRot * centerLocal);

            // Right vector at s (world)
            float yawAtS = yaw0 + dYaw * s; // radians
            Vector3 rightWorld = new Vector3(Mathf.Cos(yawAtS), 0f, -Mathf.Sin(yawAtS));

            Vector3 targetWorld = centerWorld + (rightWorld * xOffset);

            // Height sample
            Vector3 rayOrigin = targetWorld + Vector3.up * raycastHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                targetWorld.y = hit.point.y + heightOffset;
            }

            _workVertices[i] = transform.InverseTransformPoint(targetWorld); // local space (identity transform expected)
        }

        mesh.vertices = _workVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }
    }
}

