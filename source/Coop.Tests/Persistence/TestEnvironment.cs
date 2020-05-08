using System;
using System.Collections.Generic;
using Coop.Game.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Tests
{
    internal class TestEnvironment : IEnvironment
    {
        #region TimeControl
        public CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        public List<CampaignTimeControlMode> Values = new List<CampaignTimeControlMode>() { CampaignTimeControlMode.Stop };
        public CampaignTimeControlMode TimeControlMode
        {
            get => Values[^1];
            set => Values.Add(value);
        }
        #endregion

        #region MobileParty
        public IReadOnlyDictionary<MobileParty, RemoteValue<Vec2>> RemoteMoveTo => throw new NotImplementedException();

        public void AddRemoteMoveTo(MobileParty party, RemoteValue<Vec2> moveTo)
        {
            throw new NotImplementedException();
        }

        public void RemoveRemoteMoveTo(MobileParty party)
        {
            throw new NotImplementedException();
        }

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}