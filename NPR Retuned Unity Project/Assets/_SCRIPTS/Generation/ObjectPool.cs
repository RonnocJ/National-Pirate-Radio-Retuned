using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private readonly List<GameObject> _activeList = new();
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
        if (_inactivePool.Count == 0)
        {
            AddRandomInstance(variants, parent);
        }

        var obj = _inactivePool.Dequeue();

        obj.SetActive(true);
        _activeList.Add(obj);
        return obj;
    }
    public GameObject Get(GameObject prefab, Transform parent)
    {
       if (_inactivePool.Count == 0)
        {
            AddRandomInstance(prefab, parent);
        }

        var obj = _inactivePool.Dequeue();

        obj.SetActive(true);
        _activeList.Add(obj);
        return obj;
    }
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        if (_activeList.Remove(obj))
        {
            obj.SetActive(false);
            _inactivePool.Enqueue(obj);
        }
    }

    public void SortActiveByDistance(Vector3 referencePosition)
    {
        if (_activeList.Count < 2) return;

        _activeList.Sort((a, b) =>
        {
            float distB = Vector3.SqrMagnitude(b.transform.position - referencePosition);
            float distA = Vector3.SqrMagnitude(a.transform.position - referencePosition);
            return distB.CompareTo(distA);
        });
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
