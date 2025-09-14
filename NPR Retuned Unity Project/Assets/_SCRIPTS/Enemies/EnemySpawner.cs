using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public int Alive;
    public ObjectPool EnemyPool;
    [SerializeField, Range(0, 100)] private int spawnChance;
    [SerializeField] private float spawnTime;
    [SerializeField] private int spawnMax;
    [SerializeField] private float spawnRange;
    [SerializeField] private float spawnHeight;
    [SerializeField] private LayerMask spawnSurfaces;
    [SerializeField] private int poolSize;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemyParent;
    private int success;
    IEnumerator Start()
    {
        EnemyPool = new ObjectPool();


        while (true)
        {
            yield return new WaitForSeconds(spawnTime);

            if (Alive > poolSize) continue;
            
            success = 0;

            for (int i = 0; i < spawnMax * 4; i++)
            {
                if (Random.value * 100 > spawnChance) continue;

                transform.position = VanController.root.transform.position + Random.insideUnitSphere * spawnRange;

                transform.position = new Vector3(transform.position.x, 256, transform.position.z);

                Ray ray = new Ray(transform.position, Vector3.down);

                if (Physics.Raycast(ray, out RaycastHit hit, 512, spawnSurfaces))
                {
                    transform.position = new Vector3(transform.position.x, hit.point.y + spawnHeight, transform.position.z);
                }

                if (Vector3.Distance(transform.position, VanController.root.transform.position) < 256) continue;

                if (!PfGraph.root.IsNavigable(transform.position)) continue;

                if (EnemyPool.CreatedCount < poolSize) EnemyPool.Prewarm(1, enemyPrefab, enemyParent);

                var obj = EnemyPool.Get(enemyPrefab, enemyParent);
                obj.transform.position = transform.position;

                var e = obj.GetComponent<Enemy>();
                e.spawner = this;
                e.Spawn();

                Alive++;
                success++;

                if (success == spawnMax) break;

                yield return null;
            }
        }
    }
}