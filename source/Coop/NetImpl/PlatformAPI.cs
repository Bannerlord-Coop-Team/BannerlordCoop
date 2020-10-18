using Coop.NetImpl.Steam;
using Epic.OnlineServices;
using Galaxy.Api;
using Network.Infrastructure;
using NLog;
using Steamworks;
using System;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.PlatformService;
using TaleWorlds.PlatformService.Steam;
using TaleWorlds.PlayerServices;

namespace Coop.NetImpl
{
    //This is just here to make our lives easier when using TaleWorlds PlatformServices and extend them when needed
    public class PlatformAPI
    {
        private IFriendListService friendListService;
        private IPlatformServices platformServices;
        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

        //Public to make testing easier (inject mocks). Since the services are retrieved during runtime (other than in unit tests),
        //use PlatformAPI() to make a PlatformAPI with the retrieved services from the current platform.
        public PlatformAPI(IFriendListService friendListService, IPlatformServices platformServices)
        {
            this.friendListService = friendListService;
            this.platformServices = platformServices;
        }

        public PlatformAPI()
        {
            platformServices = PlatformServices.Instance;
            platformServices.Initialize(new IFriendListService[0]);
            friendListService = platformServices.GetFriendListServices()[0];
        }

        public string GetPlayerName()
        {
            PlayerId playerId = GetPlayerID();
            return friendListService.GetUserName(playerId).Result;
        }

        //TaleWorlds' IPlatformServices interface doesn't have a way to get the current user's name other than
        //through the GetUserName(PlayerId playerID) method, so we need a way to get the user's PlayerId.
        //IPlatformServices.UserId contains a platform-specific UserId that doesn't easily convert into a PlayerId,
        //so this method is needed to convert the User ID to the PlayerId depending on the platform.
        public PlayerId GetPlayerID()
        {
            //TODO: Add references to GOG and Epic APIs and implement the GOG and Epic PlayerID retrieval
            string currentPlatformName = friendListService.GetServiceName();
            PlayerId playerId = new PlayerId();

            string userIdString = GetUserID();

            switch (currentPlatformName)
            {
                case "Steam":
                    ulong userIdUlong = Convert.ToUInt64(userIdString);
                    CSteamID steamId = new CSteamID(userIdUlong);
                    playerId = SteamPlayerIdExtensions.ToPlayerId(steamId);
                    break;
                case "GOG":
                    userIdUlong = Convert.ToUInt64(userIdString);
                    GalaxyID galaxyId = new GalaxyID(userIdUlong);
                    playerId = TaleWorlds.PlatformService.GOG.SteamPlayerIdExtensions.ToPlayerId(galaxyId);
                    break;
                case "Epic":
                    EpicAccountId epicId = EpicAccountId.FromString(userIdString);

                    //I just copy pasted the implementation TaleWorlds.PlatformService.Epic.EpicPlatformService because the method EpicAccountIdToPlayerId is private
                    //EpicAccountIdToPlayerId(epicId)
                    StringBuilder stringBuilder = new StringBuilder(64);
                    int capacity = stringBuilder.Capacity;
                    epicId.ToString(stringBuilder, ref capacity);
                    playerId = new PlayerId(3, stringBuilder.ToString());
                    break;
            }

            return playerId;
        }

        public string GetUserID()
        {
            return platformServices.UserId;
        }
    }
}
