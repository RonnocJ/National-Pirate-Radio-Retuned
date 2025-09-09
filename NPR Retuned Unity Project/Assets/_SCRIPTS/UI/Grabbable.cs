using UnityEngine;

public abstract class Grabbable : MonoBehaviour
{
    public Vector3 MouseOffset;
    public float TargetMoveSpeed;
    public virtual void OnHover()
    {

    }
    public virtual void OnDrag()
    {
        transform.localPosition = MouseMover.root.transform.localPosition + MouseOffset;
    }
    public virtual void OnRelease()
    {

    }
}