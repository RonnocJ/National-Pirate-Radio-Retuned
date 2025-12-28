using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class MobileCommandUnit : Enemy
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float turnTorque = 250f;
    [SerializeField] private float contactDamage;
    [SerializeField] private float turretMoveSpeed;
    [SerializeField] private float fireRate;
    [SerializeField] private float fireDamage;
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private Transform turret;
    [SerializeField] private LineRenderer aimLaser;
    [SerializeField] private VisualEffect fireFX;
    private bool _freezeTurret;
    private float _randFireRate;
    private float _fireTimer;
    private RaycastHit _hit;
    public override void Spawn()
    {
        base.Spawn();
        _randFireRate = fireRate;
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (_destroyed || _freezeTurret) return;

        Ray ray = new Ray(transform.position, -transform.up);

        if (Physics.Raycast(ray, out _hit, 128f, terrainMask))
        {
            if (_hit.distance > 10f)
            {
                _rb.AddForce(-transform.up * moveSpeed, ForceMode.Force);
            }
            else if (_hit.distance < 3f)
            {
                _rb.AddForce(transform.up * moveSpeed, ForceMode.Force);
            }
        }
        else
        {
            _hit = default;
        }

        Vector3 desiredForward = Vector3.ProjectOnPlane(VanController.root.transform.position - transform.position, _hit.normal);

        if (desiredForward.sqrMagnitude < 0.0001f)
        {
            desiredForward = Vector3.ProjectOnPlane(transform.forward, _hit.normal);
        }

        desiredForward.Normalize();

        Vector3 torqueToFaceTarget = Vector3.Cross(transform.forward, desiredForward);
        Vector3 totalTorque = (Vector3.Cross(transform.up, _hit.normal) + torqueToFaceTarget) * turnTorque;

        _rb.AddTorque(totalTorque, ForceMode.Acceleration);

        _rb.AddForce(transform.forward * moveSpeed, ForceMode.Force);

        if (Vector3.Distance(_target.position, transform.position) > 320f || Vector3.Distance(_target.position, turret.GetChild(0).position) < 32f)
        {
            aimLaser.enabled = false;
            return;
        }

        aimLaser.enabled = true;
        turret.rotation = Quaternion.Lerp(turret.rotation, Quaternion.LookRotation(_target.position - turret.position, Vector3.up), Time.fixedDeltaTime * turretMoveSpeed);

        Vector3 endPoint = turret.GetChild(0).position + turret.forward * 320f;

        if (Physics.SphereCast(turret.GetChild(0).position, 1.5f, turret.forward, out RaycastHit hit, 320f, targetMask)) endPoint = hit.point;

        aimLaser.SetPosition(1, Vector3.up * Vector3.Distance(turret.GetChild(0).position, endPoint));

        _fireTimer += Time.fixedDeltaTime;

        if (_fireTimer >= _randFireRate && Vector3.Angle(turret.forward, (_target.position - turret.position).normalized) < 15f)
        {
            aimLaser.enabled = false;
            fireFX.Play();

            _freezeTurret = true;

            StartCoroutine(CheckHit(-Mathf.Log(1f - 0.5f * hit.distance / 1000f) / 0.5f, turret.position, turret.forward));
        }
    }
    private IEnumerator CheckHit(float initialDelay, Vector3 initialPos, Vector3 initialDir)
    {
        yield return new WaitForSeconds(initialDelay);

        if (Physics.SphereCast(initialPos, 0.5f, initialDir, out RaycastHit hit, 320f, targetMask))
        {
            ExplosionManager.root.Explode(hit.point);
            if (hit.collider.gameObject.layer == 3) VanDamage.root.DealDamage(fireDamage);
        }

        yield return new WaitForSeconds(0.5f);

        _freezeTurret = false;
        _fireTimer = 0;
        _randFireRate = fireRate + Random.Range(-0.25f, 0.25f);
    }
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == 8)
        {
            var obj = collision.transform.parent.gameObject;

            TreeManager.root.StartCoroutine(TreeManager.root.FellTree(obj, -collision.contacts[0].normal.normalized));
        }
    }
    void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.layer == 3)
        {
            VanDamage.root.DPS += contactDamage;
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (col.gameObject.layer == 3)
        {
            VanDamage.root.DPS -= contactDamage;
        }
    }

    public override void DestroyEnemy(bool killedByPlayer)
    {
        base.DestroyEnemy(killedByPlayer);
        spawner.enabled = false;
    }
}