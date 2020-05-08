using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence
{
    public interface IEnvironment
    {
        #region TimeControl
        CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        CampaignTimeControlMode TimeControlMode { get; set; }
        #endregion

        #region MobileParty
        void AddRemoteMoveTo(MobileParty party, RemoteValue<Vec2> moveTo);
        void RemoveRemoteMoveTo(MobileParty party);
        IReadOnlyDictionary<MobileParty, RemoteValue<Vec2>> RemoteMoveTo { get; }
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        #endregion
    }
}
