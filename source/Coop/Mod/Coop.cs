using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public static class Coop
    {
        public static bool IsServer => CoopServer.Instance.Current != null;
        public static bool IsClientConnected => CoopClient.Instance.ClientConnected;

        /// <summary>
        ///     The arbiter is the game instance with authority over all other clients.
        /// </summary>
        public static bool IsArbiter => IsServer && IsClientConnected; // The server game session is connected as a client as well!

        /// <summary>
        ///     If the active game session is a coop game that should be synchronized between all clients.
        /// </summary>
        /// <returns></returns>
        public static bool IsCoopGameSession()
        {
            return IsClientConnected || IsServer;
        }
        
        /// <summary>
        ///     Return whether the given MobileParty can be controlled by this game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsController(MobileParty party)
        {
            if (!IsCoopGameSession())
            {
                return true;
            }
            
            bool isPlayerControlled = CoopClient.Instance.GameState.IsPlayerControlledParty(party);
            if (isPlayerControlled && party == MobileParty.MainParty)
            {
                // Main party of the local client
                return true;
            }

            // Every other party is controlled by the arbiter.
            return IsArbiter;
        }
    }
}
