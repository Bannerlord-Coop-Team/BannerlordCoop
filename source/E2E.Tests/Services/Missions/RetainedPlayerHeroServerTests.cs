using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using Missions.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Exercises retained-hero grants through the campaign client/server wire and authoritative ledger.</summary>
public class RetainedPlayerHeroServerTests : MissionTestEnvironment
{
    public RetainedPlayerHeroServerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("before-mission-ready", true)]
    [InlineData("wrong-sender", false)]
    [InlineData("wrong-epoch", false)]
    [InlineData("wrong-battle", false)]
    [InlineData("wrong-character", false)]
    [InlineData("wrong-seed", false)]
    [InlineData("not-supplied", false)]
    [InlineData("supply-arrives-after-request", true)]
    public void ServerForwardsOnlyTheCurrentHoldersExactSuppliedHero(string variant, bool accepted)
    {
        var (battleId, partyIds) = SetupCoopBattle("holder", "returner");
        var clients = Clients.ToArray();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            players.SetPeer("holder", clients[0].NetPeer);
            players.SetPeer("returner", clients[1].NetPeer);
        });
        EnterBattle(clients[0], battleId);
        if (variant == "before-mission-ready")
            clients[1].Call(() => clients[1].Resolve<IBattleHostRegistry>().Remove(battleId));
        EnterBattle(clients[1], battleId, missionReady: variant != "before-mission-ready");
        if (variant == "before-mission-ready")
            clients[1].Call(() => Assert.False(clients[1].Resolve<IBattleHostRegistry>().TryGet(battleId, out _)));

        string partyId = null;
        string characterId = null;
        int epoch = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battleId, out var battle));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var party));
            var eventParty = battle.AttackerSide.Parties.Concat(battle.DefenderSide.Parties)
                .Single(value => value.Party == party.Party);
            Assert.True(Server.ObjectManager.TryGetId(eventParty, out partyId));
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer("returner", out var player));
            characterId = player.CharacterObjectId;
            Assert.True(Server.Resolve<IBattleHostRegistry>().TryGet(battleId, out var host));
            epoch = host.Epoch;
            var ledger = Server.Resolve<IBattleTroopLedger>();
            ledger.SetReserve(battleId, partyId, new[] { new TroopReserveEntry(1141, characterId, 0) });
            if (variant != "not-supplied" && variant != "supply-arrives-after-request") ledger.ReportSupplied(battleId, partyId, 1);
        }, MapEventDisabledMethods);

        var data = new BattleAgentSpawnData(Guid.NewGuid(), variant == "wrong-character" ? "other-hero" : characterId,
            default, BattleSideEnum.Defender, 22, "holder", partyId,
            variant == "wrong-seed" ? 1142 : 1141, new Equipment(), default, null,
            originalOwnerControllerId: "returner", movementScopeId: "returner:first-mission", authorityRevision: 1);
        var handoff = new NetworkRetainedPlayerHero(variant == "wrong-battle" ? "another-battle" : battleId,
            variant == "wrong-epoch" ? epoch + 1 : epoch, "returner", data);
        var sender = clients[variant == "wrong-sender" ? 1 : 0];
        BattleHostAssignment assignmentAtGrant = null;
        clients[1].Call(() => clients[1].Resolve<IMessageBroker>().Subscribe<NetworkRetainedPlayerHero>(payload =>
            GameThread.RunSafe(() => clients[1].Resolve<IBattleHostRegistry>().TryGet(battleId, out assignmentAtGrant))));
        int messagesBefore = clients[1].InternalMessages.Count;
        int before = clients[1].InternalMessages.GetMessages<NetworkRetainedPlayerHero>().Count();
        sender.Call(() => sender.Resolve<INetwork>().SendAll(new NetworkRequestRetainedPlayerHero(handoff)));

        if (variant == "supply-arrives-after-request")
        {
            Assert.Equal(before, clients[1].InternalMessages.GetMessages<NetworkRetainedPlayerHero>().Count());
            Server.Call(() => Server.Resolve<IBattleTroopLedger>().ReportSupplied(battleId, partyId, 1));
            sender.Call(() => sender.Resolve<INetwork>().SendAll(new NetworkRequestRetainedPlayerHero(handoff)));
        }

        var grants = clients[1].InternalMessages.GetMessages<NetworkRetainedPlayerHero>().Skip(before).ToArray();
        Assert.Equal(accepted ? 1 : 0, grants.Length);
        if (accepted)
        {
            Assert.NotNull(assignmentAtGrant);
            Assert.Equal("holder", assignmentAtGrant.HostControllerId);
            Assert.Equal(epoch, assignmentAtGrant.Epoch);
            var received = clients[1].InternalMessages.Skip(messagesBefore)
                .Where(message => message is NetworkBattleHostAssigned || message is NetworkRetainedPlayerHero).ToArray();
            int grantIndex = Array.FindIndex(received, message => message is NetworkRetainedPlayerHero);
            Assert.True(grantIndex > 0);
            var assigned = Assert.IsType<NetworkBattleHostAssigned>(received[grantIndex - 1]);
            Assert.Equal(battleId, assigned.MapEventId);
            Assert.Equal(epoch, assigned.Epoch);
            Assert.Equal("holder", assigned.HostControllerId);
            Assert.Equal(data.AgentId, grants[0].Previous.AgentId);
            Assert.Equal(1, grants[0].Previous.AuthorityRevision);
            Assert.Equal("holder", grants[0].Previous.OwnerControllerId);
            Assert.Equal("returner", grants[0].ReturningControllerId);
        }
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IBattleTroopLedger>().TryGetReserve(battleId, partyId, out var entries, out int supplied));
            Assert.Single(entries);
            Assert.Equal(variant == "not-supplied" ? 0 : 1, supplied);
        });
    }
}
