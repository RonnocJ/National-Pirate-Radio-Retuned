using UnityEngine;

public static class DialogueLoader
{
    public static DialogueScript LoadFromResources(string resourcePath)
    {
        var ta = Resources.Load<TextAsset>(resourcePath);
        if (ta == null)
        {
            Debug.LogError($"Dialogue json not found at Resources/{resourcePath}");
            return null;
        }
        var data = JsonUtility.FromJson<DialogueScript>(ta.text);
        if (data == null)
        {
            Debug.LogError($"Failed to parse dialogue json at Resources/{resourcePath}");
        }
        return data;
    }
}

