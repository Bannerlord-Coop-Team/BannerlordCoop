using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Messages;
using Missions.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M11 phase 2: the battle host's AI fleet attempt reaches the server, which records it while the battle is open and sends it to
/// every client, and to a client entering the campaign later, so a player who opens the battle afterwards caps its fleet.
/// </summary>
public class NavalNpcFleetAttemptScenarioTests : NavalBattleTestEnvironment
{
    public NavalNpcFleetAttemptScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    // The side's first attempt is recorded and sent once; a second report of it changes nothing, a client entering the campaign
    // gets it again while the battle is open, and none once the battle ended.
    [Fact]
    public void HostsAttempt_IsSentToEveryClientOnce_AndToAJoiningClientWhileTheBattleIsOpen()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        Server.NetworkSentMessages.Clear();

        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, new NetworkNpcFleetAttempted(battle.MapEventId,
            BattleSideEnum.Attacker, failed: false, aiHulls: 5, playerHulls: 3, countedPartyIds: new[] { "MobileParty_A" })));
        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, new NetworkNpcFleetAttempted(battle.MapEventId,
            BattleSideEnum.Attacker, failed: true, aiHulls: 0, playerHulls: 0, countedPartyIds: null)));
        Server.PumpGameThread();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkNpcFleetAttemptRecorded>());
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var attempts = instance.Resolve<INavalNpcFleetAttemptTracker>();
                Assert.True(attempts.TryGet(battle.MapEventId, BattleSideEnum.Attacker, out var attempt));
                Assert.Equal((5, 3), (attempt.AiHulls, attempt.PlayerHulls));
                Assert.Equal(new[] { "MobileParty_A" }, attempt.CountedPartyIds);
            });
        }

        Server.SimulateMessage(this, new PlayerCampaignEntered(b.Instance.NetPeer));
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkNpcFleetAttemptRecorded>().Count());

        Server.Call(() => Server.GetRegisteredObject<MapEvent>(battle.MapEventId).FinalizeEvent(), MapEventDisabledMethods);
        Server.PumpGameThread();
        Server.SimulateMessage(this, new PlayerCampaignEntered(b.Instance.NetPeer));

        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkNpcFleetAttemptRecorded>().Count());
    }

    // BR-017: a battle whose last player left stays open, and re-engaging it opens a new mission whose host attempts the fleets
    // again; that attempt is recorded and sent anew, and replaces the old one on every client.
    [Fact]
    public void ReEngagedBattle_RecordsItsNewMissionsAttempt_AndEveryClientReplacesTheOldOne()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        Report(a, battle, aiHulls: 5);
        foreach (var leaver in new[] { a, b })
        {
            Server.Call(() => Server.SimulateMessage(leaver.Instance.NetPeer, new NetworkMissionLeft(leaver.Id, battle.MapEventId)),
                VictoryConclusionDisabledMethods());
        }

        Server.PumpGameThread();
        Server.SimulateMessage(a.Instance.NetPeer, new NetworkMissionEntered(a.Id, battle.MapEventId));
        Server.NetworkSentMessages.Clear();

        Report(a, battle, aiHulls: 2);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkNpcFleetAttemptRecorded>());
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<INavalNpcFleetAttemptTracker>().TryGet(battle.MapEventId, BattleSideEnum.Attacker,
                    out var attempt));
                Assert.Equal(2, attempt.AiHulls);
            });
        }
    }

    // The battle host reports its Attacker AI fleet attempt to the server.
    private void Report(NavalPeer host, NavalBattle battle, int aiHulls)
    {
        Server.Call(() => Server.SimulateMessage(host.Instance.NetPeer, new NetworkNpcFleetAttempted(battle.MapEventId,
            BattleSideEnum.Attacker, failed: false, aiHulls, playerHulls: 3, countedPartyIds: new[] { "MobileParty_A" })));
        Server.PumpGameThread();
    }
}
