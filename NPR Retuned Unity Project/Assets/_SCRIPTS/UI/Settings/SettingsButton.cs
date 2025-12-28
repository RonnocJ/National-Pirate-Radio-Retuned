using UnityEngine;

public class SettingsButton : SettingsEntry
{
    public enum SettingsButtonType
    {
        Resume,
        EndRun,
    }
    [SerializeField] private SettingsButtonType buttonType;
    [SerializeField] private SettingsManager s;
    void Start()
    {
       PInputManager.root.actions[PlayerActionType.Find].bAction += ActivateButton; 
    }
    protected override void Update()
    {
        base.Update();
    }
    private void ActivateButton()
    {
        if(!Highlighted || !GameManager.root.Paused) return;

        switch (buttonType)
        {
            case SettingsButtonType.Resume:
                s.TogglePause();
                break;
            case SettingsButtonType.EndRun:
                VanDamage.root.OnPlayerDie?.Invoke();
                AudioManager.root.PlaySound(AudioEvent.stopAll);
                StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Shop));
                s.TogglePause();
                break;
        }
    }
}