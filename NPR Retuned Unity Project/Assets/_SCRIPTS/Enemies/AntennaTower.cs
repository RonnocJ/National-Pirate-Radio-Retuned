using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntennaTower : MonoBehaviour
{
    [SerializeField] private float timeToCapture;
    [SerializeField] private float spawnTime;
    [SerializeField, Range(0f, 1f)] private float spawnChance;
    [SerializeField] private GameObject buffBeamPrefab;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private FCCEnemyType[] enemySpawnTypes;
    private bool _captured;
    private bool _capturing;
    private float _captureTimer;
    private ObjectPool _buffBeamPool;
    [SerializeField] private List<AntennaBuffBeam> _activeBeams = new();
    void OnEnable()
    {
        _buffBeamPool = new ObjectPool();
    }
    void Update()
    {
        var enemyCols = Physics.OverlapSphere(transform.position, 1024f, enemyMask);
        var beamsToRemove = new List<AntennaBuffBeam>(_activeBeams);

        foreach (var col in enemyCols)
        {
            if (!col.CompareTag("Buffable")) continue;

            var enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            var existing = _activeBeams.Find(b => b.ETarget == enemy);
            if (existing != null)
            {
                beamsToRemove.Remove(existing);
                continue;
            }

            var beam = _buffBeamPool.Get(buffBeamPrefab, transform.GetChild(0));
            var antennaBeam = beam.GetComponent<AntennaBuffBeam>();

            antennaBeam.Source = this;
            antennaBeam.ETarget = enemy;
            antennaBeam.VTarget = null;
            antennaBeam.BuffEnemy();

            _activeBeams.Add(antennaBeam);
        }

        foreach (var b in beamsToRemove)
        {
            if(b.VTarget != null) continue;
            
            _activeBeams.Remove(b);
            b.DebuffEnemy();
            _buffBeamPool.Return(b.gameObject);
        }

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
                
                var beam = _buffBeamPool.Get(buffBeamPrefab, transform.GetChild(0));
                var antennaBeam = beam.GetComponent<AntennaBuffBeam>();

                antennaBeam.Source = this;
                antennaBeam.ETarget = null;
                antennaBeam.VTarget = col.GetComponentInParent<VanController>();
                
                _activeBeams.Add(antennaBeam);
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