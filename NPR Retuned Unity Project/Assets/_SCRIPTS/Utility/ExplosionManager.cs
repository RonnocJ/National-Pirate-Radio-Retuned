using System.Collections;
using UnityEngine;

public class ExplosionManager : Singleton<ExplosionManager>
{
    [SerializeField] private int explosionPoolSize;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject sparkExplosionPrefab;
    [SerializeField] private Transform explosionParent;
    private ObjectPool _explosionPool;
    private ObjectPool _sparkExplosionPool;
    void Start()
    {
        _explosionPool = new ObjectPool();
        _sparkExplosionPool = new ObjectPool();

        for (int i = 0; i < explosionPoolSize; i++)
        {
            _explosionPool.Prewarm(1, explosionPrefab, explosionParent);
            _sparkExplosionPool.Prewarm(1, sparkExplosionPrefab, explosionParent);
        }
    }

    public void Explode(Vector3 spawnPos)
    {
        var obj = _explosionPool.Get(explosionPrefab, explosionParent);
        obj.transform.position = spawnPos;
        StartCoroutine(ExplodeRoutine(obj, _explosionPool, 0.75f));
    }
    public void SparkExplode(Vector3 spawnPos)
    {
        var obj = _sparkExplosionPool.Get(sparkExplosionPrefab, explosionParent);
        obj.transform.position = spawnPos;
        StartCoroutine(ExplodeRoutine(obj, _sparkExplosionPool, 0.5f));
    }
    private IEnumerator ExplodeRoutine(GameObject explosionFX, ObjectPool explosionPool, float waitTime)
    {
        explosionFX.GetComponent<ParticleSystem>().Play();
        yield return new WaitForSeconds(waitTime);
        explosionPool.Return(explosionFX);
    }
}