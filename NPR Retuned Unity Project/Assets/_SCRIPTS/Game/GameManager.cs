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
    public Action<GameState> OnGStateSwitch;
    public Action<PlayerState> OnPStateSwitch;
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