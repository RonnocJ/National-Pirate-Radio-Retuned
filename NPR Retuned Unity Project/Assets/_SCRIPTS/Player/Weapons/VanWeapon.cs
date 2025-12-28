using UnityEngine;
public abstract class VanWeapon : MonoBehaviour
{
    [HideInInspector] public Vector3 HitTarget;
    [HideInInspector] public Vector3 HitNormal;
    [HideInInspector] public Collider HitCollider;
    [Header("Arm Alignment")]
    public Transform[] PalmTargets;
    [Header("Weapon Settings")]

    [SerializeField] protected float weaponRange;
    [SerializeField] protected float weaponDamage;
    [SerializeField] protected float hypeDrain;
    [SerializeField] protected LineRenderer aimBeam;
    [SerializeField] protected CameraManager c;
    public float MoveSpeed;
    private bool _weaponActive;
    private bool _weaponFiring => PInputManager.root.actions[PlayerActionType.Action].fValue > 0.1f;
    private Camera _mainCamera;
    void OnEnable()
    {
        WeaponSettings.root.currentWeapon = this;
        _weaponActive = false;
        _mainCamera = Camera.main;
    }
    protected virtual void Start()
    {
        GameManager.root.OnPStateSwitch += ToggleWeapon;
        GameManager.root.OnPauseSwitch += c => { if (c) StopFireWeapon(); };
        PInputManager.root.actions[PlayerActionType.Action].onFValueChange += SetWeaponState;
    }
    protected virtual void ToggleWeapon(PlayerState newState)
    {
        if (newState == PlayerState.Weapon) _weaponActive = true;
        else _weaponActive = false;
    }
    protected void SetWeaponState(float inVal)
    {
        if (!_weaponActive || GameManager.root.Paused) return;

        if (inVal > 0.1f) StartFireWeapon();
        else StopFireWeapon();
    }
    protected virtual void AimWeapon()
    {
        var hits = Physics.SphereCastAll(transform.position, WeaponSettings.root.AimAssist, transform.forward, weaponRange, WeaponSettings.root.LayerInclusions);

        foreach (var sphereHit in hits)
        {
            if (sphereHit.collider.gameObject.TryGetComponent(out Enemy e))
            {
                HitTarget = e.transform.position;
                HitNormal = sphereHit.normal;
                HitCollider = sphereHit.collider;

                MouseMover.root.WeaponTarget = HitTarget;

                if (aimBeam.enabled)
                {
                    aimBeam.SetPosition(0, aimBeam.transform.position);
                    aimBeam.SetPosition(1, HitTarget);
                }

                transform.rotation = Quaternion.LookRotation((HitTarget - transform.position).normalized);

                return;
            }
        }

        Ray ray = new Ray(transform.position, transform.forward.normalized);

        if (Physics.Raycast(ray, out RaycastHit hit, weaponRange, WeaponSettings.root.LayerInclusions))
        {
            HitTarget = hit.point;
            HitNormal = hit.normal;
            HitCollider = hit.collider;
        }
        else
        {
            HitTarget = ray.origin + ray.direction * weaponRange;
            HitNormal = Vector3.zero;
            HitCollider = null;
        }

        MouseMover.root.WeaponTarget = HitTarget;

        if (aimBeam.enabled)
        {
            aimBeam.SetPosition(0, aimBeam.transform.position);
            aimBeam.SetPosition(1, HitTarget);
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(_mainCamera.transform.forward.normalized), Time.deltaTime * MoveSpeed);
    }
    protected virtual void StartFireWeapon()
    {
        if (!_weaponActive) return;
        VanDamage.root.DPS += hypeDrain;
        //c.ShakeCamera(1f);
    }
    protected virtual void FireWeapon()
    {
        if (!_weaponActive) return;

        //c.ShakeCamera(0.2f);
    }
    protected virtual void StopFireWeapon()
    {
        VanDamage.root.DPS -= hypeDrain;
    }
    private void LateUpdate()
    {
        aimBeam.enabled = _weaponActive && !_weaponFiring;

        if (!_weaponActive || GameManager.root.Paused) return;

        AimWeapon();

        if (_weaponFiring) FireWeapon();
    }

}
