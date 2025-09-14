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
                if (value <= 0f) DestroyEnemy();

                _health = Mathf.Clamp(value, 0, 99999);
            }
        }
    }
    [SerializeField] protected float maxHealth;
    [SerializeField] protected float repathInterval = 0.5f;
    [SerializeField] protected AudioEvent deathSound;
    protected bool _destroyed;
    protected int _pathIndex;
    protected float _repathTimer;
    public Rigidbody _rb;
    protected Transform _target;
    protected List<Vector3> _path;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _target = VanController.root.transform;

        Health = maxHealth;
    }
    public virtual void Spawn()
    {
        _destroyed = false;
        _path = new List<Vector3>();
        _pathIndex = 0;
    }
    public virtual void DamageEnemy(float damage)
    {
        Health -= damage;
    }
    public virtual void DestroyEnemy()
    {
        if (_destroyed) return;

        _destroyed = true;

        AudioManager.root.PlaySound(deathSound, gameObject, 1, new AudioCallback(() =>
        {
            spawner.EnemyPool.Return(gameObject);
            spawner.Alive--;
        }, AkCallbackType.AK_EndOfEvent));

    }
}