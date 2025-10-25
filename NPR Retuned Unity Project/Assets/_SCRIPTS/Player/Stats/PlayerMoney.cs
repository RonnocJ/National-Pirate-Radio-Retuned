using UnityEngine;

public class PlayerMoney : Singleton<PlayerMoney>
{
    private float _runMoney;
    public float RunMoney
    {
        get => _runMoney;
        set
        {
            if (value != _runMoney)
            {
                moneyText.SetText($"${Mathf.Round(value * 100) / 100}");

                PlayerStats.root.CurrentMoney += value - _runMoney;
            }

            _runMoney = value;
        }
    }
    [SerializeField] private GlyphTextRenderer moneyText;
}