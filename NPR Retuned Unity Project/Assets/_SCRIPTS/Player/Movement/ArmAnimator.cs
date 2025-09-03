using UnityEngine;

public class ArmAnimator : MonoBehaviour
{
    [Header("Runtime Settings")]
    [SerializeField] private int armTargetIndex;
    [SerializeField] private Rect armRect;
    [Header("Gizmo Settings")]
    [SerializeField] private Vector3 armRectOffset;
    [SerializeField] private Vector3 armRectRotation;
    private VanWeapon _currentWeapon => WeaponSettings.root.currentWeapon;

    // Helper: Rectangle perimeter as 4 segments in local space (x,z)
    private Vector2[] GetPerimeterCorners()
    {
        float minX = armRect.x - (armRect.width / 2f);
        float maxX = armRect.x + (armRect.width / 2f);
        float minZ = armRect.y - (armRect.height / 2f);
        float maxZ = armRect.y + (armRect.height / 2f);
        return new Vector2[]
        {
            new Vector2(minX, minZ), // Bottom Left
            new Vector2(maxX, minZ), // Bottom Right
            new Vector2(maxX, maxZ), // Top Right
            new Vector2(minX, maxZ), // Top Left
        };
    }

    // Helper: Project a point onto the perimeter, returning the closest perimeter position and the distance along the perimeter
    private void ProjectPointToPerimeter(Vector2 point, out Vector2 closest, out float perimeterT)
    {
        Vector2[] corners = GetPerimeterCorners();
        float[] edgeLengths = new float[4];
        float totalLength = 0f;
        for (int i = 0; i < 4; i++)
        {
            edgeLengths[i] = Vector2.Distance(corners[i], corners[(i + 1) % 4]);
            totalLength += edgeLengths[i];
        }

        float minSqrDist = float.MaxValue;
        closest = corners[0];
        perimeterT = 0f;
        float accumulated = 0f;

        for (int i = 0; i < 4; i++)
        {
            Vector2 a = corners[i];
            Vector2 b = corners[(i + 1) % 4];
            Vector2 ab = b - a;
            float abLen = ab.magnitude;
            Vector2 abNorm = ab / abLen;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, abNorm) / abLen);
            Vector2 proj = a + ab * t;
            float sqrDist = (point - proj).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = proj;
                perimeterT = accumulated + abLen * t;
            }
            accumulated += abLen;
        }
        perimeterT /= totalLength; // Normalize to [0,1]
    }

    // Helper: Get a point on the perimeter given normalized t in [0,1]
    private Vector2 GetPointOnPerimeter(float t)
    {
        Vector2[] corners = GetPerimeterCorners();
        float[] edgeLengths = new float[4];
        float totalLength = 0f;
        for (int i = 0; i < 4; i++)
        {
            edgeLengths[i] = Vector2.Distance(corners[i], corners[(i + 1) % 4]);
            totalLength += edgeLengths[i];
        }
        float targetDist = t * totalLength;
        float accumulated = 0f;
        for (int i = 0; i < 4; i++)
        {
            if (targetDist <= accumulated + edgeLengths[i])
            {
                float segT = (targetDist - accumulated) / edgeLengths[i];
                return Vector2.Lerp(corners[i], corners[(i + 1) % 4], segT);
            }
            accumulated += edgeLengths[i];
        }
        return corners[0]; // fallback
    }

    // Helper: Get inward-facing angle for a perimeter t
    private float GetInwardAngle(float t)
    {
        // 0: bottom, 1: right, 2: top, 3: left
        if (t < 0.25f) return 0f;
        if (t < 0.5f) return 270f;
        if (t < 0.75f) return 180f;
        return 90f;
    }

    private float currentT = 0f; // Current position along perimeter [0,1]

    void FixedUpdate()
    {
        if (_currentWeapon == null || _currentWeapon.PalmTargets.Length == 0) return;

        Vector3 palmLocal = transform.parent.InverseTransformPoint(_currentWeapon.PalmTargets[armTargetIndex].position);
        Vector2 palm2D = new Vector2(palmLocal.x, palmLocal.z);

        // Find closest perimeter point to palm target
        ProjectPointToPerimeter(palm2D, out _, out float targetT);

        // On first frame, snap to closest perimeter point to current position
        if (Time.frameCount == 1 || float.IsNaN(currentT))
        {
            Vector2 arm2D = new Vector2(transform.localPosition.x, transform.localPosition.z);
            ProjectPointToPerimeter(arm2D, out _, out currentT);
        }

        // Move currentT toward targetT along the shortest path (perimeter is a loop)
        float deltaT = Mathf.DeltaAngle(currentT * 360f, targetT * 360f) / 360f;
        float moveStep = _currentWeapon.MoveSpeed * Time.deltaTime /  (GetPerimeterCorners()[0] - GetPerimeterCorners()[1]).magnitude; // normalize speed
        if (Mathf.Abs(deltaT) < moveStep)
            currentT = targetT;
        else
            currentT += Mathf.Sign(deltaT) * moveStep;

        // Wrap currentT to [0,1]
        if (currentT < 0f) currentT += 1f;
        if (currentT > 1f) currentT -= 1f;

        // Get position and angle
        Vector2 pos2D = GetPointOnPerimeter(currentT);
        float angle = GetInwardAngle(currentT);

        transform.localPosition = new Vector3(pos2D.x, 0, pos2D.y);
        transform.localRotation = Quaternion.Euler(0, angle, 0);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(transform.parent.position + armRectOffset, Quaternion.Euler(armRectRotation) * transform.parent.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(armRect.x + armRect.width / 2, armRect.y + armRect.height / 2, 0), new Vector3(armRect.width, armRect.height, 0.1f));
    }
}