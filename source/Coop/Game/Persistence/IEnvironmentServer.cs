using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentServer
    {
        #region TimeControl
        CampaignTimeControlMode TimeControlMode { get; set; }
        #endregion

        #region MobileParty
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        #endregion
    }
}
