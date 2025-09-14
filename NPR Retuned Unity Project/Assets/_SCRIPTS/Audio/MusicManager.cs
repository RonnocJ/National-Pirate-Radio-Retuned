using System;

public class MusicManager : Singleton<MusicManager>
{
    void Start()
    {
        AudioManager.root.PlaySound(AudioEvent.playCDMusic, gameObject);
        AudioManager.root.SetSwitch(AudioSwitch.NowPlaying_BREAK_Static, gameObject);
    }

    public void SwitchSong(SongName newSong)
    {
        if (Enum.TryParse(typeof(AudioSwitch), "NowPlaying_BREAK_" + newSong.ToString(), out var songToPlay))
        {
            AudioManager.root.SetSwitch((AudioSwitch)songToPlay, gameObject);
        }
    }
}