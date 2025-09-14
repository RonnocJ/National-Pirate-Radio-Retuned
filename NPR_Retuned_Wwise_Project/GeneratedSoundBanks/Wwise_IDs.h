/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID PLAYCDMUSIC = 3752191667U;
        static const AkUniqueID PLAYCDPLAYERCLOSE = 1736359555U;
        static const AkUniqueID PLAYCDPLAYEROPEN = 2283007477U;
        static const AkUniqueID PLAYDRONEEXPLOSION = 1793089900U;
        static const AkUniqueID PLAYDRONEHOVERLOOP = 1561607685U;
        static const AkUniqueID PLAYDRONESHOOT = 3233078124U;
        static const AkUniqueID PLAYROTORLOOP = 2715042163U;
        static const AkUniqueID PLAYTTSVOICE = 1507170084U;
        static const AkUniqueID PLAYVANENGINE = 2430665392U;
        static const AkUniqueID PLAYWINDAMBIENCE = 628329353U;
        static const AkUniqueID SETSTATIC = 185187129U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace ID_JOETOOLS
        {
            static const AkUniqueID GROUP = 200428098U;

            namespace STATE
            {
                static const AkUniqueID HAPPY = 1427264549U;
                static const AkUniqueID NEUTRAL = 670611050U;
                static const AkUniqueID NONE = 748895195U;
            } // namespace STATE
        } // namespace ID_JOETOOLS

    } // namespace STATES

    namespace SWITCHES
    {
        namespace ENGINE
        {
            static const AkUniqueID GROUP = 268529915U;

            namespace SWITCH
            {
                static const AkUniqueID STARTED = 2798660212U;
                static const AkUniqueID STOPPED = 2904797076U;
            } // namespace SWITCH
        } // namespace ENGINE

        namespace NOWPLAYING
        {
            static const AkUniqueID GROUP = 390902339U;

            namespace SWITCH
            {
                static const AkUniqueID EVG = 932389263U;
                static const AkUniqueID NPR = 662417179U;
                static const AkUniqueID STATIC = 1409504247U;
            } // namespace SWITCH
        } // namespace NOWPLAYING

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID DRONE_DISTANCE = 3881441573U;
        static const AkUniqueID ENGINE_GEAR = 1358796823U;
        static const AkUniqueID ENGINE_RPM = 1130155893U;
        static const AkUniqueID ENGINE_THROTTLE = 2727184314U;
        static const AkUniqueID PLAYER_SPEED = 1062779386U;
        static const AkUniqueID TTS_CHARACTER = 4203130972U;
    } // namespace GAME_PARAMETERS

    namespace BANKS
    {
        static const AkUniqueID INIT = 1355168291U;
        static const AkUniqueID MAIN = 3161908922U;
    } // namespace BANKS

    namespace BUSSES
    {
        static const AkUniqueID GLOBALBUS = 2629241110U;
        static const AkUniqueID MASTER_AUDIO_BUS = 3803692087U;
        static const AkUniqueID MUSICBUS = 2886307548U;
        static const AkUniqueID SFXBUS = 3803850708U;
    } // namespace BUSSES

    namespace AUX_BUSSES
    {
        static const AkUniqueID ENEMYVERB = 3390657128U;
        static const AkUniqueID VANVERB = 3347336007U;
    } // namespace AUX_BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
