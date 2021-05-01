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

        /*
         * This constructor should basically only be used for testing. 
        * Since the services are retrieved during runtime from TaleWorlds' PlatformService (other than in unit tests),
        * use PlatformAPI() to make a PlatformAPI with the retrieved services from the current platform.
        */
        public PlatformAPI(IFriendListService friendListService, IPlatformServices platformServices)
        {
            this.friendListService = friendListService;
            this.platformServices = platformServices;
        }

        public PlatformAPI()
        {
            platformServices = PlatformServices.Instance;
            if(platformServices == null)
            {
                string errorMessage = "PlatformAPI: Failed to retrieve instance of PlatformServices.";
                Logger.Error(errorMessage);
                throw new ArgumentNullException("platformServices", errorMessage);
            }

            bool platformInitialized = platformServices.Initialize(new IFriendListService[0]);

            if(!platformInitialized)
            {
                string errorMessage = "PlatformAPI: Failed to initialize platformServices.";
                Logger.Error(errorMessage);
                throw new Exception(errorMessage);
            }

            IFriendListService[] friendListServices = platformServices.GetFriendListServices();
            
            if(friendListServices == null || friendListServices.Length == 0)
            {
                string errorMessage = "PlatformAPI: Failed to retrieve friend list services from PlatformServices.";
                Logger.Error(errorMessage);
                throw new Exception(errorMessage);
            }

            friendListService = friendListServices[0];
        }

        public string GetPlayerName()
        {
            PlayerId playerId = GetPlayerID();
            string playerName = playerId.Equals(PlayerId.Empty) ? "Player" : friendListService.GetUserName(playerId).Result;

            return playerName;
        }

        //TaleWorlds' IPlatformServices interface doesn't have a way to get the current user's name other than
        //through the GetUserName(PlayerId playerID) method, so we need a way to get the user's PlayerId.
        //IPlatformServices.UserId contains a platform-specific UserId that doesn't easily convert into a PlayerId,
        //so this method is needed to convert the User ID to the PlayerId depending on the platform.
        public PlayerId GetPlayerID()
        {
            string currentPlatformName = friendListService.GetServiceName();
            PlayerId playerId = PlayerId.Empty;

            string userIdString = platformServices.UserId;

            switch (currentPlatformName)
            {
                case "Steam":
                    ulong userIdUlong = Convert.ToUInt64(userIdString);
                    CSteamID steamId = new CSteamID(userIdUlong);
                    playerId = SteamPlayerIdExtensions.ToPlayerId(steamId);
                    break;
                //TODO: uncomment this once we can get GalaxyCSharpGlue.dll referenced properly
                //case "GOG":
                //    userIdUlong = Convert.ToUInt64(userIdString);
                //    GalaxyID galaxyId = new GalaxyID(userIdUlong);
                //    playerId = TaleWorlds.PlatformService.GOG.SteamPlayerIdExtensions.ToPlayerId(galaxyId);
                //    break;
                //TODO: uncomment this once we can get EOSSDK.dll referenced properly
                //case "Epic":
                //    EpicAccountId epicId = EpicAccountId.FromString(userIdString);

                //    //I just copy pasted the implementation TaleWorlds.PlatformService.Epic.EpicPlatformService because the method EpicAccountIdToPlayerId is private
                //    //EpicAccountIdToPlayerId(epicId)
                //    StringBuilder stringBuilder = new StringBuilder(64);
                //    int capacity = stringBuilder.Capacity;
                //    epicId.ToString(stringBuilder, ref capacity);
                //    playerId = new PlayerId(3, stringBuilder.ToString());
                //    break;
                default:
                    Logger.Warn("Attempted to retrieve player ID for unsupported platform.");
                    break;
            }

            return playerId;
        }

        /*
         * Returns the platform-specific User ID. For example, if the user is playing through Steam,
         * this should be the steamID64 of the user, e.g. 76561198092541763. See https://developer.valvesoftware.com/wiki/SteamID
         * NOTE: This is not a PlayerId. GetPlayerID() returns a PlayerId object that 
         * many of the methods in TaleWorlds' PlatformServices use. 
         */
        public string GetUserID()
        {
            return platformServices.UserId;
        }
    }
}
