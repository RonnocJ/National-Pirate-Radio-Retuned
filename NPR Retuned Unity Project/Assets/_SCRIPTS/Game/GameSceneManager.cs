using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : Singleton<GameSceneManager>
{
    protected override void OnEnable()
    {
        base.OnEnable();

        SceneManager.sceneLoaded += (sc, _) =>
        {
            PInputManager.root.ClearActions();
            GameManager.root.ClearActions();

            switch (sc.name)
            {
                case "Title":

                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.Title);
                    AudioManager.root.PlaySound(AudioEvent.playTitleMusic);

                    GameManager.root.CurrentGState = GameState.Title;
                    break;
                case "Talk":

                    AudioManager.root.StopSound(AudioEvent.playTitleMusic);
                    SoundbankManager.root.UnloadSoundbank(AudioSoundbank.Title);

                    NonDgUI.root.toTalkPanel.anchoredPosition = Vector2.right * -2560;
                    GameManager.root.CurrentGState = GameState.Talking;
                    break;
                case "Shop":

                    AudioManager.root.StopSound(AudioEvent.playTitleMusic);
                    SoundbankManager.root.UnloadAll();
                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.Shop);


                    NonDgUI.root.StartCoroutine(NonDgUI.root.FadeToBlack(false));
                    GameManager.root.CurrentGState = GameState.Shop;
                    break;
                case "Level":
                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.VanSFX);
                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.LevelSFX);
                    
                    AudioManager.root.StopSound(AudioEvent.playTitleMusic);
                    SoundbankManager.root.UnloadSoundbank(AudioSoundbank.Title);

                    GameManager.root.CurrentGState = GameState.Level;
                    GameManager.root.CurrentPState = PlayerState.Start;

                    if (GameManager.root.NewGame)
                    {
                        NonDgUI.root.StartCoroutine(NonDgUI.root.ShowIntroQuotes());
                    }
                    break;
            }
        };
    }
    public void LoadTitle()
    {
        SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
    }
    public void LoadTalk()
    {
        SceneManager.LoadSceneAsync("Talk", LoadSceneMode.Single);
    }
    public void LoadLevel()
    {
        SceneManager.LoadSceneAsync("Level", LoadSceneMode.Single);
    }
    public void LoadShop()
    {
        SceneManager.LoadSceneAsync("Shop", LoadSceneMode.Single);
    }
}