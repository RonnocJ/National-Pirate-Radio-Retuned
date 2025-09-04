using System.Collections;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private float cameraDistance;
    [SerializeField] private float cameraHeight;
    [SerializeField] private float smoothSpeed;
    [SerializeField] private float pitchTop;
    [SerializeField] private float pitchBottom;
    [SerializeField] private float dollySpeed;
    [SerializeField] private int maxCollisionChecks;
    [SerializeField] private float collisionRadius;
    [SerializeField] private LayerMask collisionInclusions;
    [SerializeField] private ArmAnimator[] arms;
    private bool _cameraMoveable;
    private float _currentYaw;
    private float _currentPitch;
    private Vector2 _lookInput => PInputManager.root.actions[PlayerActionType.Look].v2Value;
    private Vector3 _desiredPosition;
    private Transform _target => VanController.root.transform;

    IEnumerator Start()
    {
        _cameraMoveable = false;
        GameManager.root.OnPStateSwitch += SwitchMode;

        yield return null;

        foreach (var arm in arms)
        {
            arm.GetComponent<Animator>().enabled = _cameraMoveable;
        }
    }
    private void SwitchMode(PlayerState newState)
    {
        if (newState is PlayerState.Utility or PlayerState.Weapon)
        {
            _cameraMoveable = !_cameraMoveable;

            foreach (var arm in arms)
            {
                arm.GetComponent<Animator>().enabled = _cameraMoveable;
            }
        }
    }
    void FixedUpdate()
    {
        if (_cameraMoveable) MoveableCamera();
        else FixedCamera();

        DetectCollisions();

        transform.position = _desiredPosition;

        Vector3 lookDir = _target.position - _desiredPosition;
        if (lookDir.magnitude < 0.1f)
            lookDir = _target.forward;

        if (_cameraMoveable)
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up) * Quaternion.Euler(_currentPitch, 0, 0);
        else
            transform.rotation = Quaternion.LookRotation(_target.position + _target.forward - _desiredPosition, Vector3.up);
    }

    private void FixedCamera()
    {
        _desiredPosition = Vector3.Lerp(transform.position, _target.position - _target.forward * cameraDistance + Vector3.up * cameraHeight, smoothSpeed * Time.deltaTime * Vector3.Distance(transform.position, _target.position) / cameraDistance);

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_target.position + _target.forward - transform.position, Vector3.up), smoothSpeed * Time.deltaTime / 10f);

        _currentYaw = transform.eulerAngles.y;
    }

    private void MoveableCamera()
    {
        _currentYaw += _lookInput.x;
        _currentPitch -= _lookInput.y;
        _currentPitch = Mathf.Clamp(_currentPitch, -pitchTop, pitchBottom);

        Vector3 offset = Quaternion.Euler(0, _currentYaw, 0) * (Vector3.back * cameraDistance);

        _desiredPosition = _target.position + offset + Vector3.up * cameraHeight;

        transform.rotation = Quaternion.LookRotation(_target.position - _desiredPosition, Vector3.up) * Quaternion.Euler(_currentPitch, 0, 0);
    }

    private void DetectCollisions()
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            collisionRadius,
            _target.position - transform.position,
            Vector3.Distance(transform.position, _target.position) + collisionRadius,
            collisionInclusions
        );

        if (hits.Length > 0)
        {
            float closestHitDistance = Vector3.Distance(transform.position, _target.position) + collisionRadius;

            foreach (RaycastHit hit in hits)
            {
                float hitDistance = Vector3.Distance(hit.point, transform.position);

                if (hitDistance < closestHitDistance)
                    closestHitDistance = hitDistance;
            }

            _desiredPosition = Vector3.Lerp(transform.position, _target.position - transform.forward * (closestHitDistance - collisionRadius) + Vector3.up * cameraHeight, dollySpeed * Time.deltaTime);
        }
    }
}