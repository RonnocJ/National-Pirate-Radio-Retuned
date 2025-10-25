using UnityEngine;

public class PersistentManagers : Singleton<PersistentManagers>
{
    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this);
    }
}