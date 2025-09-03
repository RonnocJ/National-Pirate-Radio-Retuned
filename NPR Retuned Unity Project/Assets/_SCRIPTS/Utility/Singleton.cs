using UnityEditor;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Component
{
    private static T _root;
    public static T root
    {
        get
        {
            if (_root == null)
            {

                _root = FindFirstObjectByType<T>();
                if (_root == null)
                {
                    GameObject singletonObject = new GameObject(typeof(T).Name);
                    _root = singletonObject.AddComponent<T>();
                    Debug.LogError($"No Singleton instance of {_root} found, creating new game object");
                }
            }
            return _root;
        }
    }

    protected virtual void Awake()
    {
        if (_root == null)
        {
            _root = this as T;
        }
        else if (_root != this)
        {
            Destroy(gameObject);
        }
    }
}

public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
{
    private static T _root;
    public static T root
    {
        get
        {
            if (_root == null)
            {
                // Try direct load by type name (expects asset at Resources/TypeName.asset)
                _root = Resources.Load<T>(typeof(T).Name);

                // Fallback: search anywhere under Resources for the first matching asset
                if (_root == null)
                {
                    var all = Resources.LoadAll<T>(string.Empty);
                    if (all != null && all.Length > 0)
                    {
                        // Prefer exact name match if present, otherwise first found
                        foreach (var candidate in all)
                        {
                            if (candidate != null && candidate.name == typeof(T).Name)
                            {
                                _root = candidate;
                                break;
                            }
                        }
                        if (_root == null)
                        {
                            _root = all[0];
                        }
                    }
                }
            }
            return _root;
        }
    }

    protected virtual void Awake()
    {
        if (_root == null)
        {
            _root = this as T;
        }
        else if (_root != this)
        {
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(this));
#endif
        }
    }
}
