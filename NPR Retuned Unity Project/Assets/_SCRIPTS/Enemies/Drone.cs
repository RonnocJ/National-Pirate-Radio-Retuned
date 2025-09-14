using System.Collections.Generic;
using UnityEngine;

public class Drone : Enemy
{
    [SerializeField] private float verticalMoveForce;
    [SerializeField] private float horizontalMoveForce;
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float rotateSpeedDegPerSec = 360f;
    [SerializeField] private Transform firePoint;
    [Header("Weapons")]
    [SerializeField] private float fireInterval = 0.6f;
    [SerializeField] private float fireRange = 48f;
    [SerializeField] private float fireDamage = 1f;
    [SerializeField] private float explosionDamage;
    [SerializeField] private float explosionForce;
    [SerializeField] private ParticleSystem fireParticle;
    private float _randFireInterval;
    private float _fireTimer;
    private RaycastHit _hit;
    private List<Vector2Int> _currentPath = new List<Vector2Int>();
    public override void Spawn()
    {
        base.Spawn();
        AudioManager.root.PlaySound(AudioEvent.playDroneHoverLoop, gameObject, 1);
        _repathTimer = repathInterval;
        _randFireInterval = Random.Range(fireInterval - 0.1f, fireInterval + 0.1f);
    }
    void FixedUpdate()
    {
        if (_destroyed) return;

        Ray ray = new Ray(transform.position, Vector3.down);

        if (Physics.Raycast(ray, out _hit, 128f, terrainMask))
        {
            if (_hit.distance > 12f)
            {
                _rb.AddForce(Vector3.down * verticalMoveForce, ForceMode.Force);
            }
            else if (_hit.distance < 8f)
            {
                _rb.AddForce(Vector3.up * verticalMoveForce, ForceMode.Force);
            }
        }
        else
        {
            _hit = default;
        }

        UpdateMovePath();
        UpdateShoot();

        float d = Vector3.Distance(_target.position, transform.position);

        AudioManager.root.SetRTPC(AudioRTPC.Drone_Distance, Mathf.Clamp(d, 0f, 256f), false, AudioEvent.playDroneHoverLoop, gameObject, 1);

        if (d < 32f) DestroyEnemy();
    }

    private void UpdateMovePath()
    {
        _repathTimer += Time.deltaTime;

        if (_repathTimer > repathInterval)
        {
            _repathTimer = 0;
            _currentPath = PfGraph.root.FindPath(transform.position, VanController.root.transform.position);
        }

        Quaternion target;
        var up = _hit.collider != null ? _hit.normal : Vector3.up;

        if (_currentPath != null && _currentPath.Count > 1)
        {
            Vector3 waypoint = new Vector3(_currentPath[1].x, transform.position.y, _currentPath[1].y);
            Vector3 toNext = waypoint - transform.position;
            Vector3 toNextOnPlane = Vector3.ProjectOnPlane(toNext, up);
            if (toNextOnPlane.sqrMagnitude < 1e-4f)
            {
                toNextOnPlane = Vector3.ProjectOnPlane(VanController.root.transform.position - transform.position, up);
            }
            target = Quaternion.LookRotation(toNextOnPlane, up);

            if (PfGraph.root.V3ToInt(transform.position) == _currentPath[1])
            {
                _currentPath.Remove(_currentPath[0]);
            }

            _rb.AddForce(transform.forward * horizontalMoveForce, ForceMode.Force);
        }
        else
        {
            target = Quaternion.LookRotation(VanController.root.transform.position - transform.position, Vector3.up);
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, Time.deltaTime * rotateSpeedDegPerSec);
    }

    private void UpdateShoot()
    {
        _fireTimer += Time.deltaTime;

        if (_fireTimer > _randFireInterval)
        {
            _fireTimer = 0;
            _randFireInterval = Random.Range(fireInterval - 0.1f, fireInterval + 0.1f);

            if (Vector3.Distance(transform.position, VanController.root.transform.position) < fireRange)
            {
                Ray ray = new Ray(firePoint.position, firePoint.forward);

                var hits = Physics.SphereCastAll(ray, 0.33f, fireRange);
                foreach (var h in hits)
                {
                    if (h.collider.CompareTag("Player"))
                    {
                        VanDamage.root.DealDamage(fireDamage);
                    }
                }

                fireParticle.Play();
                AudioManager.root.PlaySound(AudioEvent.playDroneShoot, gameObject);
            }
        }
    }
    public override void DestroyEnemy()
    {
        base.DestroyEnemy();

        var cols = Physics.OverlapSphere(transform.position, 32f);

        foreach (var c in cols)
        {
            if (c.CompareTag("Player"))
            {
                VanController.root.PlayerRb.AddExplosionForce(explosionForce, transform.position, 32, 5f * explosionForce, ForceMode.Impulse);
                VanDamage.root.DealDamage(explosionDamage / Vector3.Distance(transform.position, VanController.root.transform.position));
            }
            else if (c.TryGetComponent(out Enemy e) && e != this)
            {
                e._rb.AddExplosionForce(explosionForce, transform.position, 32, explosionForce, ForceMode.Impulse);
                e.DamageEnemy(explosionDamage / Vector3.Distance(transform.position, e.transform.position));
            }
        }
    }
}
