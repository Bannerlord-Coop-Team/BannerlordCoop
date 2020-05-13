using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentClient
    {
        SyncField TargetPosition { get; }
        SyncField TimeControlMode { get; }

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        object GetTimeController();
        #endregion
    }
}
