using Coop.Mod.Persistence.RPC;
using JetBrains.Annotations;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentServer
    {
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        bool CanChangeTimeControlMode { get; }

        /// <summary>
        ///     Returns the large object store for this server.
        /// </summary>
        [NotNull]
        SharedRemoteStore Store { get; }

        /// <summary>
        ///     Returns the queue to broadcast events to all clients. NotNull if persistence is initialized.
        /// </summary>
        [CanBeNull]
        EventBroadcastingQueue EventQueue { get; }

        [CanBeNull]
        MobileParty GetMobilePartyByIndex(int iPartyIndex);

        Campaign GetCurrentCampaign();
    }
}
