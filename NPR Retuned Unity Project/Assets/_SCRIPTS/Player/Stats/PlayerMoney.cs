using UnityEngine;

public class PlayerMoney : Singleton<PlayerMoney>
{
    [SerializeField] private float _runMoney = 0f;
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
    void Start()
    {
        VanDamage.root.OnPlayerDie += CalculateEarnings;
    }
    private void CalculateEarnings()
    {
        PlayerStats.root.LastRunMoney = RunMoney;
        PlayerStats.root.CurrentMoney += RunMoney - PlayerStats.root.FCCFine - PlayerStats.root.VanUpkeep;
    }
}