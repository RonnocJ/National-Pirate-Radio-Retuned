using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    public bool DisableAfterClick;
    public bool Enabled;
    public virtual void OnHover()
    {
        if (!Enabled) return;
    }
    public virtual void OnClick()
    {
        if (!Enabled) return;
    }
    public virtual void OnRelease()
    {
        if (DisableAfterClick) Enabled = false;
    }
}