using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
[Flags]
public enum ActionValueType
{
    Vector2 = 1 << 0,
    Float = 1 << 1,
    Button = 1 << 2
}
public class PlayerAction
{
    public InputAction action;
    public Action bAction;
    private float _fValue;
    public float fValue
    {
        get => _fValue;
        set
        {
            if (value != _fValue)
            {
                onFValueChange?.Invoke(value);
                _fValue = value;
            }
        }
    }
    public Action<float> onFValueChange;
    private Vector2 _v2Value;
    public Vector2 v2Value
    {
        get => _v2Value;
        set
        {
            if (value != _v2Value)
            {
                onV2ValueChange?.Invoke(value);
                _v2Value = value;
            }
        }
    }
    public Action<Vector2> onV2ValueChange;
    public PlayerAction(InputAction action, ActionValueType type)
    {
        this.action = action;

        if ((type & ActionValueType.Button) != 0)
        {
            bAction = null;
            action.performed += ctx => bAction?.Invoke();
        }
        if ((type & ActionValueType.Float) != 0)
        {
            fValue = 0f;
            action.performed += ctx => fValue = ctx.ReadValue<float>();
            action.canceled += ctx => fValue = 0f;
        }
        if ((type & ActionValueType.Vector2) != 0)
        {
            v2Value = Vector2.zero;
            action.performed += ctx => v2Value = ctx.ReadValue<Vector2>();
            action.canceled += ctx => v2Value = Vector2.zero;
        }
    }
}
public enum PlayerActionType
{
    Drive,
    Look,
    Brake,
    Ability,
    Find,
    Action,
    Switch,
    Reload,
}
public class PInputManager : Singleton<PInputManager>
{
    [SerializeField] private InputActionAsset inputAsset;
    public Dictionary<PlayerActionType, PlayerAction> actions = new Dictionary<PlayerActionType, PlayerAction>();
    public void ClearActions()
    {
        foreach (var action in actions.Values)
        {
            action.bAction = new Action(() => { });
            action.onFValueChange = new Action<float>(_ => { });
            action.onV2ValueChange = new Action<Vector2>(_ => {});
        }
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        if (inputAsset != null && !inputAsset.enabled)
        {
            inputAsset.Enable();
        }

        foreach (var type in Enum.GetValues(typeof(PlayerActionType)))
        {
            PlayerActionType actionType = (PlayerActionType)type;
            InputAction action = inputAsset.FindActionMap("Player").FindAction(actionType.ToString());

            if (action != null)
            {
                switch (actionType)
                {
                    case PlayerActionType.Drive:
                    case PlayerActionType.Look:
                    case PlayerActionType.Ability:
                        actions[actionType] = new PlayerAction(action, ActionValueType.Vector2);
                        break;
                    case PlayerActionType.Brake:
                        actions[actionType] = new PlayerAction(action, ActionValueType.Float);
                        break;
                    case PlayerActionType.Reload:
                    case PlayerActionType.Switch:
                        actions[actionType] = new PlayerAction(action, ActionValueType.Button);
                        break;
                    case PlayerActionType.Action:
                    case PlayerActionType.Find:
                        actions[actionType] = new PlayerAction(action, ActionValueType.Button | ActionValueType.Float);
                        break;
                }
            }
        }
    }
}
