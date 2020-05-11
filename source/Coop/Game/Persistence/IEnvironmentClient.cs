using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentClient
    {
        Field TargetPosition { get; }
        Field TimeControlMode { get; }

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        object GetTimeController();
        #endregion
    }
}
