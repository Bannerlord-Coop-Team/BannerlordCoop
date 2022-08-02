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
using TaleWorlds.MountAndBlade;
using SandBox.BoardGames.MissionLogics;
using TaleWorlds.CampaignSystem.Settlements;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using SandBox;
using Moq;
using SandBox.BoardGames;
using Coop.Mod.Missions.Messages.BoardGames;

namespace Coop.Tests.Missions
{
    public class BoardGameLogic_Test : IDisposable
    {
        private readonly ITestOutputHelper output;

        Mock<MissionBoardGameLogic> logicMock;
        Mock<BoardGameSeega> boardGameMock;

        public BoardGameLogic_Test(ITestOutputHelper output)
        {
            this.output = output;
            logicMock = new Mock<MissionBoardGameLogic>();
            boardGameMock = new Mock<BoardGameSeega>(logicMock.Object, PlayerTurn.PlayerOne);
            typeof(MissionBoardGameLogic).GetProperty(nameof(MissionBoardGameLogic.Board)).SetValue(logicMock.Object, boardGameMock.Object);
        }

        [Fact]
        public void BoardGameStartTest()
        {
            TestMessageBroker testMessageBroker = new TestMessageBroker();
            CultureObject.BoardGameType gameType = CultureObject.BoardGameType.Seega;
            BoardGameLogic boardGameLogic = new BoardGameLogic(testMessageBroker, Guid.NewGuid(), logicMock.Object, gameType);
            boardGameLogic.StartGame(true, null);
            output.WriteLine("Test 1 was called");

        }

        [Fact]
        public void SendGameRequestTest()
        {
            TestMessageBroker testMessageBroker = new TestMessageBroker();
            
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
        private readonly Dictionary<Type, List<Delegate>> m_Subscribers = new Dictionary<Type, List<Delegate>>();

        public void Dispose()
        {
            
        }

        public void Publish<T>(T message, NetPeer peer = null)
        {
            Publish<T>(message);
        }

        public void Publish<T>(T message)
        {
            if (message == null)
                return;
            if (!m_Subscribers.ContainsKey(typeof(T)))
            {
                return;
            }
            var delegates = m_Subscribers[typeof(T)];
            if (delegates == null || delegates.Count == 0) return;
            var payload = new MessagePayload<T>(message, null);
            foreach (var handler in delegates.Select
            (item => item as Action<MessagePayload<T>>))
            {
                handler?.Invoke(payload);
            }
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
