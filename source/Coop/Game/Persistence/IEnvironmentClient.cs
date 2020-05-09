using System.Collections.Generic;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentClient
    {
        #region TimeControl
        [CanBeNull] RemoteValue<CampaignTimeControlMode> TimeControlMode { get; set; }
        #endregion

        #region MobileParty
        void AddRemoteMoveTo(MobileParty party, RemoteValue<Vec2> moveTo);
        void RemoveRemoteMoveTo(MobileParty party);
        IReadOnlyDictionary<MobileParty, RemoteValue<Vec2>> RemoteMoveTo { get; }
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        #endregion
    }
}
