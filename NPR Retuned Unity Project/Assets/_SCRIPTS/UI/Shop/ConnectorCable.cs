using GogoGaga.OptimizedRopesAndCables;
using UnityEngine;

public class ConnectorCable : Grabbable
{
    public bool Active;
    public UpgradeWheel AttachedWheel;
    public PinColor Color;
    [SerializeField] private LineRenderer wireLine;
    [SerializeField] Transform wireEndPoint;
    [SerializeField] private Rope wire;
    [SerializeField] private AnimationCurve wireCurve;
    public Rigidbody Rb;
    private void Awake()
    {
        Rb.AddForce(Random.insideUnitSphere * 1000f);
    }
    void Update()
    {
        int len = wireLine.positionCount;
        wire.ropeLength = wireCurve.Evaluate(Vector3.Distance(wireEndPoint.position, transform.position));

        if (!Active) return;

        transform.GetChild(0).rotation = Quaternion.FromToRotation(transform.up, Rb.isKinematic? Vector3.down : (wireLine.GetPosition(len - 1) - wireLine.GetPosition(len - 2)).normalized);
    }

    public override void OnDrag()
    {
        base.OnDrag();
        if (!Active) return;
        Rb.isKinematic = true;
        GetComponentInChildren<SphereCollider>().enabled = false;

        Vector3 tp = MouseMover.root.transform.position + MouseOffset;
        transform.position = Vector3.Lerp(transform.position, tp, Time.deltaTime * TargetMoveSpeed);

        if (Vector3.Distance(wireEndPoint.position, transform.position) > 6.5f) MouseMover.root.ForceRelease();
    }

    public override void OnRelease()
    {
        base.OnRelease();
        if (!Active) return;

        Rb.isKinematic = false;
        GetComponentInChildren<SphereCollider>().enabled = true;
    }
}