using UnityEngine;

public abstract class Grabbable : MonoBehaviour
{
    [HideInInspector] public bool Grabbed;
    public Vector3 MouseOffset;
    public float TargetMoveSpeed;
    public virtual void OnHover()
    {
        
    }
    public virtual void OnDrag()
    {
        Grabbed = true;
    }
    public virtual void OnRelease()
    {
        Grabbed = false;
    }
}