using System;
using System.Collections.Generic;
using UnityEngine;

public class VanDamage : Singleton<VanDamage>
{
    public Action OnPlayerDie;
    [SerializeField] private float _hype;
    public float Hype
    {
        get => _hype;
        set
        {
            if (value != _hype)
            {
                if (value < _hype) _regenTimer = 0;

                _hype = Mathf.Clamp(value, 0, _p.MaxHype);
                hypeFluid.SetFloat("_FillAmount", value / _p.MaxHype);
                hypeText.SetText($"Hype: \n{Mathf.RoundToInt(value)} / {_p.MaxHype}", 0);

                if (value <= 0) OnPlayerDie?.Invoke();
            }
        }
    }
    [SerializeField] private float _dps;
    public float DPS
    {
        get => _dps;
        set
        {
            if (value != _dps)
            {
                _dps = value;
            }
        }
    }
    [SerializeField] private Material hypeFluid;
    [SerializeField] private GlyphTextRenderer hypeText;
    private float _regenTimer;
    private PlayerStats _p => PlayerStats.root;
    private Queue<Action> _damageQueue = new();
    void Start()
    {
        Hype = _p.MaxHype;
    }
    void Update()
    {
        if (_damageQueue.Count > 0)
        {
            _damageQueue.Dequeue()?.Invoke();
        }

        Hype -= DPS * Time.deltaTime;

        if (Hype < _p.MaxHype)
        {
            if (_regenTimer > _p.RegenDelay)
            {
                Hype += _p.RegenAmount * Time.deltaTime;
                return;
            }

            _regenTimer += Time.deltaTime;
        }
    }
    public void DealDamage(float damageAmount)
    {
        _damageQueue.Enqueue(new Action(() => Hype -= damageAmount));
    }
}