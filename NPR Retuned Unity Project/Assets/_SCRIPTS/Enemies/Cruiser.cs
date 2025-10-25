using System.Collections;
using UnityEngine;

public class Cruiser : EnemyVehicle
{
    [Header("Weapon Settings")]
    [SerializeField] private float fireInterval;
    [SerializeField] private float fireRange;
    [SerializeField] private float aimSpeed;
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem fireParticle;
    [Header("Rooting Settings")]
    [SerializeField] private float rootDistance;
    [SerializeField] private float releaseDistance;
    [SerializeField] private float frontAngleThreshold;
    private float _fireTimer;
    private float _randFireInterval;
    private bool _isRooted;
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (_destroyed) return;
                
                        UpdateRootingState();

        if (_isRooted)
        {
            DriveInput = Vector2.zero;
            BrakeInput = 1f;
        }

        UpdateShoot();
    }
    private void UpdateShoot()
    {
        Vector3 toPlayer = VanController.root.transform.position + Vector3.up - firePoint.position;

        if (toPlayer.sqrMagnitude <= fireRange * fireRange)
        {
            _fireTimer += Time.deltaTime;

            firePoint.parent.rotation = Quaternion.RotateTowards(firePoint.parent.rotation, Quaternion.LookRotation(toPlayer), aimSpeed);

            if (_fireTimer > _randFireInterval && Vector3.Angle(firePoint.forward, toPlayer) <= 15f)
            {
                _fireTimer = 0;
                _randFireInterval = Random.Range(fireInterval - 0.1f, fireInterval + 0.1f);
                fireParticle.Play();
                AudioManager.root.PlaySound(AudioEvent.playDroneShoot, gameObject);
            }
        }
    }
    private void UpdateRootingState()
    {
        Vector3 toVehicle = transform.position - VanController.root.transform.position;

        if (toVehicle.sqrMagnitude > rootDistance * rootDistance) return;

        Vector3 playerForward = VanController.root.transform.forward;
        toVehicle.y = 0f;
        playerForward.y = 0f;

        toVehicle.Normalize();
        playerForward.Normalize();

        float dot = Vector3.Dot(playerForward, toVehicle);
        float angleThreshold = Mathf.Cos(frontAngleThreshold * Mathf.Deg2Rad);

        if (dot >= angleThreshold && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit) && hit.collider.gameObject.layer == 7)
        {
            StartCoroutine(SetRooted(true));
        }
        else if (_isRooted && (transform.position - VanController.root.transform.position).sqrMagnitude > releaseDistance * releaseDistance)
        {
            StartCoroutine(SetRooted(false));
        }
    }
    private IEnumerator SetRooted(bool shouldRoot)
    {
        if (_isRooted == shouldRoot) yield break;

        _isRooted = shouldRoot;

        if (shouldRoot)
        {
            for (int i = 0; i < 60; i++)
            {
                _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, Vector3.zero, i / 60f);
                _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, i / 60f);

                Vector3 toVehicle = transform.position - VanController.root.transform.position; Vector3 playerForward = VanController.root.transform.forward;
                toVehicle.y = 0f;
                playerForward.y = 0f;

                toVehicle.Normalize();
                playerForward.Normalize();

                float dot = Vector3.Dot(playerForward, toVehicle);
                float angleThreshold = Mathf.Cos(frontAngleThreshold * Mathf.Deg2Rad);
                if (dot < angleThreshold || !Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit) || hit.collider.gameObject.layer != 7) yield break;
                yield return null;
            }
        }

        _rb.isKinematic = shouldRoot;
    }
}