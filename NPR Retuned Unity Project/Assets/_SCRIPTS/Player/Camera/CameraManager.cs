using System.Collections;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private float cameraDistance;
    [SerializeField] private float cameraHeight;
    [SerializeField] private float smoothSpeed;
    [SerializeField] private float lookSensitivity;
    [SerializeField] private float pitchTop;
    [SerializeField] private float pitchBottom;
    [SerializeField] private float dollySpeed;
    [SerializeField] private int maxCollisionChecks;
    [SerializeField] private float collisionRadius;
    [SerializeField] private LayerMask collisionInclusions;
    [SerializeField] private ArmAnimator[] arms;
    private bool _cameraMoveable = false;
    private float _currentYaw;
    private float _currentPitch;
    private Vector2 _lookInput => PInputManager.root.actions[PlayerActionType.Look].v2Value;
    private Vector2 _currentLookInput;
    private Vector3 _desiredPosition;
    private Transform _target => VanController.root.transform;

    void Start()
    {
        if (!GameManager.root.NewGame) RegisterSwitchInputs();
    }
    public void RegisterSwitchInputs()
    {
        PInputManager.root.actions[PlayerActionType.Switch].bAction += SwitchState;
        GameManager.root.OnPStateSwitch += SwitchMode;
    }
    private void SwitchState()
    {
        if (GameManager.root.CurrentPState is PlayerState.Utility or PlayerState.Weapon)
        {
            GameManager.root.CurrentPState = (PlayerState)(-(int)GameManager.root.CurrentPState + 3);
        }
    }
    private void SwitchMode(PlayerState newState)
    {
        if (newState is PlayerState.Utility or PlayerState.Weapon)
        {
            if (newState == PlayerState.Utility) _cameraMoveable = false;
            else _cameraMoveable = true;
        }
    }
    void FixedUpdate()
    {
        if (_cameraMoveable) MoveableCamera();
        else FixedCamera();

        DetectCollisions();

        transform.position = _desiredPosition;

        if (_cameraMoveable)
        {
            transform.rotation = Quaternion.LookRotation(_target.position - _desiredPosition, Vector3.up) * Quaternion.Euler(_currentPitch, 0, 0);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(_target.position + _target.forward - _desiredPosition, Vector3.up);
        }
    }

    private void FixedCamera()
    {
        _desiredPosition = Vector3.Lerp(transform.position, _target.position - _target.forward * cameraDistance + Vector3.up * cameraHeight, smoothSpeed * Time.deltaTime * Vector3.Distance(transform.position, _target.position) / cameraDistance);

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_target.position + _target.forward - transform.position, Vector3.up), smoothSpeed * Time.deltaTime / 10f);

        _currentYaw = transform.eulerAngles.y;
    }

    private void MoveableCamera()
    {
        _currentLookInput = Vector2.Lerp(_currentLookInput, _lookInput, Time.deltaTime * lookSensitivity);

        if (GameManager.root.NewGame && Tutorial.root.Iteration < 2) _currentLookInput = Vector2.zero;
         
        _currentYaw += _currentLookInput.x;
        _currentPitch -= _currentLookInput.y;

        _currentPitch = Mathf.Clamp(_currentPitch, -pitchTop, pitchBottom);

        Vector3 offset = Quaternion.Euler(0, _currentYaw, 0) * (Vector3.back * cameraDistance);

        _desiredPosition = _target.position + offset + Vector3.up * cameraHeight;
    }

    private void DetectCollisions()
    {
        Vector3 targetPosition = _target.position;
        Vector3 currentPosition = transform.position;
        Vector3 desiredOffset = _desiredPosition - targetPosition;

        float desiredDistance = desiredOffset.magnitude;
        if (desiredDistance <= Mathf.Epsilon) return;

        Vector3 desiredDirection = desiredOffset / desiredDistance;

        bool blockedAlongPath = Physics.SphereCast(
            targetPosition,
            collisionRadius,
            desiredDirection,
            out RaycastHit hit,
            desiredDistance,
            collisionInclusions,
            QueryTriggerInteraction.Ignore
        );

        bool blockedAtDestination = Physics.CheckSphere(
            targetPosition + desiredOffset,
            collisionRadius,
            collisionInclusions,
            QueryTriggerInteraction.Ignore
        );

        if (!blockedAlongPath && !blockedAtDestination)
            return;

        if (TryFindSlidePosition(targetPosition, desiredOffset, out Vector3 slidePosition, out float yawAdjustment))
        {
            _desiredPosition = Vector3.Lerp(currentPosition, slidePosition, dollySpeed * Time.deltaTime);

            if (_cameraMoveable)
                _currentYaw = NormalizeAngle(_currentYaw + yawAdjustment);

            return;
        }

        if (blockedAlongPath)
        {
            float safeDistance = Mathf.Clamp(hit.distance - collisionRadius, 0f, desiredDistance);
            Vector3 fallbackPosition = targetPosition + desiredDirection * safeDistance;
            _desiredPosition = Vector3.Lerp(currentPosition, fallbackPosition, dollySpeed * Time.deltaTime);
        }
        else
        {
            float safeDistance = Mathf.Max(desiredDistance - collisionRadius, 0f);
            Vector3 fallbackPosition = targetPosition + desiredDirection * safeDistance;
            _desiredPosition = Vector3.Lerp(currentPosition, fallbackPosition, dollySpeed * Time.deltaTime);
        }
    }

    private bool TryFindSlidePosition(Vector3 targetPosition, Vector3 desiredOffset, out Vector3 slidePosition, out float yawAdjustment)
    {
        slidePosition = default;
        yawAdjustment = 0f;

        int checks = Mathf.Max(2, maxCollisionChecks);
        float angleStep = 180f / checks;
        Vector3 upAxis = Vector3.up;

        for (int i = 1; i <= checks; i++)
        {
            float angle = angleStep * i;

            if (EvaluateSlideCandidate(targetPosition, desiredOffset, angle, upAxis, out slidePosition))
            {
                yawAdjustment = angle;
                return true;
            }

            if (EvaluateSlideCandidate(targetPosition, desiredOffset, -angle, upAxis, out slidePosition))
            {
                yawAdjustment = -angle;
                return true;
            }
        }

        return false;
    }

    private bool EvaluateSlideCandidate(Vector3 targetPosition, Vector3 desiredOffset, float angle, Vector3 upAxis, out Vector3 candidatePosition)
    {
        Quaternion rotation = Quaternion.AngleAxis(angle, upAxis);
        Vector3 rotatedOffset = rotation * desiredOffset;
        float rotatedDistance = rotatedOffset.magnitude;

        if (rotatedDistance <= Mathf.Epsilon)
        {
            candidatePosition = default;
            return false;
        }

        Vector3 direction = rotatedOffset / rotatedDistance;

        bool pathBlocked = Physics.SphereCast(
            targetPosition,
            collisionRadius,
            direction,
            out _,
            rotatedDistance,
            collisionInclusions,
            QueryTriggerInteraction.Ignore
        );

        if (pathBlocked)
        {
            candidatePosition = default;
            return false;
        }

        candidatePosition = targetPosition + rotatedOffset;

        bool destinationBlocked = Physics.CheckSphere(
            candidatePosition,
            collisionRadius,
            collisionInclusions,
            QueryTriggerInteraction.Ignore
        );

        return !destinationBlocked;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
            angle += 360f;

        return angle;
    }
}