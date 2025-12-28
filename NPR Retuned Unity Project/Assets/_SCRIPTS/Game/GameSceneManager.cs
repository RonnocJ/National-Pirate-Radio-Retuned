using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : Singleton<GameSceneManager>
{
    public string LastLoadedScene;
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

                    if (LastLoadedScene == "Title") break;
                    LastLoadedScene = "Title";

                    SoundbankManager.root.UnloadAll();

                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.Title);
                    AudioManager.root.PlaySound(AudioEvent.playTitleMusic);

                    GameManager.root.CurrentGState = GameState.Title;

                    break;

                case "Talk":

                    if (LastLoadedScene == "Talk") break;

                    AudioManager.root.StopSound(AudioEvent.playTitleMusic);
                    SoundbankManager.root.UnloadAll();

                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.Dialogue);

                    NonDgUI.root.toTalkPanel.anchoredPosition = Vector2.right * -2560;
                    GameManager.root.CurrentGState = GameState.Talking;

                    if (PlayerStats.root.NewGame) DialoguePlayer.root.PlayFromResources("GameIntro/newGame", "mono", -1, DialoguePlayer.root.ToTitle);
                    else
                    {
                        switch (LastLoadedScene)
                        {
                            case "Shop":

                                DialoguePlayer.root.PlayDialogue(LevelIntro.introStage1);
                                break;

                            case "Debt":

                                DialoguePlayer.root.PlayDialogue(AfterLevel.afterFirstDrive);
                                break;
                            default:
                                break;

                        }
                    }

                    LastLoadedScene = "Talk";

                    break;

                case "Shop":

                    if (LastLoadedScene == "Shop") break;
                    LastLoadedScene = "Shop";

                    AudioManager.root.StopSound(AudioEvent.playTitleMusic);
                    SoundbankManager.root.UnloadAll();

                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.Shop);

                    NonDgUI.root.StartCoroutine(NonDgUI.root.FadeToBlack(false));
                    GameManager.root.CurrentGState = GameState.Shop;

                    break;

                case "Level":

                    if (LastLoadedScene == "Level") break;
                    LastLoadedScene = "Level";

                    AudioManager.root.StopSound(AudioEvent.playTitleMusic);
                    SoundbankManager.root.UnloadAll();

                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.LevelSFX);
                    SoundbankManager.root.LoadSoundbank(AudioSoundbank.VanSFX);

                    GameManager.root.CurrentGState = GameState.Level;
                    GameManager.root.CurrentPState = PlayerState.Start;

                    if (PlayerStats.root.NewGame)
                    {
                        NonDgUI.root.StartCoroutine(NonDgUI.root.ShowIntroQuotes());
                    }

                    break;

                case "Debt":
                NonDgUI.root.StartCoroutine(NonDgUI.root.FadeToBlack(false));
                    break;

                default:

                    if (PlayerStats.root.NewGame) LoadTalk();
                    else LoadTitle();

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
    public void LoadDebt()
    {
        SceneManager.LoadSceneAsync("Debt", LoadSceneMode.Single);
    }
}