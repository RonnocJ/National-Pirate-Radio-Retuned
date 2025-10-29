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
    [SerializeField] private LayerMask collisionMask;
    private bool _cameraMoveable = false;
    private float _currentYaw;
    private float _currentPitch;
    private Vector2 _lookInput => PInputManager.root.actions[PlayerActionType.Look].v2Value;
    private Vector2 _currentLookInput;
    private Vector3 _desiredPosition;
    private Vector3 _lastFrameTargetPos;
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

        _lastFrameTargetPos = _target.position;
    }

    private void FixedCamera()
    {
        Vector3 flatForward = transform.forward;
        flatForward.y = 0;
        flatForward = Vector3.Lerp(flatForward, _target.forward, Vector3.Distance(_lastFrameTargetPos, _target.position) * smoothSpeed * Time.fixedDeltaTime);

        _desiredPosition = _target.position - (flatForward * cameraDistance) + (Vector3.up * cameraHeight);
        
        flatForward = (_target.position - _desiredPosition).normalized;
        transform.forward = flatForward;

        _currentYaw = transform.eulerAngles.y;
        _currentPitch = 0;
    }

    private void MoveableCamera()
    {
        _currentLookInput = Vector2.Lerp(_currentLookInput, _lookInput, Time.deltaTime * lookSensitivity);

        if (GameManager.root.NewGame && Tutorial.root.Iteration < 2) _currentLookInput = Vector2.zero;

        _currentYaw += _currentLookInput.x;
        _currentPitch -= _currentLookInput.y;

        _currentPitch = Mathf.Clamp(_currentPitch, -pitchTop, pitchBottom);

        Vector3 offset = Quaternion.Euler(0, _currentYaw, 0) * (Vector3.back * cameraDistance);

        _desiredPosition = _target.position + offset + (Vector3.up * cameraHeight);

        transform.rotation = Quaternion.LookRotation(_target.position - _desiredPosition, Vector3.up) * Quaternion.Euler(_currentPitch, 0, 0);
    }

    private void DetectCollisions()
    {
        if(Physics.SphereCast(_target.position, 0.05f, transform.position - _target.position, out RaycastHit hit, cameraDistance, collisionMask))
        {
            _desiredPosition = hit.point + ((_target.position - transform.position) * 0.05f);
            _desiredPosition.y = _target.position.y + cameraHeight;
        }
    }
}