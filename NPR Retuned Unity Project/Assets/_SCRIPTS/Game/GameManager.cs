using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public enum GameState
{
    None,
    Title,
    Talking,
    Shop,
    Level,
    Debt
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
    public bool Paused
    {
        get => _paused;
        set
        {
            OnPauseSwitch?.Invoke(value);

            _paused = value;
        }
    }
    [SerializeField] private bool _paused;
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
    public Action<bool> OnPauseSwitch;
    public Action<GameState> OnGStateSwitch;
    public Action<PlayerState> OnPStateSwitch;
    public void ClearActions()
    {
        OnGStateSwitch = new Action<GameState>(_ => { });
        OnPStateSwitch = new Action<PlayerState>(_ => { });
    }
    public void TriggerAfterLevelDialogue()
    {
        PlayerStats.root.Runs++;

        /*if (PlayerStats.root.Runs == 1) StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Talking));
        else*/ StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Shop));
    }
}
