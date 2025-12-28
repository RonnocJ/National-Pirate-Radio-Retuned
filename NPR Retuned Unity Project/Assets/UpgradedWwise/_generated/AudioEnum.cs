

/// <summary>
///   The list of events in the game.
/// </summary>
public enum AudioEvent {
    None = 0,
    playPinboardEject = 578532127,
    playPinboardInsert = 1197348043,
    playCDMusic = -542775629,
    setStatic = 185187129,
    pauseAll = -203920114,
    playMouseClick = 436537884,
    resumeAll = -1054066427,
    stopAll = -1208426410,
    playATVAttack = 113422346,
    playATVBlast = 1438338272,
    playCDPlayerClose = 1736359555,
    playCDPlayerOpen = -2011959819,
    playDroneExplosion = 1793089900,
    playDroneHoverLoop = 1561607685,
    playDroneShoot = -1061889172,
    playMagnetExplode = 580489224,
    playMagnetFloat = 276583697,
    playRotorLoop = -1579925133,
    playTTSVoice = 1507170084,
    playWindAmbience = 628329353,
    playTireSqueal = -292689192,
    playTrumpetFireFull = 549296393,
    playVanEngine = -1864301904,
    playTitleMusic = 718394488,
    playGameIntro = -1402311191,
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
    NowPlaying_BREAK_LIH = 444705608,
    NowPlaying_BREAK_SOW = 544238342,
    NowPlaying_BREAK_NPR = 662417179,
    NowPlaying_BREAK_BRD = 765054347,
    NowPlaying_BREAK_EVG = 932389263,
    NowPlaying_BREAK_FUG = 982472045,
    NowPlaying_BREAK_WLZ = 1030097862,
    NowPlaying_BREAK_Static = 1409504247,
    Attack_BREAK_Releasing = 1007009725,
    Attack_BREAK_Charging = -466345064,
    Engine_BREAK_Started = -1496307084,
    Engine_BREAK_Stopped = -1390170220,
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
    Music_Volume = 1006694123,
    Player_Speed = 1062779386,
    Engine_RPM = 1130155893,
    Engine_Gear = 1358796823,
    Engine_Rev = 1482485841,
    SFX_Volume = 1564184899,
    Main_Volume = -1982795281,
    WheelFriction = -1817479220,
    Engine_Throttle = -1567782982,
    Drone_Distance = -413525723,
    TTS_Character = -91836324,
}

/// <summary>
///   The list of soundbanks in the game.
/// </summary>
public enum AudioSoundbank {
    Shop = 251412225,
    LIH = 444705608,
    SOW = 544238342,
    BRD = 765054347,
    EVG = 932389263,
    FUG = 982472045,
    WLZ = 1030097862,
    Init = 1355168291,
    Global = 1465331116,
    LevelSFX = 1900718590,
    VanSFX = 1939123861,
    Title = -589240787,
    Dialogue = -364830561,
}