using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR 
public static class UWAudioEnumGenerator {
    public enum EnumCodeSectionType {
        Event,
        State,
        Switch,
        Trigger,
        RTPC,
        Soundbank
    }

    public struct EnumCodeSection {
        public EnumCodeSectionType type;
        public List<string> values;
        public List<int> ids;

        public EnumCodeSection(EnumCodeSectionType type, List<string> values, List<int> ids) {
            this.type = type;
            this.values = values;
            this.ids = ids;
        }
    }
    private static UWBankDictionaries _bankObj;

    [MenuItem("Utilities/Recreate Audio Enum")]
    public static void GenerateEnum() {
        if (_bankObj == null) {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(UWBankDictionaries)}");

            if (guids.Length > 0) {
                _bankObj = AssetDatabase.LoadAssetAtPath<UWBankDictionaries>(AssetDatabase.GUIDToAssetPath(guids[0]));
            } else {
                Debug.LogError("Please create a EventBankDictionary Scriptable Object!");
                return;
            }
        }

        _bankObj.serializedEvents.Clear();

        string jsonData = File.ReadAllText("Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/SoundbanksInfo.json");
        var enumCodeSections = ExtractEventNamesFromJson(jsonData);

        string fileContents = GenerateFileTextForCodeSections(enumCodeSections);

        WriteEnumToFile(fileContents);

