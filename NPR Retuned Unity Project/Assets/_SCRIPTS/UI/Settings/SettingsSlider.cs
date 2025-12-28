using System;
using System.Collections.Generic;
using UnityEngine;

public class SettingsSlider : SettingsEntry, ISaveData
{
    [SerializeField] private float maxY;
    public enum SettingsSliderType
    {
        MasterVolume,
        MusicVolume,
        SFXVolume,
    }
    [SerializeField] private SettingsSliderType SliderType;
    private float _currentValue;
    private Vector2 _moveInput => PInputManager.root.actions[PlayerActionType.Drive].v2Value;
    public Dictionary<string, object> AddSaveData()
    {
        return new Dictionary<string, object>()
        {
            {SliderType.ToString(), _currentValue}
        };
    }
    public void ReadSaveData(Dictionary<string, object> dataDict)
    {
        if (dataDict.TryGetValue(SliderType.ToString(), out object sliderVal))
        {
            _currentValue = Convert.ToSingle(sliderVal);
            transform.localPosition = new Vector3(transform.localPosition.x, _currentValue, transform.localPosition.z);
            AudioManager.root.SetRTPC(AudioRTPC.Main_Volume, (_currentValue / maxY * 0.5f) + 0.5f);
        }
    }
    protected override void Update()
    {
        base.Update();

        if (Highlighted && GameManager.root.Paused)
        {
            _currentValue = Mathf.Clamp(_currentValue + _moveInput.y * Time.unscaledDeltaTime * 2f, -maxY, maxY);
            transform.localPosition = new Vector3(transform.localPosition.x, _currentValue, transform.localPosition.z);

            float sliderVal = (_currentValue / maxY * 0.5f) + 0.5f;

            switch (SliderType)
            {
                case SettingsSliderType.MasterVolume:
                    AudioManager.root.SetRTPC(AudioRTPC.Main_Volume, sliderVal);
                    break;
                case SettingsSliderType.MusicVolume:
                    AudioManager.root.SetRTPC(AudioRTPC.Music_Volume, sliderVal);
                    break;
                case SettingsSliderType.SFXVolume:
                    AudioManager.root.SetRTPC(AudioRTPC.SFX_Volume, sliderVal);
                    break;
            }
        }
    }
}
