using UnityEngine;

public class MouseMover : Singleton<MouseMover>
{
    [SerializeField] private float mouseSpeed;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private float minLocalYWhileGrabbing = -0.25f; // clamp mouse Y while grabbing
    private bool _mouseActive;
    private bool _grabbing;
    private bool _hasHit;
    private Vector2 _mousePos => PInputManager.root.actions[PlayerActionType.Cursor].v2Value;
    private Vector3 _hitPoint;
    private Quaternion _hoverTargetLocalRotation;
    private RaycastHit _hit;
    private Transform _child;
    private Animator _anim;
    private Camera _mainCam;
    private Grabbable _grabTarget;
    void Start()
    {
        GameManager.root.OnPStateSwitch += ChangeMouse;

        Cursor.visible = false;

        _child = transform.GetChild(0).GetChild(0);
        _anim = GetComponent<Animator>();
        _mainCam = Camera.main;

        _mouseActive = true;
        _hasHit = false;

        PInputManager.root.actions[PlayerActionType.Action].onFValueChange += CheckClick;
        PInputManager.root.actions[PlayerActionType.Look].onV2ValueChange += CheckHover;
    }
    private void ChangeMouse(PlayerState newState)
    {
        if (newState == PlayerState.Utility)
        {
            transform.localScale = Vector3.one * 0.05f;
            _mouseActive = true;
        }
        else if (newState == PlayerState.Weapon)
        {
            transform.localScale = Vector3.zero;
            _mouseActive = false;
        }
    }

    void Update()
    {
        if (!_mouseActive) return;

        Ray rayZ = _mainCam.ScreenPointToRay(_mousePos);
        rayZ.origin = _mainCam.transform.position;

        if (Physics.SphereCast(rayZ.origin, 0.05f, rayZ.direction, out RaycastHit hitZ, 10, 1 << 5))
        {
            _hitPoint = hitZ.point - Vector3.forward * 0.05f;
            _hit = hitZ;
            _hasHit = true;
        }
        Ray rayY = new Ray(transform.position, -transform.up);

        if (Physics.Raycast(rayY, out RaycastHit hitY, 0.125f, 1 << 5))
        {
            _hitPoint.y = hitY.point.y + 0.05f;
        }

        Vector3 lp = transform.localPosition;

        transform.localPosition = Vector3.Lerp(lp, transform.InverseTransformPoint(_hitPoint), Time.deltaTime * mouseSpeed);

        if (_grabbing)
        {
            // Disallow the mouse to go below a minimum local Y when grabbing
            Vector3 clamped = transform.localPosition;
            clamped.y = Mathf.Max(clamped.y, minLocalYWhileGrabbing);
            transform.localPosition = clamped;
        }

        Quaternion targetLocal = _anim.GetBool("hoveringPoint") ? _hoverTargetLocalRotation : Quaternion.identity;
        _child.localRotation = Quaternion.Lerp(_child.localRotation, targetLocal, Time.deltaTime * rotationLerpSpeed);

        if (_grabbing) _grabTarget.OnDrag();
    }

    private void CheckHover(Vector2 _)
    {
        if (!_hasHit) return;

        if (_hit.collider.gameObject.TryGetComponent(out Interactable i) && i.Enabled)
        {
            Vector3 up = (_hit.transform.position - _child.position).normalized;
            Vector3 fwd = Vector3.Cross(up, transform.right).normalized;

            Quaternion targetWorld = Quaternion.LookRotation(fwd, up);
            _hoverTargetLocalRotation = Quaternion.Inverse(transform.rotation) * targetWorld;

            i.OnHover();

            _anim.SetBool("hoveringPoint", true);
        }
        else
        {
            _anim.SetBool("hoveringPoint", false);
        }

        if (_hit.collider.gameObject.TryGetComponent(out Grabbable d))
        {
            d.OnHover();

            _anim.SetBool("hoveringGrab", true);
        }
        else
        {
            _anim.SetBool("hoveringGrab", false);
        }
    }

    private void CheckClick(float newFVal)
    {
        if (!_hasHit) return;

        _hit.collider.gameObject.TryGetComponent(out Interactable i);

        if (newFVal > 0)
        {
            if (i != null && i.Enabled)
            {
                i.OnClick();
                _anim.SetTrigger("click");
            }
            else if (_hit.collider.gameObject.TryGetComponent(out Grabbable d))
            {
                _grabbing = true;
                _grabTarget = d;

                _anim.SetBool("grabbing", true);
            }
        }
        else
        {
            if (_grabTarget != null)
            {
                _grabbing = false;
                _grabTarget.OnRelease();
                _grabTarget = null;

                _anim.SetBool("grabbing", false);
            }
            else if (i != null)
            {
                i.OnRelease();
            }
        }
    }
    public void ForceRelease()
    {
        _hit.collider.gameObject.TryGetComponent(out Interactable i);

        if (_grabTarget != null)
        {
            _grabbing = false;
            _grabTarget.OnRelease();
            _grabTarget = null;

            _anim.SetBool("grabbing", false);
        }
        else if (i != null)
        {
            i.OnRelease();
        }
    }
}
