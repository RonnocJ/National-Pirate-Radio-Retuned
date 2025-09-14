using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Objects/Player/PlayerStats", order = 0)]
public class PlayerStats : ScriptableSingleton<PlayerStats>
{
    public float MaxHype;
    public float RegenAmount;
    public float RegenDelay;
}