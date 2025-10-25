using UnityEngine;
public enum AbilityType
{
    Charge,
    Single,
    Continuous
}
public abstract class AbilityDefinition : MonoBehaviour
{
    public AbilityPosition Pos;
    public virtual AbilityType Type { get => type; set => type = value; }
    private AbilityType type;
    public float CooldownRate;
    [Header("Charge Settings")]
    public float AbilityMax;
    public float AbilityOverload;

    [Header("Single Settings")]
    public int Charges;
    public int MaxCharges;
    [Header("Continuous Settings")]
    public float MaxActiveTime;
    public float DrainRate;
    public virtual void AbilityHeld(float currentTime)
    {

    }
    public virtual void AbilityRelease(bool overloaded, float currentTime)
    {

    }
}