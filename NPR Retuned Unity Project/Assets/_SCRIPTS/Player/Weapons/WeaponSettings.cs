using UnityEngine;
[CreateAssetMenu(fileName = "WeaponSettings", menuName = "Objects/Weapons/WeaponSettings", order = 1)]
public class WeaponSettings : ScriptableSingleton<WeaponSettings>
{
    public LayerMask LayerInclusions;
    [System.NonSerialized]
    public VanWeapon currentWeapon;
}
