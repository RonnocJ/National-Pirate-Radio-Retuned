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
    Level
}
public enum PlayerState
{
    Start = 0,
    Utility = 1,
    Weapon = 2,
    Dead = 3
}
public class GameManager : Singleton<GameManager>, ISaveData
{
    public bool NewGame = true;
    public int Runs = 0;
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
    public void ClearActions()
    {
        OnGStateSwitch = new Action<GameState>(_ => { });
        OnPStateSwitch = new Action<PlayerState>(_ => { });
    }

    public Dictionary<string, object> AddSaveData()
    {
        return new Dictionary<string, object>()
        {
            { "newGame", NewGame },

            { "runs", Runs }
        };
    }
    public void ReadSaveData(Dictionary<string, object> dataDict)
    {
        if (dataDict.TryGetValue("newGame", out object newGame))
        {
            NewGame = Convert.ToBoolean(newGame);
        }

        if (dataDict.TryGetValue("runs", out object runs))
        {
            Runs = Convert.ToInt32(runs);
        }
    }
    public void TriggerAfterLevelDialogue()
    {
        Runs++;

        if (Runs == 1) StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Talking));
        else StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Shop));
    }
}
