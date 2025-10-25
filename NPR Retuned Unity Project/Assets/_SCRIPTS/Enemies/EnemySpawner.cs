using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public int Alive;
    public int PoolSize;
    public ObjectPool EnemyPool;
    [SerializeField, Range(0, 100)] private int spawnChance;
    [SerializeField] private float spawnTime;
    [SerializeField] private int spawnMax;
    public float spawnRange;
    [SerializeField] private float spawnHeight;
    [SerializeField] private LayerMask spawnSurfaces;

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemyParent;
    private int success;
    IEnumerator Start()
    {
        EnemyPool = new ObjectPool();

        while (MainGenerator.root.firstGen)
        {
            yield return null;
        }

        WaitForSeconds wait = new WaitForSeconds(spawnTime);

        while (true)
        {
            yield return wait;

            if (Alive >= PoolSize - 1) continue;

            success = 0;
            Vector3 playerPos = VanController.root.transform.position;

            for (int i = 0; i < spawnMax * 4; i++)
            {
                if (Random.value * 100f > spawnChance) continue;

                if (!TryGetSpawnPosition(playerPos, out Vector3 spawnPos)) continue;
                if (Vector2.Distance(new Vector2(spawnPos.x, spawnPos.z), new Vector2(playerPos.x, playerPos.z)) < 256f) continue;

                SpawnEnemy(spawnPos);
                yield return null;

                if (success >= spawnMax) break;
            }
        }
    }

    public Enemy SpawnEnemy(Vector3 position)
    {
        if (EnemyPool.CreatedCount < PoolSize) EnemyPool.Prewarm(1, enemyPrefab, enemyParent);

        var obj = EnemyPool.Get(enemyPrefab, enemyParent);
        obj.transform.position = position;

        var e = obj.GetComponent<Enemy>();
        e.spawner = this;
        e.Spawn();

        Alive++;
        success++;

        return e;
    }

    private bool TryGetSpawnPosition(Vector3 playerPos, out Vector3 spawnPos)
    {
        Vector2 offset = Random.insideUnitCircle * spawnRange;
        Vector3 samplePoint = new Vector3(playerPos.x + offset.x, playerPos.y + 256f, playerPos.z + offset.y);

        if (Physics.Raycast(samplePoint, Vector3.down, out RaycastHit hit, 512f, spawnSurfaces))
        {
            spawnPos = hit.point + Vector3.up * spawnHeight;
            return true;
        }

        spawnPos = default;
        return false;
    }
}
