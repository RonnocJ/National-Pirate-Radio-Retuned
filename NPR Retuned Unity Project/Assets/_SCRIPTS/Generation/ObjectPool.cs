using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private readonly Queue<GameObject> _pool = new();
    private int _createdCount = 0;

    public int PoolCount => _pool.Count;
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
        if (_pool.Count == 0)
        {
            AddRandomInstance(variants, parent);
        }
        var obj = _pool.Dequeue();
        obj.SetActive(true);
        return obj;
    }
    public GameObject Get(GameObject prefab, Transform parent)
    {
        if (_pool.Count == 0)
        {
            AddRandomInstance(prefab, parent);
        }
        var obj = _pool.Dequeue();
        obj.SetActive(true);
        return obj;
    }
    public void Return(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        _pool.Enqueue(obj);
    }

    private void AddRandomInstance(GameObject[] variants, Transform parent)
    {
        int idx = Random.Range(0, variants.Length);
        var prefab = variants[idx];

        var obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        obj.SetActive(false);
        _pool.Enqueue(obj);
        _createdCount++;
    }
    private void AddRandomInstance(GameObject prefab, Transform parent)
    {
        var obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        obj.SetActive(false);
        _pool.Enqueue(obj);
        _createdCount++;
    }
}
