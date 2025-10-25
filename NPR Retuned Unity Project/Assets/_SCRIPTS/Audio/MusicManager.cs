using System;

public class MusicManager : Singleton<MusicManager>
{
    void Start()
    {
        AudioManager.root.PlaySound(AudioEvent.playCDMusic, gameObject);
        AudioManager.root.SetSwitch(AudioSwitch.NowPlaying_BREAK_Static, gameObject);

        VanDamage.root.OnPlayerDie += SetStatic;
    }

    public void SwitchSong(SongName newSong)
    {
        if (Enum.TryParse(typeof(AudioSwitch), "NowPlaying_BREAK_" + newSong.ToString(), out var songToPlay))
        {
            AudioManager.root.SetSwitch((AudioSwitch)songToPlay, gameObject);
        }
    }
    public void SetStatic()
    {
        AudioManager.root.SetSwitch(AudioSwitch.NowPlaying_BREAK_Static, gameObject);
    }
}