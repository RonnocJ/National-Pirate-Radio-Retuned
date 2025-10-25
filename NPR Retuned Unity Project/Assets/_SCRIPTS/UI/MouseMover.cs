using UnityEngine;

public class MouseMover : Singleton<MouseMover>
{
    [SerializeField] private float mouseSpeed;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private float mouseDeltaSensitivity = 1f;
    [SerializeField] private float mouseScaleMult;
        [SerializeField] private Camera mainCam;
    private bool _mouseActive = false;
    public bool _grabbing;
    private bool _hasHit;
    private Vector2 _virtualMousePos;
    private Vector3 _hitPoint;
    private Vector3 _originalScale;
    private Quaternion _hoverTargetLocalRotation;
    private Ray _rayZ;
    private Transform _child;
    private Animator _anim;

    private Grabbable _hoverGrabTarget;
    private Grabbable _heldGrabTarget;
    private Interactable _interactableTarget;
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _child = transform.GetChild(0).GetChild(0);
        _anim = GetComponent<Animator>();

        _originalScale = Vector3.one * (Vector3.Distance(mainCam.transform.position, transform.position) * mouseScaleMult);
        transform.localScale = _originalScale;

        _mouseActive = true;
        _hasHit = false;

        _virtualMousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        GameManager.root.OnPStateSwitch += ChangeMouse;

        PInputManager.root.actions[PlayerActionType.Action].onFValueChange += CheckClick;
    }
    private void ChangeMouse(PlayerState newState)
    {
        if (newState == PlayerState.Utility)
        {
            transform.localScale = _originalScale;
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

        _rayZ = mainCam.ScreenPointToRay(_virtualMousePos);
        _rayZ.origin = mainCam.transform.position;

        if (_grabbing && _heldGrabTarget != null)
        {
            RaycastHit[] hits = Physics.SphereCastAll(_rayZ.origin, 0.01f, _rayZ.direction, 50f, 1 << 5, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (var h in hits)
            {
                if (!IsPartOfHeld(h.collider.transform))
                {
                    _hitPoint = h.point - Vector3.forward * 0.05f;

                    found = true;
                    break;
                }
            }
            if (!found) _hasHit = false;
        }
        else
        {
            if (Physics.SphereCast(_rayZ, 0.01f, out RaycastHit hitZ, 50f, 1 << 5, QueryTriggerInteraction.Ignore))
            {
                _hitPoint = hitZ.point - Vector3.forward * 0.05f;
            }
        }

        Ray rayY = new Ray(transform.position, -transform.up);

        if (_grabbing && _heldGrabTarget != null)
        {
            RaycastHit[] hitsY = Physics.RaycastAll(rayY, 0.125f, 1 << 5, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hitsY, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hy in hitsY)
            {
                if (!IsPartOfHeld(hy.collider.transform))
                {
                    _hitPoint.y = hy.point.y + 0.05f;
                    break;
                }
            }
        }
        else
        {
            if (Physics.SphereCast(rayY, 0.01f, out RaycastHit hitY, 0.125f, 1 << 5, QueryTriggerInteraction.Ignore))
            {
                _hitPoint.y += 0.1f - hitY.distance;
            }
        }

        _virtualMousePos += PInputManager.root.actions[PlayerActionType.Look].v2Value * mouseDeltaSensitivity;
        _virtualMousePos.x = Mathf.Clamp(_virtualMousePos.x, 0f, Mathf.Max(0f, Screen.width - 1f));
        _virtualMousePos.y = Mathf.Clamp(_virtualMousePos.y, 0f, Mathf.Max(0f, Screen.height - 1f));

        if (!Physics.SphereCast(_rayZ.origin, 0.05f, _rayZ.direction, out RaycastHit triggerHit, 50f, 1 << 5))
        {
            _hasHit = false;
            return;
        }
        else _hasHit = true;

        if (triggerHit.collider.gameObject.TryGetComponent(out Interactable i) && i.Enabled)
        {
            _interactableTarget = i;
            Vector3 up = (triggerHit.transform.position - _child.position).normalized;
            Vector3 fwd = Vector3.Cross(up, transform.right).normalized;

            Quaternion targetWorld = Quaternion.LookRotation(fwd, up);
            _hoverTargetLocalRotation = Quaternion.Inverse(transform.rotation) * targetWorld;

            _interactableTarget.OnHover();

            _anim.SetBool("hoveringPoint", true);

            if (!_grabbing && _hoverGrabTarget != null)
            {
                _hoverGrabTarget = null;
                _anim.SetBool("hoveringGrab", false);
            }
        }
        else if (!_grabbing && triggerHit.collider.gameObject.TryGetComponent(out Grabbable g))
        {
            _hoverGrabTarget = g;
            _hoverGrabTarget.OnHover();

            _anim.SetBool("hoveringGrab", true);
        }
        else if (_interactableTarget != null)
        {
            _interactableTarget.OnEndHover();
            _interactableTarget = null;
            _anim.SetBool("hoveringPoint", false);
        }
        else if (!_grabbing && _hoverGrabTarget != null)
        {
            _hoverGrabTarget = null;
            _anim.SetBool("hoveringGrab", false);
        }
        else if (_grabbing)
        {
            _anim.SetBool("hoveringGrab", false);
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, transform.InverseTransformPoint(_hitPoint), Time.deltaTime * mouseSpeed);

        Quaternion targetLocal = _anim.GetBool("hoveringPoint") ? _hoverTargetLocalRotation : Quaternion.LookRotation(transform.InverseTransformDirection(mainCam.transform.forward), transform.InverseTransformDirection(mainCam.transform.up));
        _child.localRotation = Quaternion.Lerp(_child.localRotation, targetLocal, Time.deltaTime * rotationLerpSpeed);

        if (_grabbing && _heldGrabTarget != null) _heldGrabTarget.OnDrag();
    }
    private bool IsPartOfHeld(Transform t)
    {
        if (_heldGrabTarget == null) return false;
        Transform root = _heldGrabTarget.transform;
        while (t != null)
        {
            if (t == root) return true;
            t = t.parent;
        }
        return false;
    }

    private void CheckClick(float newFVal)
    {
        if (!_hasHit) return;

        if (newFVal > 0)
        {
            if (_interactableTarget != null && _interactableTarget.Enabled)
            {
                _interactableTarget.OnClick();
                _anim.SetTrigger("click");
                AudioManager.root.PlaySound(AudioEvent.playMouseClick, gameObject);
            }
            else if (!_grabbing && _hoverGrabTarget != null)
            {
                _heldGrabTarget = _hoverGrabTarget;
                _hoverGrabTarget = null;
                _grabbing = true;
                _anim.SetBool("grabbing", true);
                _anim.SetBool("hoveringGrab", false);
            }
        }
        else
        {
            if (_heldGrabTarget != null)
            {
                _grabbing = false;
                _heldGrabTarget.OnRelease();
                _heldGrabTarget = null;
                _hoverGrabTarget = null;

                _anim.SetBool("grabbing", false);
            }
            else if (_interactableTarget != null)
            {
                _interactableTarget.OnRelease();
            }
        }
    }
    public void ForceRelease()
    {
        if (_heldGrabTarget != null)
        {
            _heldGrabTarget.OnRelease();

            _grabbing = false;
            _heldGrabTarget = null;
            _hoverGrabTarget = null;

            _anim.SetBool("grabbing", false);
        }
        else if (_interactableTarget != null)
        {
            _interactableTarget.OnRelease();
        }
    }
}
