using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RemoteAction;
using JetBrains.Annotations;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Provides an abstraction layer between the persistence and the game for the server.
    /// </summary>
    public interface IEnvironmentServer
    {

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
        ///     Returns a party given its guid.
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        [CanBeNull]
        MobileParty GetMobilePartyById(MBGUID guid);

        /// <summary>
        ///     Gets the synchronization for <see cref="MobileParty"/> instances.
        /// </summary>
        MobilePartySync PartySync { get; }

        /// <summary>
        ///     Stops time and locks time control.
        /// </summary>
        void LockTimeControlStopped();

        /// <summary>
        ///     Unlocks time control.
        /// </summary>
        void UnlockTimeControl();
    }
}
