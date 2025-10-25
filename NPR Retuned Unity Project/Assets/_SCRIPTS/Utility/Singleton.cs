using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Linq;

public class Singleton<T> : MonoBehaviour where T : Component
{
    private static T _root;
    public static T root
    {
        get
        {
            if (_root == null)
            {
#if UNITY_EDITOR
                var all = Resources.FindObjectsOfTypeAll<T>();
                _root = all.FirstOrDefault(o => !EditorUtility.IsPersistent(o) && (o.hideFlags & HideFlags.HideAndDontSave) == 0);
#else
                _root = FindObjectOfType<T>();
#endif
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
            HandleDuplicateInstance();
        }
    }

    protected virtual void OnEnable()
    {
        if (_root == null)
        {
            _root = this as T;
        }
        else if (_root != this)
        {
            HandleDuplicateInstance();
        }
    }

    protected virtual void OnDestroy()
    {
        if (_root == this)
            _root = null;
    }

    private void HandleDuplicateInstance()
    {

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(gameObject);
            return;
        }
#endif

        Destroy(gameObject);
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
