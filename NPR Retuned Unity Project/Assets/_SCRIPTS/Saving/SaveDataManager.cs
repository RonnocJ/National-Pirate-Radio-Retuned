using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;

[Serializable]
public class SaveDataProfile
{
    public string FilePath;
    public string FileName;
    public Dictionary<string, object> Data;
    public SaveDataProfile(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;
        Data = new();
    }
    public void ReadAllData(MonoBehaviour[] allBehaviours)
    {
        Data = new();

        string fullPath = ResolveFullPath();

        if (File.Exists(fullPath))
        {
            try
            {
                string loadedData = File.ReadAllText(fullPath);
                Data = JsonConvert.DeserializeObject<Dictionary<string, object>>(loadedData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error trying to load save data from path {fullPath} \n {e}");
            }
        }

        int i = 0;

        foreach (var b in allBehaviours)
        {
            if (b is ISaveData saveData)
            {
                i++;

                if (Data.ContainsKey(GetStableID(b)) && Data[GetStableID(b)] is Newtonsoft.Json.Linq.JObject jObject)
                {
                    saveData.ReadSaveData(jObject.ToObject<Dictionary<string, object>>());
                }

                else
                    saveData.ReadSaveData(new Dictionary<string, object>());
            }
        }
    }
    public void WriteAllData(MonoBehaviour[] allBehaviours)
    {
        Data = new();

        int i = 0;

        string fullPath = ResolveFullPath();

        foreach (var b in allBehaviours)
        {
            if (b is ISaveData saveData)
            {
                i++;

                if (saveData.AddSaveData() != null)
                    Data[GetStableID(b)] = saveData.AddSaveData();
            }
        }

        try
        {
            if (!string.IsNullOrEmpty(FilePath))
            {
                Directory.CreateDirectory(FilePath);
            }

            string dataToStore = JsonConvert.SerializeObject(Data, Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(dataToStore);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not save file to {fullPath} \n {e}");
        }
    }
    public void ResetData()
    {
        string fullPath = ResolveFullPath();

        try
        {
            string dataToStore = JsonConvert.SerializeObject(new Dictionary<string, object>(), Formatting.Indented);

            if (!string.IsNullOrEmpty(FilePath))
            {
                Directory.CreateDirectory(FilePath);
            }

            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(dataToStore);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not reset save data at {fullPath} \n {e}");
        }
    }

    private string ResolveFullPath()
    {
        string directory = string.IsNullOrWhiteSpace(FilePath)
            ? Application.persistentDataPath
            : FilePath.Trim();

        string validFileName = EnsureValidFileName(FileName);

        FilePath = directory;
        FileName = validFileName;

        return Path.Combine(directory, validFileName);
    }

    private static string EnsureValidFileName(string fileName)
    {
        const string defaultFileName = "SaveData.json";

        if (string.IsNullOrWhiteSpace(fileName))
            return defaultFileName;

        fileName = fileName.Trim();

        char[] invalidChars = Path.GetInvalidFileNameChars();

        if (fileName.IndexOfAny(invalidChars) >= 0)
        {
            fileName = new string(fileName.Select(c => Array.IndexOf(invalidChars, c) >= 0 ? '_' : c).ToArray());
        }

        if (!Path.HasExtension(fileName))
        {
            fileName += ".json";
        }

        return fileName;
    }

    private string GetStableID(MonoBehaviour behaviour)
    {
        var idHolder = behaviour.GetComponent<ObjectIDManager>();
        if (idHolder == null)
        {
#if UNITY_EDITOR
            idHolder = Undo.AddComponent<ObjectIDManager>(behaviour.gameObject);
            EditorUtility.SetDirty(behaviour.gameObject);
#endif
        }

        return idHolder?.ID;
    }
}
public class SaveDataManager : Singleton<SaveDataManager>
{
    [SerializeField] private string _fileName;
    public SaveDataProfile MainProfile
    {
        get => new SaveDataProfile(Application.persistentDataPath, _fileName);
    }
    public SaveDataProfile ProfileToLoad;
    public Action<Dictionary<string, object>> LoadNewData;
    protected override void OnEnable()
    {
        base.OnEnable();

        if (ProfileToLoad == null || ProfileToLoad.FileName == "" || ProfileToLoad.FilePath == "") ProfileToLoad = MainProfile;
        ProfileToLoad.ReadAllData(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None));
    }
    void OnApplicationQuit()
    {
        if (TestOverrides.root.saveProgress) MainProfile.WriteAllData(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None));
    }
}
#if UNITY_EDITOR

[InitializeOnLoad]
public static class SaveDataIDAutoAssigner
{
    static SaveDataIDAutoAssigner()
    {
        EditorApplication.delayCall += AssignUniqueIDsToAllSaveData;
    }

    private static void AssignUniqueIDsToAllSaveData()
    {
        ISaveData[] saveDataComponents = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveData>()
            .ToArray();

        foreach (var saveData in saveDataComponents)
        {
            var behaviour = (MonoBehaviour)saveData;
            if (behaviour.GetComponent<ObjectIDManager>() == null)
            {
                Undo.AddComponent<ObjectIDManager>(behaviour.gameObject);
                EditorUtility.SetDirty(behaviour.gameObject);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
