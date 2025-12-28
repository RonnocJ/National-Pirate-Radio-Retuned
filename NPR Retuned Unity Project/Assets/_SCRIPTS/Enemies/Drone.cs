using System.Collections.Generic;
using UnityEngine;

public class Drone : Enemy
{
    [SerializeField] private Animator anim;
    [SerializeField] private float verticalMoveForce;
    [SerializeField] private float horizontalMoveForce;
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private float rotateSpeedDegPerSec = 360f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float waypointArrivalDistance = 2.5f;
    [SerializeField] private float hoverBrakeForce = 8f;
    [Header("Weapons")]
    [SerializeField] private float fireInterval = 0.6f;
    [SerializeField] private float fireRange = 48f;
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
        anim.SetBool("alive", true);
        AudioManager.root.PlaySound(AudioEvent.playDroneHoverLoop, gameObject, 1);
        _repathTimer = repathInterval;
        RebuildWorldPath();
        _randFireInterval = Random.Range(fireInterval - 0.1f, fireInterval + 0.1f);
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();

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

        if (d < 32f) DestroyEnemy(false);
    }

    private void UpdateMovePath()
    {
        _repathTimer += Time.deltaTime;

        if (_repathTimer > repathInterval)
        {
            _repathTimer = 0;
            RebuildWorldPath();
        }

        Quaternion target = transform.rotation;
        var up = _hit.collider != null ? _hit.normal : Vector3.up;

        if (_path != null && Vector3.Distance(_target.position, transform.position) > 64f)
        {
            float arrivalDistanceSqr = waypointArrivalDistance * waypointArrivalDistance;
            int targetIndex = Mathf.Clamp(_pathIndex, 0, _path.Count - 1);
            bool hasTarget = false;
            _pathIndex = targetIndex;

            for (int guard = 0; guard < _path.Count; guard++)
            {
                Vector3 waypoint = _path[targetIndex];
                waypoint.y = transform.position.y;

                Vector3 toNextOnPlane = Vector3.ProjectOnPlane(waypoint - transform.position, up);
                if (toNextOnPlane.sqrMagnitude <= arrivalDistanceSqr && targetIndex < _path.Count - 1)
                {
                    targetIndex++;
                    continue;
                }

                _pathIndex = targetIndex;

                if (toNextOnPlane.sqrMagnitude < 1e-4f)
                {
                    toNextOnPlane = Vector3.ProjectOnPlane(VanController.root.transform.position - transform.position, up);
                }

                if (toNextOnPlane.sqrMagnitude > 1e-4f)
                {
                    target = Quaternion.LookRotation(toNextOnPlane, up);
                    _rb.AddForce(toNextOnPlane.normalized * horizontalMoveForce, ForceMode.Force);
                    hasTarget = true;
                }
                break;
            }

            if (!hasTarget)
            {
                target = Quaternion.LookRotation(VanController.root.transform.position - transform.position, Vector3.up);
                DampHorizontalVelocity(up);
            }
        }
        else
        {
            target = Quaternion.LookRotation(VanController.root.transform.position - transform.position, Vector3.up);
            DampHorizontalVelocity(up);
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, Time.deltaTime * rotateSpeedDegPerSec);
    }

    private void RebuildWorldPath()
    {
        _currentPath = PfGraph.root.FindPath(transform.position, VanController.root.transform.position, false);

        if (_currentPath == null || _currentPath.Count == 0)
        {            
            for(int i = 0; i < 4; i++)
            {
                _currentPath = PfGraph.root.FindPath(transform.position, transform.position + Random.insideUnitSphere * 128f, false);
                if (_currentPath != null && _currentPath.Count > 0) break;
            }
        }

        RebuildWorldWaypoints(_currentPath);

        _pathIndex = 0;
    }

    private void UpdateShoot()
    {
        _fireTimer += Time.deltaTime;

        if (_fireTimer > _randFireInterval)
        {
            _fireTimer = 0;
            _randFireInterval = Random.Range(fireInterval - 0.1f, fireInterval + 0.1f);

            Transform aimOrigin = firePoint != null ? firePoint : transform;
            Vector3 toPlayer = VanController.root.transform.position - aimOrigin.position;
            if (toPlayer.sqrMagnitude <= fireRange * fireRange)
            {
                if (Vector3.Angle(aimOrigin.forward, toPlayer) <= 15f)
                {
                    fireParticle.Play();
                    AudioManager.root.PlaySound(AudioEvent.playDroneShoot, gameObject);
                }
            }
        }
    }
    public override void DestroyEnemy(bool destroyedByPlayer)
    {
        if (_destroyed) return;
        
        base.DestroyEnemy(destroyedByPlayer);

        ExplosionManager.root.Explode(transform.position);
        anim.SetBool("alive", false);

        var cols = Physics.OverlapSphere(transform.position, 32f);

        foreach (var c in cols)
        {
            if (c.CompareTag("Player"))
            {
                VanController.root.PlayerRb.AddExplosionForce(explosionForce, transform.position, 32, 5f * explosionForce, ForceMode.Impulse);
                VanDamage.root.DealDamage(explosionDamage / (Vector3.Distance(transform.position, VanController.root.transform.position) + 1));
            }
        }
    }

    private void DampHorizontalVelocity(Vector3 up)
    {
        Vector3 lateralVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, up);
        if (lateralVelocity.sqrMagnitude < 1e-4f) return;

        _rb.AddForce(-lateralVelocity * hoverBrakeForce, ForceMode.Acceleration);
    }
}
