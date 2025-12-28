using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

                if (!TestOverrides.root.immortal || value > _hype) _hype = Mathf.Clamp(value, 0, _p.MaxHype);

                hypeFluid.SetFloat("_Fill", _hype / _p.MaxHype);
                hypeText.SetText($"Hype: \n{Mathf.RoundToInt(_hype)} / {_p.MaxHype}", 0);

                if (_hype / _p.MaxHype < 0.5f)
                {
                    _vignetteComp.intensity.value = screenFxCurve.Evaluate(_hype / _p.MaxHype * 2f) * 0.75f;
                    _colorComp.saturation.value = screenFxCurve.Evaluate(_hype / _p.MaxHype * 2f) * -33f;
                    _colorComp.contrast.value = screenFxCurve.Evaluate(_hype / _p.MaxHype * 2f) * 20f;
                }

                if (value <= 0 && GameManager.root.CurrentPState != PlayerState.Dead)
                {
                    OnPlayerDie?.Invoke();

                    AudioManager.root.PlaySound(AudioEvent.stopAll);

                    GameManager.root.CurrentPState = PlayerState.Dead;
                    StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Debt));
                }
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
    [SerializeField] private AnimationCurve screenFxCurve;
    [SerializeField] private VolumeProfile globalProfile;

    private float _regenTimer;
        private Vignette _vignetteComp;
    private ColorAdjustments _colorComp;
    private PlayerStats _p => PlayerStats.root;
    
    private Queue<Action> _damageQueue = new();
    void Start()
    {
        _vignetteComp = globalProfile.components.Find(c => c is Vignette) as Vignette;
        _colorComp = globalProfile.components.Find(c => c is ColorAdjustments) as ColorAdjustments;

        _vignetteComp.intensity.value = 0f;
        _colorComp.saturation.value = 0f;
        _colorComp.contrast.value = 0f;

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