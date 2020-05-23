using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentClient
    {
        SyncFieldGroup<MobileParty, MovementData> TargetPosition { get; }
        SyncField<Campaign, CampaignTimeControlMode> TimeControlMode { get; }

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
        #endregion
    }
}
