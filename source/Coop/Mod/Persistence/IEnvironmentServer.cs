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

        [NotNull] SharedRemoteStore Store { get; }

        [CanBeNull]
        MobileParty GetMobilePartyByIndex(int iPartyIndex);

        Campaign GetCurrentCampaign();
    }
}
