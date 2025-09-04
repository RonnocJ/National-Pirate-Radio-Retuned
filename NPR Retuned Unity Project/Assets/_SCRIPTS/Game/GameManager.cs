using System;
using Unity.VisualScripting;
using UnityEngine;
public enum GameState
{
    Title,
    Talking,
    Level
}
public enum PlayerState
{
    Start = 0,
    Utility = 1,
    Weapon = 2,
    Dead = 3
}
[Flags]
public enum DiscState
{
    Empty = 0,
    Finding = 1,
    Handling = 2,
    Playing = 3
}
public class GameManager : Singleton<GameManager>
{
    [SerializeField] private GameState _currentGState;
    public GameState CurrentGState
    {
        get => _currentGState;
        set
        {
            if (value != _currentGState)
            {
                OnGStateSwitch?.Invoke(value);

                _currentGState = value;
            }
        }
    }
    [SerializeField] private PlayerState _currentPState;
    public PlayerState CurrentPState
    {
        get => _currentPState;
        set
        {
            if (value != _currentPState)
            {
                OnPStateSwitch?.Invoke(value);

                _currentPState = value;
            }
        }
    }
    [SerializeField] private DiscState _currentDState;
    public DiscState CurrentDState
    {
        get => _currentDState;
        set
        {
            if (value != _currentDState)
            {
                OnDStateSwitch?.Invoke(value);

                _currentDState = value;
            }
        }
    }
    public Action<GameState> OnGStateSwitch;
    public Action<PlayerState> OnPStateSwitch;
    public Action<DiscState> OnDStateSwitch;
    void Start()
    {
        PInputManager.root.actions[PlayerActionType.Switch].bAction += SwitchVanMode;
    }
    private void SwitchVanMode()
    {
        if (CurrentPState is PlayerState.Utility or PlayerState.Weapon)
        {
            CurrentPState = (PlayerState)(-(int)CurrentPState + 3);
        }
    }
}