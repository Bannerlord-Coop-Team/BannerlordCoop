#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Tests;
using ProtoBuf;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MapEvents.TroopSupply;
using Moq;
using Newtonsoft.Json.Linq;

namespace E2E.Tests.Services.Missions;

/// <summary>Checks fixture request boundaries and its serialized identity.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class BattleHealthFixtureCommandTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose() => ModInformation.IsServer = wasServer;

    [Theory]
    [InlineData("damage", "not-an-agent", "1")]
    [InlineData("damage", "00000000-0000-0000-0000-000000000000", "1")]
    [InlineData("damage", "fca7722a-6c36-4922-ae85-7da5b6f19036", "0")]
    [InlineData("damage", "fca7722a-6c36-4922-ae85-7da5b6f19036", "10001")]
    [InlineData("damage", "fca7722a-6c36-4922-ae85-7da5b6f19036", "NaN")]
    [InlineData("retreat", "fca7722a-6c36-4922-ae85-7da5b6f19036", "1")]
    [InlineData("unknown", "fca7722a-6c36-4922-ae85-7da5b6f19036", "1")]
    public void InvalidRequest_IsRejectedBeforeResolvingRuntime(string operation, string agentId, string damage)
    {
        ModInformation.IsServer = true;
        var args = new CoopCommandArgsFactory().FromValues(new[] { "testclient", "battle", operation, agentId, damage });
        Assert.Equal("Invalid operation, agent or damage.", new BattleHealthFixtureCommand().ProcessCommand(args).Output);
    }

    [Fact]
    public void ClientCannotRequestMutation()
    {
        ModInformation.IsServer = false;
        var args = new CoopCommandArgsFactory().FromValues(new[] { "testclient", "battle", "retreat" });
        Assert.False(new BattleHealthFixtureCommand().ProcessCommand(args).Succeeded);
    }

    [Fact]
    public void RequestRoundTrip_PreservesBattleAgentAndOperation()
    {
        var agentId = Guid.NewGuid();
        var request = new NetworkBattleHealthFixture("battle", "damage", agentId, 37);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, request);
        stream.Position = 0;
        var received = Serializer.Deserialize<NetworkBattleHealthFixture>(stream);
        Assert.Equal("battle", received.MapEventId);
        Assert.Equal("damage", received.Operation);
        Assert.Equal(agentId, received.AgentId);
        Assert.Equal(37, received.Damage);
    }

    [Fact]
    public void ReserveState_AfterRegistryDestructionExposesRemainingLedgerThenEmptyCleanup()
    {
        ModInformation.IsServer = true;
        var objects = new Mock<IObjectManager>();
        var ledger = new BattleTroopLedger();
        ledger.SetReserve("old-battle", "party", new[] { new TroopReserveEntry(1, "troop", 0, health: 37f) });
        var command = new BattleHealthReserveStateCommand(objects.Object, ledger);
        var args = new CoopCommandArgsFactory().FromValues(new[] { "old-battle" });
        JObject Read() => JObject.Parse(command.ProcessCommand(args).Output.Substring("LIVE_TEST_JSON=".Length));
        var remaining = Read();
        Assert.False((bool)remaining["registered"]!);
        Assert.Equal(37f, (float)remaining["parties"]![0]!["entries"]![0]!["Health"]!);
        ledger.Remove("old-battle");
        Assert.Empty((JArray)Read()["parties"]!);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ReserveState_RejectsEmptyRetainedId(string id)
    {
        ModInformation.IsServer = true;
        var command = new BattleHealthReserveStateCommand(new Mock<IObjectManager>(MockBehavior.Strict).Object,
            new Mock<IBattleTroopLedger>(MockBehavior.Strict).Object);
        Assert.False(command.ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { id })).Succeeded);
    }
}
#endif
