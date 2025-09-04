using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    public int PoolSize;
    public Queue<GameObject> Pool = new();
    public ObjectPool(int newSize, GameObject prefab, Transform targetParent)
    {
        Pool.Clear();

        for (int i = 0; i < newSize; i++)
        {
            GameObject newObj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, targetParent);
            newObj.SetActive(false);
            Pool.Enqueue(newObj);
        }
    }

    public GameObject Retrive(Tile tile)
    {
        GameObject returnObj = Pool.Dequeue();
        returnObj.SetActive(true);
        tile.Object = returnObj;

        Pool.Enqueue(returnObj);
        
        return returnObj;
    }
    public void Return(GameObject obj, Tile tile)
    {
        if (obj == null) return;
        tile.Object = null;
        obj.SetActive(false);
        Pool.Enqueue(obj);
    }
}
