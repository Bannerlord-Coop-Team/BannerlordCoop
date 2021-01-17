using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.MethodCall;
using JetBrains.Annotations;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace RemoteAction
{
    /// <summary>
    ///     Provides an abstraction layer between the persistence and the game for the server.
    /// </summary>
    public interface IEnvironmentServer
    {
        /// <summary>
        ///     Access to the movement data for all parties in the clients game world.
        /// </summary>
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }

        /// <summary>
        ///     Determines if the campaign time control mode could be changed right now.
        /// </summary>
        bool CanChangeTimeControlMode { get; }

        /// <summary>
        ///     Returns the shared object store for this server.
        /// </summary>
        [NotNull]
        SharedRemoteStore Store { get; }

        /// <summary>
        ///     Returns the queue to broadcast events to all clients. NotNull if persistence is initialized.
        /// </summary>
        [CanBeNull]
        EventBroadcastingQueue EventQueue { get; }

        /// <summary>
        ///     Returns a party given its party index.
        /// </summary>
        /// <param name="iPartyIndex"></param>
        /// <returns></returns>
        [CanBeNull]
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
    }
}
