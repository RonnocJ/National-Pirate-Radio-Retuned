using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Objects/Audio/EventBankDictionary", order = 1)]
public class UWBankDictionaries : ScriptableObject
{
    public List<EventEntry> serializedEvents = new();
    public List<RTPCEntry> serializedRTPCs = new();

    private Dictionary<string, (string, uint)> eventBankDict = new();

    public Dictionary<string, (string bankName, uint id)> EventBankDict
    {
        get
        {
            if (eventBankDict.Count == 0) 
            {
                foreach (var entry in serializedEvents)
                {
                    eventBankDict[entry.eventName] = (entry.bankName, entry.eventId);
                }
            }
            return eventBankDict;
        }
    }

    private Dictionary<string, (float, float)> rtpcBankDict = new();

    public Dictionary<string, (float min, float max)> RTPCBankDict
    {
        get
        {
            if (rtpcBankDict.Count == 0)
            {
                foreach (var entry in serializedRTPCs)
                {
                    rtpcBankDict[entry.Name] = (entry.Min, entry.Max);
                }
            }
            return rtpcBankDict;
        }
    }
}

[Serializable]
public class EventEntry
{
    public string eventName;
    public string bankName;
    public uint eventId;

    public EventEntry(string newEvent, string newBank, uint newId)
    {
        eventName = newEvent;
        bankName = newBank;
        eventId = newId;
    }
}
[Serializable]
public class RTPCEntry
{
    public string Name;
    public float Min;
    public float Max;

    public RTPCEntry(string newRTPC, float newMin, float newMax)
    {
        Name = newRTPC;
        Min = newMin;
        Max = newMax;
    }
}