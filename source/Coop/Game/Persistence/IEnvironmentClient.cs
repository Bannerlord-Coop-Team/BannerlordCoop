using Coop.Game.Persistence.Party;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
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
