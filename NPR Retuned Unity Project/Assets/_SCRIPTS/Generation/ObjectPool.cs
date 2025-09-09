using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private readonly Stack<GameObject> _inactive = new();
    private int _createdCount = 0;

    public int InactiveCount => _inactive.Count;
    public int CreatedCount => _createdCount;

    public void Prewarm(int count, GameObject[] variants, Transform parent)
    {
        for (int i = 0; i < count; i++)
        {
            AddRandomInstance(variants, parent);
        }
    }
    public void Prewarm(int count, GameObject prefab, Transform parent)
    {
        for (int i = 0; i < count; i++)
        {
            AddRandomInstance(prefab, parent);
        }
    }
    public GameObject Get(GameObject[] variants, Transform parent)
    {
        if (_inactive.Count == 0)
        {
            AddRandomInstance(variants, parent);
        }
        var go = _inactive.Pop();
        if (go == null)
        {
            // If somehow destroyed, recreate
            AddRandomInstance(variants, parent);
            go = _inactive.Pop();
        }
        go.SetActive(true);
        return go;
    }
        public GameObject Get(GameObject prefab, Transform parent)
    {
        if (_inactive.Count == 0)
        {
            AddRandomInstance(prefab, parent);
        }
        var go = _inactive.Pop();
        if (go == null)
        {
            AddRandomInstance(prefab, parent);
            go = _inactive.Pop();
        }
        go.SetActive(true);
        return go;
    }
    public void Return(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        _inactive.Push(go);
    }

    private void AddRandomInstance(GameObject[] variants, Transform parent)
    {
        int idx = Random.Range(0, variants.Length);
        var prefab = variants[idx];

        var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        go.SetActive(false);
        _inactive.Push(go);
        _createdCount++;
    }
    private void AddRandomInstance(GameObject prefab, Transform parent)
    {
        var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        go.SetActive(false);
        _inactive.Push(go);
        _createdCount++;
    }
}

