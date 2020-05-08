using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Game.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game
{
    public class ServerEnvironment : IEnvironment
    {
        public CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        public CampaignTimeControlMode TimeControlMode { get; set; } = CampaignTimeControlMode.Stop;

        #region MobileParty
        public void AddRemoteMoveTo(MobileParty party, RemoteValue<Vec2> moveTo)
        {
            throw new NotImplementedException();
        }

        public void RemoveRemoteMoveTo(MobileParty party)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyDictionary<MobileParty, RemoteValue<Vec2>> RemoteMoveTo =>
            throw new NotImplementedException();

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }
        #endregion
    }
}
