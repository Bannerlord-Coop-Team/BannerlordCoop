using JetBrains.Annotations;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentClient
    {
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; }
        FieldAccess<Campaign, bool> TimeControlModeLock { get; }
        CampaignTime AuthoritativeTime { get; set; }

        [NotNull] RemoteStore Store { get; }

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
        #endregion
    }
}
