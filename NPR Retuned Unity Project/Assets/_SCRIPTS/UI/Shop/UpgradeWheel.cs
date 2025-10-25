using UnityEngine;
public enum StatType
{
    VehicleSpeed,
    WeaponDamage,
    AbilityStrength,
    RegenSpeed,
    MaxHype
}
public class UpgradeWheel : MonoBehaviour
{
    [SerializeField] private int _upgradeLevel;
    public int UpgradeLevel
    {
        get => _upgradeLevel;
        set
        {
            if (value > _upgradeLevel)
            {
                value = Mathf.Min(value, upgradeMax);
                if (value <= _upgradeLevel) return;
            }
            else
            {
                value = Mathf.Clamp(value, 0, upgradeMax);
            }

            if (value != _upgradeLevel)
            {
                var newMat = new MaterialPropertyBlock();
                newMat.SetFloat("_Fill", (float)value / upgradeMax);

                GetComponent<MeshRenderer>().SetPropertyBlock(newMat);

                levelText.SetText(value.ToString());

                _upgradeLevel = value;

                switch (stat)
                {
                    case StatType.VehicleSpeed:
                        PlayerStats.root.VehicleSpeed = statScaling.Evaluate(value);
                        break;
                    case StatType.WeaponDamage:
                        PlayerStats.root.WeaponDamage = statScaling.Evaluate(value);
                        break;
                    case StatType.AbilityStrength:
                        PlayerStats.root.AbilityStrength = statScaling.Evaluate(value);
                        break;
                    case StatType.RegenSpeed:
                        PlayerStats.root.RegenAmount = statScaling.Evaluate(value);
                        break;
                        case StatType.MaxHype:
                        PlayerStats.root.MaxHype = statScaling.Evaluate(value);
                        break;
                }
            }
        }
    }
    [SerializeField] private int upgradeMax;
    [SerializeField] private StatType stat;
    [SerializeField] private AnimationCurve statScaling;
    [SerializeField] private Material fillMat;
    [SerializeField] private GlyphTextRenderer levelText;
}
