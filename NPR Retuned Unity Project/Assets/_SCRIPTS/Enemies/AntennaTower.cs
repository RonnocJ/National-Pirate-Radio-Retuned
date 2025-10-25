using System.Collections;
using UnityEngine;

public class AntennaTower : MonoBehaviour
{
    [SerializeField] private float timeToCapture;
    [SerializeField] private float spawnTime;
    [SerializeField, Range(0f, 1f)] private float spawnChance;
    [SerializeField] private FCCEnemyType[] enemySpawnTypes;
    private bool _captured;
    private bool _capturing;
    private float _captureTimer;

    void OnEnable()
    {
        StartCoroutine(SpawnBuffedEnemies());
    }
    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator SpawnBuffedEnemies()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnTime);

            if (_capturing) continue;

            EnemySpawner eSpawn = EnemyManager.root.SpawnerDict[enemySpawnTypes[Random.Range(0, enemySpawnTypes.Length)]];

            if (eSpawn.Alive >= eSpawn.PoolSize) continue;
            if (Random.value > spawnChance) continue;

            Enemy e = eSpawn.SpawnEnemy(transform.position);

            e.UpdateMaxHealth(e.maxHealth * 1.5f);
        }
    }
    void Update()
    {
        if (_capturing || _captureTimer == 0) return;

        _captureTimer -= Time.deltaTime;
        _captureTimer = Mathf.Clamp(_captureTimer, 0, timeToCapture);
    }
    void OnTriggerStay(Collider col)
    {
        if (col.CompareTag("Player") && !_captured)
        {
            _capturing = true;
            _captureTimer += Time.deltaTime;

            if (_captureTimer > timeToCapture)
            {
                _captured = true;
                Debug.Log("Captured!");
            }

            
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            _capturing = false;
        }
    }
}