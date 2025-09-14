

/// <summary>
///   The list of events in the game.
/// </summary>
public enum AudioEvent {
    None = 0,
    playCDMusic = -542775629,
    playCDPlayerClose = 1736359555,
    playCDPlayerOpen = -2011959819,
    playDroneExplosion = 1793089900,
    playDroneHoverLoop = 1561607685,
    playDroneShoot = -1061889172,
    playRotorLoop = -1579925133,
    playTTSVoice = 1507170084,
    playVanEngine = -1864301904,
    playWindAmbience = 628329353,
}

/// <summary>
///   The list of states in the game.
/// </summary>
public enum AudioState {
    None = 0,
    ID_JoeTools_BREAK_Neutral = 670611050,
    ID_JoeTools_BREAK_None = 748895195,
    ID_JoeTools_BREAK_Happy = 1427264549,
}

/// <summary>
///   The list of switches in the game.
/// </summary>
public enum AudioSwitch {
    None = 0,
    Engine_BREAK_Started = -1496307084,
    Engine_BREAK_Stopped = -1390170220,
    NowPlaying_BREAK_NPR = 662417179,
    NowPlaying_BREAK_EVG = 932389263,
    NowPlaying_BREAK_Static = 1409504247,
}

/// <summary>
///   The list of triggers in the game.
/// </summary>
public enum AudioTrigger {
    None = 0,
}

/// <summary>
///   The list of rtpcs in the game.
/// </summary>
public enum AudioRTPC {
    None = 0,
    Player_Speed = 1062779386,
    Engine_RPM = 1130155893,
    Engine_Gear = 1358796823,
    Engine_Throttle = -1567782982,
    Drone_Distance = -413525723,
    TTS_Character = -91836324,
}

/// <summary>
///   The list of soundbanks in the game.
/// </summary>
public enum AudioSoundbank {
    Init = 1355168291,
    Main = -1133058374,
}