        // This ensures unity editor sees / doesn't see errors correctly after a "reloading domain" thing
        EditorUtility.SetDirty(_bankObj);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Recreated Audio Enum");
    }

    // Method to extract event names from JSON
    static List<EnumCodeSection> ExtractEventNamesFromJson(string jsonData) {
        bool hasGeneratedSwitches = false;
        // Deserialize the JSON into a dictionary
        var soundBanksInfo = JsonConvert.DeserializeObject<SoundBanksInfoRoot>(jsonData);

        // Create a list to store event names
        var eventNames = new List<string>();
        var eventIds = new List<int>();
        var stateNames = new List<string>();
        var stateIds = new List<int>();
        var switchNames = new List<string>();
        var switchIds = new List<int>();
        var triggerNames = new List<string>();
        var triggerIds = new List<int>();
        var parameterNames = new List<string>();
        var parameterIds = new List<int>();
        var soundbankNames = new List<string>();
        var soundbankIds = new List<int>();

        _bankObj.serializedEvents.Clear();

        // Iterate over the soundbanks and extract event names
        foreach (var soundBank in soundBanksInfo.SoundBanksInfo.SoundBanks) {
            if (soundBank.Events != null) {
                foreach (var soundEvent in soundBank.Events) {
                    if (!eventNames.Contains(soundEvent.Name)) {
                        eventNames.Add(soundEvent.Name);

                        _bankObj.serializedEvents.Add(new EventEntry(soundEvent.Name, soundBank.ShortName, uint.Parse(soundEvent.Id)));

                        try {
                            eventIds.Add(unchecked((int)uint.Parse(soundEvent.Id)));
                        } catch (FormatException e) {
                            Debug.LogError(e.Message);
                        }
                    }
                }
            }


            if (soundBank.StateGroups != null) {
                for (int i = 0; i < soundBank.StateGroups.Count; i++) {
                    for (int j = 0; j < soundBank.StateGroups[i].States.Count; j++) {
                        if (!stateNames.Contains(soundBank.StateGroups[i].Name + "_BREAK_" + soundBank.StateGroups[i].States[j].Name)) {
                            stateNames.Add(soundBank.StateGroups[i].Name + "_BREAK_" + soundBank.StateGroups[i].States[j].Name);

                            try {
                                int newId = unchecked((int)uint.Parse(soundBank.StateGroups[i].States[j].Id));

                                while (stateIds.Contains(newId)) {
                                    newId++;
                                }

                                stateIds.Add(newId);
                            } catch (FormatException e) {
                                Debug.LogError(e.Message);
                            }
                        }
                    }
                }
            }

            if (soundBank.SwitchGroups != null && !hasGeneratedSwitches) {
                for (int i = 0; i < soundBank.SwitchGroups.Count; i++) {
                    for (int j = 0; j < soundBank.SwitchGroups[i].Switches.Count; j++) {
                        if (!switchNames.Contains(soundBank.SwitchGroups[i].Switches[j].Name)) {
                            switchNames.Add(soundBank.SwitchGroups[i].Name + "_BREAK_" + soundBank.SwitchGroups[i].Switches[j].Name);

                            try {
                                switchIds.Add(unchecked((int)uint.Parse(soundBank.SwitchGroups[i].Switches[j].Id)));
                            } catch (FormatException e) {
                                Debug.LogError(e.Message);
                            }
                        }
                    }
                }

                hasGeneratedSwitches = true;
            }

            if (soundBank.Triggers != null) {
                foreach (var soundTrigger in soundBank.Triggers) {
                    if (!triggerNames.Contains(soundTrigger.Name)) {
                        triggerNames.Add(soundTrigger.Name);

                        try {
                            triggerIds.Add(unchecked((int)uint.Parse(soundTrigger.Id)));
                        } catch (FormatException e) {
                            Debug.LogError(e.Message);
                        }
                    }

                }
            }

            if (soundBank.GameParameters != null) {
                foreach (var soundParameter in soundBank.GameParameters) {
                    if (!parameterNames.Contains(soundParameter.Name)) {
                        try {
                            parameterIds.Add(unchecked((int)uint.Parse(soundParameter.Id)));
                        } catch (FormatException e) {
                            Debug.LogError(e.Message);
                        }

                        parameterNames.Add(soundParameter.Name);
                    }
                }
            }

            soundbankNames.Add(soundBank.ShortName);
            soundbankIds.Add(unchecked((int)uint.Parse(soundBank.Id)));
        }

        _bankObj.serializedRTPCs.Clear();

        var wwiseProjectData = AkWwiseProjectInfo.GetData();
        foreach (var rtpcWwu in wwiseProjectData.RtpcWwu) {
            foreach (var rtpc in rtpcWwu.List) {
                _bankObj.serializedRTPCs.Add(new RTPCEntry(rtpc.Name, (float)rtpc.Min, (float)rtpc.Max));
            }
        }

        return new List<EnumCodeSection>() {
            new EnumCodeSection(EnumCodeSectionType.Event, eventNames, eventIds),
            new EnumCodeSection(EnumCodeSectionType.State, stateNames, stateIds),
            new EnumCodeSection(EnumCodeSectionType.Switch, switchNames, switchIds),
            new EnumCodeSection(EnumCodeSectionType.Trigger, triggerNames, triggerIds),
            new EnumCodeSection(EnumCodeSectionType.RTPC, parameterNames, parameterIds),
                        new EnumCodeSection(EnumCodeSectionType.Soundbank, soundbankNames, soundbankIds)
        };
    }

    // Method to generate C# enum code
    static string GenerateFileTextForCodeSections(List<EnumCodeSection> enumCodeSections) {
        string enumCode = "";

        foreach (var section in enumCodeSections) {
            enumCode += generateCodeForSection(section);
        }

        return enumCode;
    }

    static string generateCodeForSection(EnumCodeSection section) {
        var enumCode = @"

/// <summary>
///   The list of " + (section.type.ToString().Contains("Switch") ? "switches" : section.type.ToString().ToLower() + "s") + @" in the game.
/// </summary>
public enum Audio" + section.type.ToString() + @" {
";
        if (section.type != EnumCodeSectionType.Soundbank) enumCode += $"    None = 0,\n";

        for (int k = 0; k < section.values.Count; k++) {
            // Add each parameter name as an enum value
            enumCode += $"    {section.values[k]} = {section.ids[k]},\n";
        }

        enumCode += "}";

        return enumCode;
    }

    // Method to write the generated enum code to a .cs file
    static void WriteEnumToFile(string enumCode) {
        var directory = Application.dataPath + "/UpgradedWwise/_generated/";
        var filename = "AudioEnum.cs";
        var fullPath = directory + filename;
        try {
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            // Write the enum code to the file
            File.WriteAllText(fullPath, enumCode);
        } catch (Exception ex) {
            Debug.LogError($"Error writing to file: {ex.Message}");
        }

    }

    // Classes representing the structure of the JSON
    [Serializable]
    public class SoundBanksInfoRoot {
        public SoundBanksInfo SoundBanksInfo { get; set; }
    }

    [Serializable]
    public class SoundBanksInfo {
        public List<SoundBank> SoundBanks { get; set; }
    }

    [Serializable]
    public class SoundBank {
        public string Id { get; set; }
        public string ShortName { get; set; }
        public List<SoundEvent> Events { get; set; }
        public List<MusicStateGroup> StateGroups { get; set; }
        public List<MusicSwitchGroup> SwitchGroups { get; set; }
        public List<SoundTrigger> Triggers { get; set; }
        public List<SoundParameter> GameParameters { get; set; }
    }

    [Serializable]
    public class SoundEvent {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    [Serializable]
    public class MusicStateGroup {
        public string Name;
        public List<MusicState> States { get; set; }
    }

    [Serializable]
    public class MusicState {
        public string Id { get; set; }
        public string Name { get; set; }
    }
    [Serializable]
    public class MusicSwitchGroup {
        public string Name;
        public List<MusicSwitch> Switches { get; set; }
    }
    [Serializable]
    public class MusicSwitch {
        public string Id { get; set; }
        public string Name { get; set; }
    }
    [Serializable]
    public class SoundTrigger {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    [Serializable]
    public class SoundParameter {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
#endif