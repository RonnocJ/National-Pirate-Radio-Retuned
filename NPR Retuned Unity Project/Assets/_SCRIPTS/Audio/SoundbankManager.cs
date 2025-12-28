using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SoundbankManager : Singleton<SoundbankManager>
{
    private readonly Dictionary<AudioSoundbank, int> _bankRefCounts = new();

    protected override void OnEnable()
    {
        base.OnEnable();

        LoadSoundbank(AudioSoundbank.Global);
    }
    public void LoadSoundbank(AudioSoundbank bank)
    {
        if (_bankRefCounts.TryGetValue(bank, out var count))
        {
            _bankRefCounts[bank] = count + 1;
            return;
        }

        _bankRefCounts[bank] = 1;
        AkBankManager.LoadBank(bank.ToString(), decodeBank: false, saveDecodedBank: false);
    }
    public void UnloadSoundbank(AudioSoundbank bank)
    {
        if (!_bankRefCounts.TryGetValue(bank, out var count))
        {
            Debug.LogWarning($"Attempted to unload untracked Soundbank: {bank}");
            return;
        }

        if (bank == AudioSoundbank.Global)
        {
            Debug.LogWarning($"Attempted to unload persistent Soundbank: {bank}");
            return;
        }

        if (count > 1)
        {
            _bankRefCounts[bank] = count - 1;
            return;
        }

        _bankRefCounts.Remove(bank);
        AkBankManager.UnloadBank(bank.ToString());
    }
    public void UnloadAll()
    {
        var banksToUnload = _bankRefCounts.Keys
            .Where(bank => bank != AudioSoundbank.Global)
            .ToList();

        foreach (var bank in banksToUnload)
        {
            while (_bankRefCounts.TryGetValue(bank, out var count) && count > 0)
            {
                UnloadSoundbank(bank);
            }
        }
    }
}
