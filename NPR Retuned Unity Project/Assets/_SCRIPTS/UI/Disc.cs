using UnityEngine;

public class Disc : Grabbable
{
    public SongName LoadedSong;
    public Rigidbody rb;
        public Collider grabPlane;
    [SerializeField] private float gravity;
    [SerializeField] private float releaseForce;
    [SerializeField] private float distanceThreshold;
    [SerializeField] private float minLocalYWhileGrabbing;
    private Vector3 _lastWorldPos;
    private Vector3 _smoothedVelocity;
    void OnEnable()
    {
        _lastWorldPos = transform.position;
        _smoothedVelocity = Vector3.zero;
    }
    void FixedUpdate()
    {
        if (rb.isKinematic) return;

        Vector3 localPos = transform.parent.localPosition;
        localPos.z = 0.925f;
        transform.parent.localPosition = localPos;

        rb.AddForce(-transform.parent.up * gravity, ForceMode.Acceleration);
    }
    public override void OnDrag()
    {
        rb.isKinematic = true;
        grabPlane.enabled = true;

        Vector3 lp = MouseMover.root.transform.localPosition + MouseOffset;
        lp.z = 0.925f;
        lp.y = Mathf.Max(lp.y, minLocalYWhileGrabbing);
        transform.parent.localPosition = Vector3.Lerp(transform.parent.localPosition, lp, Time.deltaTime * TargetMoveSpeed);

        float dt = Time.deltaTime > 0 ? Time.deltaTime : 0.0001f;
        Vector3 instVel = (transform.parent.position - _lastWorldPos) / dt;
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, instVel, Mathf.Clamp01(20f * Time.deltaTime));
        _lastWorldPos = transform.parent.position;

        if (Vector3.Distance(MouseMover.root.transform.position, transform.parent.position) > distanceThreshold)
        {
            MouseMover.root.ForceRelease();
        }
    }
    public override void OnRelease()
    {
        base.OnRelease();
        rb.isKinematic = false;
        grabPlane.enabled = false;

        Vector3 throwVel = Vector3.ClampMagnitude(_smoothedVelocity, 10) * releaseForce;

        if (transform.parent != null)
        {
            Vector3 localVel = transform.parent.parent.InverseTransformDirection(throwVel);
            localVel.z = 0f;
            throwVel = transform.parent.parent.TransformDirection(localVel);
        }

        rb.linearVelocity = throwVel;
    }
}
