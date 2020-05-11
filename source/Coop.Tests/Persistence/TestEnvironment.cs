using System;
using System.Collections.Generic;
using Coop.Game.Persistence;
using Coop.Sync;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Tests
{
    internal class TestEnvironmentClient : IEnvironmentClient
    {
        #region TimeControl
        public List<CampaignTimeControlMode> Values = new List<CampaignTimeControlMode>();

        [CanBeNull] private RemoteValue<CampaignTimeControlMode> m_TimeControlMode;

        public RemoteValue<CampaignTimeControlMode> TimeControlMode
        {
            get => m_TimeControlMode;
            set
            {
                m_TimeControlMode = value;
                if (m_TimeControlMode != null)
                {
                    m_TimeControlMode.OnValueChanged += Values.Add;
                }
            }
        }
        #endregion

        #region MobileParty
        public Field TargetPosition => throw new NotImplementedException();
        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    internal class TestEnvironmentServer : IEnvironmentServer
    {
        public CampaignTimeControlMode TimeControlMode { get; set; } = CampaignTimeControlMode.Stop;
    }
}
