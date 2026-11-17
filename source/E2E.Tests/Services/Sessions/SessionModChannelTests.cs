using Common.Messaging;
using Common.Serialization;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Services.Missions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions;
using Missions.Sessions;
using ProtoBuf;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Sessions;

[ProtoContract(SkipConstructor = true)]
public class TestSessionModMessage : IEvent
{
    [ProtoMember(1)]
    public string SessionId { get; }
    [ProtoMember(2)]
    public int Wave { get; }

    public TestSessionModMessage(string sessionId, int wave)
    {
        SessionId = sessionId;
        Wave = wave;
    }
}

public class SessionModChannelTests : MissionTestEnvironment
{
    public SessionModChannelTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    private (string sessionId, EnvironmentInstance[] members) StartSession(MissionEngineFixture fixture)
    {
        var members = Clients.ToArray();
        string[] ids = { "owner", "peer", "third" };
        var sessionId = SessionInstanceId.Create();
        for (int i = 0; i < members.Length; i++)
        {
            SetControllerId(members[i], ids[i]);
            var member = members[i];
            member.Call(() =>
            {
                CreateConnectedMission(fixture, member, sessionId);
                var controller = member.Resolve<ISessionMissionHost>().CreateMissionBehaviors(sessionId).Single();
                ((CoopSessionController)controller).OnBehaviorInitialize();
            });
        }
        return (sessionId, members);
    }

    [Fact]
    public void SendToSession_DeliversModMessageToPeer()
    {
        using var fixture = new MissionEngineFixture();
        var (sessionId, members) = StartSession(fixture);

        members[0].Call(() => Assert.True(members[0].Resolve<ISessionModChannel>().SendToSession(new TestSessionModMessage(sessionId, 3))));

        var received = Assert.Single(members[1].InternalMessages.GetMessages<TestSessionModMessage>());
        Assert.Equal(sessionId, received.SessionId);
        Assert.Equal(3, received.Wave);
        Assert.Single(members[2].InternalMessages.GetMessages<TestSessionModMessage>());
        Assert.Empty(members[0].InternalMessages.GetMessages<TestSessionModMessage>());
    }

    [Fact]
    public void SendToController_DeliversToOnePeerOnly()
    {
        using var fixture = new MissionEngineFixture();
        var (sessionId, members) = StartSession(fixture);

        members[0].Call(() => Assert.True(members[0].Resolve<ISessionModChannel>().SendToController("peer", new TestSessionModMessage(sessionId, 1))));

        Assert.Single(members[1].InternalMessages.GetMessages<TestSessionModMessage>());
        Assert.Empty(members[2].InternalMessages.GetMessages<TestSessionModMessage>());
    }

    [Fact]
    public void ModMessageFromAnotherAssembly_RoundTripsThroughTheWireSerializer()
    {
        // The mock network passes objects by reference, so only the real serializer proves a mod type can travel.
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        var bytes = serializer.Serialize(new TestSessionModMessage("session:abc", 7));

        var received = Assert.IsType<TestSessionModMessage>(serializer.Deserialize(bytes));
        Assert.Equal("session:abc", received.SessionId);
        Assert.Equal(7, received.Wave);
    }

    [Fact]
    public void NoSessionMission_ReturnsFalse()
    {
        var client = Clients.First();

        client.Call(() =>
        {
            BattleSpawnGate.BeginBattle("12345");
            try
            {
                var channel = client.Resolve<ISessionModChannel>();
                Assert.False(channel.SendToSession(new TestSessionModMessage("12345", 1)));
                Assert.False(channel.SendToController("peer", new TestSessionModMessage("12345", 1)));
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
            Assert.Empty(client.Resolve<E2E.Tests.Environment.Mock.MockBattleNetwork>().NetworkSentMessages.GetMessages<TestSessionModMessage>());
        });
    }
}
