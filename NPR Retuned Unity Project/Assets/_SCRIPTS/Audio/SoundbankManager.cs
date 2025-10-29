using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SoundbankManager : Singleton<SoundbankManager>
{
    private HashSet<AudioSoundbank> _loadedBanks = new();
    protected override void Awake()
    {
        base.Awake();

        LoadSoundbank(AudioSoundbank.Global);
    }
    public void LoadSoundbank(AudioSoundbank bank)
    {
        if(_loadedBanks.Add(bank)) AkUnitySoundEngine.LoadBank(bank.ToString(), out _);
    }
    public void UnloadSoundbank(AudioSoundbank bank)
    {
        if(_loadedBanks.Remove(bank)) AkUnitySoundEngine.UnloadBank(bank.ToString(), IntPtr.Zero);
    }
    public void UnloadAll()
    {
        AkUnitySoundEngine.ClearBanks();
        AkUnitySoundEngine.LoadBank(AudioSoundbank.Init.ToString(), out _);

        _loadedBanks.Clear();
    }
}