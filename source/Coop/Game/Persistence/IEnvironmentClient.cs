using Coop.Sync;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentClient
    {
        Field TargetPosition { get; }

        #region TimeControl
        [CanBeNull] RemoteValue<CampaignTimeControlMode> TimeControlMode { get; set; }
        #endregion

        #region MobileParty
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        #endregion
    }
}
