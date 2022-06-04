using System;
using System.Linq;
using Coop.Mod.Persistence;
using RailgunNet;
using RailgunNet.Util;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    public static class Coop
    {
        public static Guid InvalidId = Guid.Empty;
        public static bool IsServer => CoopServer.Instance.Current != null;
        public static bool IsClientConnected => CoopClient.Instance.ClientConnected;

        public static bool IsClientInGame => CoopClient.Instance.ClientPlaying;

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

            if (party == null)
            {
                return false;
            }

            if (IsAnyPlayerMainParty(party))
            {
                // Player parties can only be controlled by the owner.
                return party == MobileParty.MainParty;
            }

            // Every other party is controlled by the arbiter.
            return IsArbiter;
        }
        /// <summary>
        ///     Returns whether the given <see cref="MobileParty"/> is the main party of any player, remote or local.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsAnyPlayerMainParty(MobileParty party)
        {
            if (IsClientConnected)
            {
                IEnvironmentClient env = CoopClient.Instance?.Persistence?.Environment;
                if (env != null)
                {
                    return env.PlayerMainParties.Contains(party);
                }
            }
            else if (IsServer)
            {
                MobilePartyEntityManager manager = CoopServer.Instance?.Persistence?.MobilePartyEntityManager;
                if (manager != null)
                {
                    return manager.IsControlledByClient(party);
                }
            }

            // Coop is not running -> only the main party is controlled by the player.
            return party == MobileParty.MainParty;
        }
        /// <summary>
        ///     Returns whether the given <see cref="MobileParty"/> is the main party of the local game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsLocalPlayerMainParty(MobileParty party)
        {
            return IsAnyPlayerMainParty(party) && party == MobileParty.MainParty;
        }
        /// <summary>
        ///     Returns whether the given <see cref="MobileParty"/> is the main party of a remote game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsRemotePlayerMainParty(MobileParty party)
        {
            return IsAnyPlayerMainParty(party) && party != MobileParty.MainParty;
        }
    }
}
