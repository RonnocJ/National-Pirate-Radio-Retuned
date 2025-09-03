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
    public float fValue;
    public Vector2 v2Value;
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
    Action,
    Switch,
    Reload,
    Interact
}
public class PInputManager : Singleton<PInputManager>
{
    public Action OnRegisterInputs;
    [SerializeField] private InputActionAsset inputAsset;
    public Dictionary<PlayerActionType, PlayerAction> actions = new Dictionary<PlayerActionType, PlayerAction>();

    protected override void Awake()
    {
        base.Awake();
        inputAsset.Enable();

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
                    case PlayerActionType.Interact:
                        actions[actionType] = new PlayerAction(action, ActionValueType.Button);
                        break;
                    case PlayerActionType.Action:
                        actions[actionType] = new PlayerAction(action, ActionValueType.Button | ActionValueType.Float);
                        break;
                }
            }
        }

        OnRegisterInputs?.Invoke();
    }

    void OnDestroy()
    {
        foreach (var action in actions.Values)
        {
            action.action.Disable();
        }

        inputAsset.Disable();
    }
}
