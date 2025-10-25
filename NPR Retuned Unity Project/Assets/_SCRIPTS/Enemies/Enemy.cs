using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    public EnemySpawner spawner;
    [SerializeField] protected float _health;
    public float Health
    {
        get => _health;
        set
        {
            if (_health != value)
            {
                if (value <= 0f) DestroyEnemy(true);

                _health = Mathf.Clamp(value, 0, maxHealth);
            }
        }
    }
    public float maxHealth;
    [SerializeField] protected float value;
    [SerializeField] protected float repathInterval = 0.5f;
    [SerializeField] protected AudioEvent deathSound;
    protected bool _destroyed;
    protected int _pathIndex;
    protected float _repathTimer;
    public Rigidbody _rb;
    protected Transform _target;
    protected List<Vector3> _path = new();

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _target = VanController.root.transform;

        Health = maxHealth;
    }
    public virtual void Spawn()
    {
        _destroyed = false;
        _path ??= new List<Vector3>();
        _path.Clear();
        _pathIndex = 0;

        UpdateMaxHealth(maxHealth);
    }
    protected virtual void FixedUpdate()
    {
        if (_destroyed) return;
        
        if (!_destroyed && Vector3.Distance(_target.position, transform.position) > spawner.spawnRange) DestroyEnemy(false);
    }
    protected void RebuildWorldWaypoints(List<Vector2Int> cellPath)
    {
        _path.Clear();

        if (cellPath == null || cellPath.Count == 0) return;

        for (int i = 0; i < cellPath.Count; i++)
        {
            Vector2Int cell = cellPath[i];
            Vector3 worldPos = new Vector3(cell.x + (PfGraph.root.CellSize * 0.5f), 0f, cell.y + (PfGraph.root.CellSize * 0.5f));
            Vector3 localPos = PosUtil.GetLocalPos(worldPos);
            localPos.y = transform.position.y;
            _path.Add(localPos);
        }
    }
    public virtual void UpdateMaxHealth(float newMaxHealth)
    {
        Health *= newMaxHealth / maxHealth;
        maxHealth = newMaxHealth;
    }
    public virtual void DamageEnemy(float damage)
    {
        Health -= damage;
    }
    public virtual void DestroyEnemy(bool killedByPlayer)
    {
        if (_destroyed) return;

        _destroyed = true;

        if (killedByPlayer) PlayerMoney.root.RunMoney += value;

        AudioManager.root.PlaySound(deathSound, gameObject, 1, new AudioCallback(() =>
        {
            spawner.EnemyPool.Return(gameObject);
            spawner.Alive--;
        }, AkCallbackType.AK_EndOfEvent));
    }
}
