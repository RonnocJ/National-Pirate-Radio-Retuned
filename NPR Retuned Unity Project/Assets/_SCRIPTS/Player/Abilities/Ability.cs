using UnityEngine;
public enum AbilityPosition
{
    None,
    Left,
    Right,
    Middle
}
public enum AbilityType
{
    Charge,
    Single,
    Continuous
}
public abstract class Ability : MonoBehaviour
{
    public AbilityPosition Position;
    protected virtual  AbilityType Type { get => type; set => type = value; }
    private AbilityType type;
    public float AbilityMax;
    public float AbilityOverload;
    public float Cooldown;
    protected float _abilityTimeActive;
    private Vector2 _abilityInput => PInputManager.root.actions[PlayerActionType.Ability].v2Value;


    [SerializeField] protected AbilityPosition _chargingPosition;
    void Update()
    {
        if (_abilityTimeActive < 0)
        {
            _abilityTimeActive += Time.deltaTime;
            return;
        }

        if (_abilityTimeActive > 0 && Type == AbilityType.Continuous && _abilityInput == Vector2.zero)
        {
            _abilityTimeActive -= Time.deltaTime;
            return;
        }

        if (_abilityInput != Vector2.zero && _abilityTimeActive < AbilityOverload && _abilityTimeActive >= 0)
        {
            if (_abilityInput.x < 0 && _chargingPosition != AbilityPosition.Middle) _chargingPosition = AbilityPosition.Left;
            else if (_abilityInput.x > 0 && _chargingPosition != AbilityPosition.Middle) _chargingPosition = AbilityPosition.Right;

            if (_abilityInput.y > 0.75f && _abilityTimeActive < 0.05f) _chargingPosition = AbilityPosition.Middle;

            _abilityTimeActive += Time.deltaTime;
            AbilityHeld();
        }

        if ((_abilityTimeActive > 0 && (_abilityInput == Vector2.zero || Type == AbilityType.Single)) || _abilityTimeActive >= AbilityOverload)
        {
            if (_chargingPosition == AbilityPosition.Left) ReleaseLeft(_abilityTimeActive >= AbilityOverload);
            else if (_chargingPosition == AbilityPosition.Right) ReleaseRight(_abilityTimeActive >= AbilityOverload);
            else if (_chargingPosition == AbilityPosition.Middle) ReleaseMiddle(_abilityTimeActive >= AbilityOverload);

            if(Type != AbilityType.Continuous) _abilityTimeActive = -Cooldown;

            _chargingPosition = AbilityPosition.None;
        }

    }
    protected virtual void AbilityHeld()
    {

    }
    protected virtual void ReleaseLeft(bool overloaded)
    {
        
    }
    protected virtual void ReleaseRight(bool overloaded)
    {
        
    }
    protected virtual void ReleaseMiddle(bool overloaded)
    {
        
    }
}