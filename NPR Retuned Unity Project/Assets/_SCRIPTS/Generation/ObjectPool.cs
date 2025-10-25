using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ObjectPool
{
    private readonly Queue<GameObject> _activePool = new();
    private readonly Queue<GameObject> _inactivePool = new();
    private int _createdCount = 0;

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
        if (_activePool.Count == 0)
        {
            AddRandomInstance(variants, parent);
        }

        GameObject obj;

        if (_inactivePool.Count > 0)
        {
            obj = _inactivePool.Dequeue();
        }
        else
        {
            obj = _activePool.Dequeue();
        }
        
        obj.SetActive(true);
        _activePool.Enqueue(obj);
        return obj;
    }
    public GameObject Get(GameObject prefab, Transform parent)
    {
       if (_activePool.Count == 0)
        {
            AddRandomInstance(prefab, parent);
        }

        GameObject obj;

        if (_inactivePool.Count > 0)
        {
            obj = _inactivePool.Dequeue();
        }
        else
        {
            obj = _activePool.Dequeue();
        }
        
        obj.SetActive(true);
        _activePool.Enqueue(obj);
        return obj;
    }
    public void Return(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        _inactivePool.Enqueue(obj);
    }

    public void SortActiveByDistance(Vector3 referencePosition)
    {
        if (_activePool.Count < 2) return;

        var ordered = new List<GameObject>(_activePool.Count);

        while (_activePool.Count > 0)
        {
            GameObject obj = _activePool.Dequeue();
            if (obj != null)
            {
                ordered.Add(obj);
            }
        }

        ordered.Sort((a, b) =>
        {
            float distB = Vector3.SqrMagnitude(b.transform.position - referencePosition);
            float distA = Vector3.SqrMagnitude(a.transform.position - referencePosition);
            return distB.CompareTo(distA);
        });

        for (int i = 0; i < ordered.Count; i++)
        {
            _activePool.Enqueue(ordered[i]);
        }
    }

    private void AddRandomInstance(GameObject[] variants, Transform parent)
    {
        int idx = Random.Range(0, variants.Length);
        var prefab = variants[idx];

        var obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        obj.SetActive(false);
        _inactivePool.Enqueue(obj);
        _createdCount++;
    }
    private void AddRandomInstance(GameObject prefab, Transform parent)
    {
        var obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        obj.SetActive(false);
        _inactivePool.Enqueue(obj);
        _createdCount++;
    }
}
