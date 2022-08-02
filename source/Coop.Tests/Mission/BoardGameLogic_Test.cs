using Common;
using Coop.Mod.Missions;
using Coop.Mod.Missions.Network;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using Network.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Moq;
using TaleWorlds.MountAndBlade;
using SandBox.BoardGames.MissionLogics;
using TaleWorlds.CampaignSystem.Settlements;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;

namespace Coop.Tests.Mission
{
    public class BoardGameLogic_Test : IDisposable
    {
        private readonly ITestOutputHelper output;

        private Mock<Settlement> settlementMock = new Mock<Settlement>();
        private Mock<MobileParty> mobilePartyMock = new Mock<MobileParty>();
        private Mock<Campaign> campaignMock = new Mock<Campaign>();


        public BoardGameLogic_Test(ITestOutputHelper output)
        {
            this.output = output;
            output.WriteLine("Setup called");

            typeof(Campaign).GetProperty("Current").SetValue(null, campaignMock.Object);

            typeof(Campaign).GetProperty("MainParty").SetValue(null, mobilePartyMock.Object);

            typeof(MobileParty).GetField("_currentSettlement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobilePartyMock.Object, settlementMock.Object);

            var seettlement = Settlement.CurrentSettlement;

        }

        [Fact]
        public void BoardGameTest()
        {
            TestMessageBroker testMessageBroker = new TestMessageBroker();
            BoardGameLogic boardGameLogic = new BoardGameLogic(testMessageBroker, Guid.NewGuid());
            output.WriteLine("Test 1 was called");

        }

        [Fact]
        public void Test2()
        {

            output.WriteLine("Test 2 was called");

        }

        public void Dispose()
        {
            output.WriteLine("Teardown was called");
        }
    }

    public class TestMessageBroker : INetworkMessageBroker
    {
        private readonly Dictionary<Type, List<Delegate>> m_Subscribers;

        public void Dispose()
        {
            
        }

        public void Publish<T>(T message, NetPeer peer = null)
        {
            
        }

        public void Publish<T>(T message)
        {
            
        }

        public void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            var delegates = m_Subscribers.ContainsKey(typeof(T)) ?
                            m_Subscribers[typeof(T)] : new List<Delegate>();
            if (!delegates.Contains(subscription))
            {
                delegates.Add(subscription);
            }
            m_Subscribers[typeof(T)] = delegates;
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            if (!m_Subscribers.ContainsKey(typeof(T))) return;
            var delegates = m_Subscribers[typeof(T)];
            if (delegates.Contains(subscription))
                delegates.Remove(subscription);
            if (delegates.Count == 0)
                m_Subscribers.Remove(typeof(T));
        }
    }
}
