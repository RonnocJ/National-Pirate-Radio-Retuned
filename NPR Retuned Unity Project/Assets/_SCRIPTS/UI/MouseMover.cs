using UnityEngine;

public class MouseMover : MonoBehaviour
{
    private bool _mouseActive;
    private Vector2 _mousePos => PInputManager.root.actions[PlayerActionType.Look].v2Value;
    private Camera _mainCam;
    void Start()
    {
        GameManager.root.OnPStateSwitch += ChangeMouse;
        _mainCam = Camera.main;
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

        Ray ray = _mainCam.ScreenPointToRay(_mousePos);
    }
}
