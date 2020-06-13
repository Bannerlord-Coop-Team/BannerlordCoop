using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public static class Coop
    {
        public static bool IsServer => CoopServer.Instance.Current != null;
        public static bool IsClient => CoopClient.Instance.Connected;

        /// <summary>
        ///     The arbiter is the game instance with authority over all clients.
        /// </summary>
        public static bool IsArbiter =>
            IsServer && IsClient; // The server currently runs in the hosts game session.

        public static bool DoSync()
        {
            return IsClient || IsServer;
        }

        /// <summary>
        ///     Returns whether changes to an object should be synchronized.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static bool DoSync(object instance)
        {
            if (instance is MobileParty party)
            {
                return DoSync(party);
            }

            return IsArbiter;
        }

        public static bool DoSync(MobileParty party)
        {
            bool isPlayerController = CoopClient.Instance.GameState.IsPlayerControlledParty(party);
            if (isPlayerController && party == MobileParty.MainParty)
            {
                return true;
            }

            if (IsArbiter && !isPlayerController)
            {
                return true;
            }

            return false;
        }
    }
}
