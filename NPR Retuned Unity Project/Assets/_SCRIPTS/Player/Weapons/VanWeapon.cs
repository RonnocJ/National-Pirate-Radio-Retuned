using UnityEngine;
public abstract class VanWeapon : MonoBehaviour
{
    [HideInInspector] public Vector3 HitTarget;
    [Header("Arm Alignment")]
    public Transform[] PalmTargets;
    [Header("Weapon Settings")]
    [SerializeField] private float weaponRange;
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
        PInputManager.root.actions[PlayerActionType.Switch].bAction += ToggleWeapon;
        PInputManager.root.actions[PlayerActionType.Action].bAction += StopFireWeapon;
    }
    protected virtual void ToggleWeapon()
    {
        _weaponActive = !_weaponActive;
    }
    protected virtual void AimWeapon()
    {
        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward.normalized);

        if (Physics.Raycast(ray, out RaycastHit hit, weaponRange, WeaponSettings.root.LayerInclusions))
        {
            HitTarget = hit.point;
        }
        else
        {
            HitTarget = ray.origin + ray.direction * weaponRange;
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
    private void Update()
    {
        if (!_weaponActive) return;

        AimWeapon();

        if (_weaponFiring) FireWeapon();
    }

}
