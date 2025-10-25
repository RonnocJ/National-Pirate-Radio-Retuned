using UnityEngine;
using System;

[DisallowMultipleComponent]
public class ObjectIDManager : MonoBehaviour
{
    [SerializeField] private string uniqueId = Guid.NewGuid().ToString();
    public string ID => uniqueId;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(uniqueId))
            uniqueId = Guid.NewGuid().ToString();
    }
}
