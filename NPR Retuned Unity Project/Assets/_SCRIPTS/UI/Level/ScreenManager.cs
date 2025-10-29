using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenManager : Singleton<ScreenManager>
{
    private const string ModeSwitchEffectKey = "MODE_SWITCH";
    private const string AtvEffectKey = "ATV_HIT";
    [SerializeField] private float baseIntensity;
    [SerializeField] private float flareIntensity;
    [SerializeField] private int switchStaticFrames = 12;
    [SerializeField] private float atvFlareIntensity = 0f;
    [SerializeField] private float atvFadeInTime = 0.1f;
    [SerializeField] private float atvFadeOutTime = 0.6f;
    [SerializeField] private GlyphTextRenderer text;
    [SerializeField] private Material levelDisplayMat;
    private bool firstSwitch = true;
    private readonly Dictionary<string, float> _effectIntensities = new();
    private Coroutine _modeSwitchRoutine;
    private Coroutine _atvRoutine;

    void Start()
    {
        levelDisplayMat.SetFloat("_NoiseIntensity", baseIntensity);
        GameManager.root.OnPStateSwitch += SwitchText;
    }

    void SwitchText(PlayerState newState)
    {
        if (!firstSwitch && newState == PlayerState.Utility)
        {
            text.SetText("Cam 1:\nUtility Mode", 0);
            PlayModeSwitchStatic();
        }
        else if (newState == PlayerState.Weapon)
        {
            text.SetText("Cam 2:\nWeapons Mode", 0);
            PlayModeSwitchStatic();
        }
        else
        {
            firstSwitch = false;
        }
    }

    public void PlayAtvStatic()
    {
        if (!isActiveAndEnabled) return;

        if (_atvRoutine != null)
        {
            StopCoroutine(_atvRoutine);
        }

        _atvRoutine = StartCoroutine(AtvStaticRoutine());
    }

    private void PlayModeSwitchStatic()
    {
        if (_modeSwitchRoutine != null)
        {
            StopCoroutine(_modeSwitchRoutine);
        }

        _modeSwitchRoutine = StartCoroutine(ModeSwitchStaticRoutine());
    }

    IEnumerator ModeSwitchStaticRoutine()
    {
        int frames = Mathf.Max(1, switchStaticFrames);

        for (int i = 0; i <= frames; i++)
        {
            float half = frames / 2f;
            float triangle = Mathf.Max(0f, -Mathf.Abs(i - half) + half);
            float intensity = triangle * flareIntensity;
            SetEffectIntensity(ModeSwitchEffectKey, intensity);
            yield return null;
        }

        SetEffectIntensity(ModeSwitchEffectKey, 0f);
        _modeSwitchRoutine = null;
    }

    IEnumerator AtvStaticRoutine()
    {

        float elapsed = 0f;
        while (elapsed < atvFadeInTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / atvFadeInTime);
            SetEffectIntensity(AtvEffectKey, Mathf.Lerp(0f, Mathf.Max(switchStaticFrames / 2f * flareIntensity, atvFlareIntensity), t));
            yield return null;
        }

        SetEffectIntensity(AtvEffectKey, Mathf.Max(switchStaticFrames / 2f * flareIntensity, atvFlareIntensity));

        if (atvFadeOutTime > 0f)
        {
            float elapsedOut = 0f;
            while (elapsedOut < atvFadeOutTime)
            {
                elapsedOut += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedOut / atvFadeOutTime);
                SetEffectIntensity(AtvEffectKey, Mathf.Lerp(Mathf.Max(switchStaticFrames / 2f * flareIntensity, atvFlareIntensity), 0f, t));
                yield return null;
            }
        }

        SetEffectIntensity(AtvEffectKey, 0f);
        _atvRoutine = null;
    }

    private void SetEffectIntensity(string key, float intensity)
    {
        float clamped = Mathf.Max(0f, intensity);

        if (clamped <= 0f)
        {
            _effectIntensities.Remove(key);
        }
        else
        {
            _effectIntensities[key] = clamped;
        }

        float highest = 0f;
        foreach (float value in _effectIntensities.Values)
        {
            if (value > highest) highest = value;
        }

        levelDisplayMat.SetFloat("_NoiseIntensity", baseIntensity + highest);
    }
}
