using System;
using System.Collections.Generic;
using UnityEngine;
public enum FCCEnemyType
{
    Drone,
    Sniper,
    Cruiser,
    ATV,
    Motorcycles,
    Tunneler,
    HyperMagnet,
    CarjackMine,
    MobileCommandUnit
}
public class EnemyManager : Singleton<EnemyManager>
{
    public Dictionary<FCCEnemyType, EnemySpawner> SpawnerDict = new();
    [SerializeField] private EnemySpawner[] spawners;

    void Start()
    {
        foreach (var s in spawners)
        {
            if (s == null) continue;

            if (!Enum.TryParse(s.gameObject.name, out FCCEnemyType key)) continue;

            SpawnerDict[key] = s;
        }
    }
}