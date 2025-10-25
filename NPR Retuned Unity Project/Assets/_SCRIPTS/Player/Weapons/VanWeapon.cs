using UnityEngine;
public abstract class VanWeapon : MonoBehaviour
{
    [HideInInspector] public Vector3 HitTarget;
    [Header("Arm Alignment")]
    public Transform[] PalmTargets;
    [Header("Weapon Settings")]

    [SerializeField] protected float weaponRange;
    [SerializeField] protected float weaponDamage;
    [SerializeField] protected LineRenderer aimBeam;
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
        PInputManager.root.actions[PlayerActionType.Action].onFValueChange += c =>
        {
            if (c < 0.1f) StopFireWeapon();
        };
    }
    protected virtual void ToggleWeapon(PlayerState newState)
    {
        if (newState == PlayerState.Weapon) _weaponActive = true;
        else _weaponActive = false;
    }
    protected virtual void AimWeapon()
    {
        HitTarget = Vector3.zero;
        var cols = Physics.SphereCastAll(_mainCamera.transform.position, WeaponSettings.root.AimAssist, _mainCamera.transform.forward.normalized, weaponRange, WeaponSettings.root.LayerInclusions);

        foreach (var c in cols)
        {
            if (c.collider.gameObject.TryGetComponent(out Enemy e))
            {
                HitTarget = e.transform.position;
                break;
            }
        }
        if (HitTarget == Vector3.zero)
        {
            Ray ray = new Ray(transform.position, _mainCamera.transform.forward.normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, weaponRange, WeaponSettings.root.LayerInclusions))
            {
                HitTarget = hit.point;
            }
            else
            {
                HitTarget = ray.origin + ray.direction * weaponRange;
            }
        }

        if (aimBeam.enabled)
        {
            aimBeam.SetPosition(0, aimBeam.transform.position);
            aimBeam.SetPosition(1, HitTarget);
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(HitTarget - transform.position), Time.deltaTime * MoveSpeed);
    }
    protected virtual void FireWeapon()
    {
        if (!_weaponActive) return;
    }
    protected virtual void StopFireWeapon()
    {

    }
    private void LateUpdate()
    {
        aimBeam.enabled = _weaponActive && !_weaponFiring;

        if (!_weaponActive) return;

        AimWeapon();

        if (_weaponFiring) FireWeapon();
    }

}
