using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Objects/Player/PlayerStats", order = 0)]
public class PlayerStats : ScriptableSingleton<PlayerStats>
{
    public float VehicleSpeed;
    public float WeaponDamage;
    public float AbilityStrength;
    public float RegenAmount;
    public float RegenDelay;
    public float MaxHype;
    public float CurrentMoney;
}