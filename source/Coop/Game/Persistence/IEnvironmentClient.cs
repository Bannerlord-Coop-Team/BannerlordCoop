using Coop.Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentClient
    {
        SyncField<MobileParty, Vec2> TargetPosition { get; }
        SyncField<Campaign, CampaignTimeControlMode> TimeControlMode { get; }

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
        #endregion
    }
}
