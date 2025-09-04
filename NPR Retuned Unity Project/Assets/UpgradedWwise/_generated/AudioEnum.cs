

/// <summary>
///   The list of events in the game.
/// </summary>
public enum AudioEvent {
    None = 0,
    playBanditDie = 1497679377,
    playBanditHey = 760155893,
    playBanditKill = 292190593,
    playBanditShoot = 1811505402,
    playBanditSpit = -734090321,
    playMegaZombieAttack = 900348891,
    playMegaZombieIdle = 1955906517,
    playMegaZombieKill = 843734399,
    playSkeletonDie = 829146376,
    playSkeletonIdle = 1497265408,
    playZombieDie = 503516373,
    playZombieIdle = -1384849129,
    playZombieKill = -162312403,
    playRoaming = 822197136,
    pauseSFX = -911537186,
    playAnvilHit = 1308629318,
    playArrowClick = 217988234,
    playBlackhole = -1735339942,
    playButtonBack = -579272654,
    playButtonClick = -1188987503,
    playButtonHeavyClick = 136873084,
    playButtonHover = 1134229243,
    playButtonWrong = -1468453450,
    playChestOpen = 2112865866,
    playClick = 2002511053,
    playCyborgKill = 1073312507,
    playCyborgShootRocket = -751998776,
    playCyborgThrowTrap = -105651344,
    playEnergyShield = 1905145288,
    playHealComplete = -142615936,
    playHealInProgress = -1616422239,
    playItemGet = 1707287486,
    playLaserEyes = -74506366,
    playMetalCollide = 2042833190,
    playQuestComplete = -428725658,
    playRadiationDie = 1690082298,
    playRingPlatformPress = -1196809887,
    playScrapHit = -122098519,
    playScrapKill = -1311746518,
    playScrapLand = -798618783,
    playStoneRoll = -23352305,
    playTakeDamage = -1513414215,
    playThump = 1649950571,
    playTirePop = 57832890,
    playWoodCollide = -1928974518,
    resumeSFX = -1218885327,
    stopAllSFX = 658567367,
    playStartingCity = 1312030930,
    playStartingCityStrike = 1024408708,
    playTitle = -701164261,
    playGateOpen = -699890442,
    playGateRing = 1230537104,
    playAcidSprayerBullet = 315461342,
    playAcidSprayerFull = -142070939,
    playAcidSprayerLoop = 877208516,
    playAcidSprayerRelease = 1625026149,
    playBrakeSqueal = -948076511,
    playCarHorn = -1225195028,
    playClimbLadder = -910535970,
    playDie = 1836766147,
    playDoorClose = -1976645275,
    playDrift = 1709085828,
    playDriverChange = 1358724929,
    playEMPPulserBullet = 1482356572,
    playExhaustBurst = -289399437,
    playFlamethrowerFull = -1407660506,
    playFlamethrowerLoop = -313940529,
    playFlamethrowerRelease = -1540081020,
    playFootstep = 1712852617,
    playFrostCannonBullet = -1570746420,
    playMachineGunBullet = -473125340,
    playMachineGunImpact = 1250273058,
    playMissileFly = 1559470536,
    playMissileLaunch = -241898116,
    playMissleExplode = -6091495,
    playMoneyCollected = -1690436346,
    startCarSounds = -776596421,
    stopBrakeSqueal = -1500809525,
    test = -1137964055,
}

/// <summary>
///   The list of states in the game.
/// </summary>
public enum AudioState {
    None = 0,
    Game_BREAK_None = 748895195,
    Game_BREAK_BossCyborg = 962934538,
    Game_BREAK_Outpost = -1831521263,
    Game_BREAK_Roaming = -737330534,
    Game_BREAK_Success = -669906570,
    Game_BREAK_Failure = -416127523,
    MainMenu_BREAK_None = 748895196,
    MainMenu_BREAK_PreTitle = -2051311462,
    MainMenu_BREAK_CharacterSelect = -625726166,
    MainMenu_BREAK_Title = -589240787,
}

/// <summary>
///   The list of switches in the game.
/// </summary>
public enum AudioSwitch {
    None = 0,
    CarSurface_BREAK_Sand = 803837735,
    WalkingSurface_BREAK_Sand = 803837735,
    WalkingSurface_BREAK_Taxi = -1457327311,
}

/// <summary>
///   The list of triggers in the game.
/// </summary>
public enum AudioTrigger {
    None = 0,
    ToActionTrigger = 342415772,
    OutpostActionTrigger = 1051770733,
    ActionOutpostTrigger = -1381899923,
}

/// <summary>
///   The list of rtpcs in the game.
/// </summary>
public enum AudioRTPC {
    None = 0,
    combatIntensity = 1253610732,
    isPaused = -1851172977,
    arrowClickPitch = 2012701150,
    distanceToGate = -590221742,
    carThrottle = 239056309,
    brakeIntensity = 271504097,
    dialogueVolumeSlider = 1051684938,
    mainVolumeSlider = 1524938871,
    soundEffectsVolumeSlider = 2058964397,
    uiVolumeSlider = -951775182,
    climbSpeed = -572499087,
    carSpeed = -351554426,
    musicVolumeSlider = -313008485,
    missilePitch = -240312593,
}

/// <summary>
///   The list of soundbanks in the game.
/// </summary>
public enum AudioSoundbank {
    EnemySFX = 406817756,
    OutsideMusic = 419494115,
    PersistentSounds = 574275146,
    StartingCityMusic = 1005620997,
    Init = 1355168291,
    TitleMusic = -2103807666,
    CyborgBoss = -1360608522,
    WormBoss = -1246748909,
    EnvironmentalSFX = -1227728876,
    RoamingMusic = -370597129,
    PlayerSFX = -11709925,
}