using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M11 phase 2: the battle host's AI fleet attempt reaches the server, which keeps it for the battle's life and passes it on to
/// every client, so a player who opens the battle later caps its fleet against it.
/// </summary>
public class NavalFleetRecordScenarioTests : NavalBattleTestEnvironment
{
    public NavalFleetRecordScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    // The first attempt of a side is the record: a second report of it changes nothing, and the battle's finalize drops it.
    [Fact]
    public void HostsAttempt_ReachesEveryClientOnce_AndTheServerDropsItWhenTheBattleEnds()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out _);
        var attempt = new NetworkNpcFleetAttempted(battle.MapEventId, BattleSideEnum.Attacker, failed: false, aiHulls: 5,
            playerHulls: 3, countedPartyIds: new[] { "MobileParty_A" });
        var repeat = new NetworkNpcFleetAttempted(battle.MapEventId, BattleSideEnum.Attacker, failed: true, aiHulls: 0,
            playerHulls: 0, countedPartyIds: null);

        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, attempt));
        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, repeat));

        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<INavalFleetRecords>().TryGet(battle.MapEventId, BattleSideEnum.Attacker, out var kept));
                Assert.Equal((false, 5, 3), (kept.Failed, kept.AiHulls, kept.PlayerHulls));
                Assert.Equal(new[] { "MobileParty_A" }, kept.CountedPartyIds);
            });
        }

        Server.Call(() => Server.GetRegisteredObject<MapEvent>(battle.MapEventId).FinalizeEvent(), MapEventDisabledMethods);
        Server.PumpGameThread();

        Server.Call(() => Assert.False(Server.Resolve<INavalFleetRecords>().TryGet(battle.MapEventId, BattleSideEnum.Attacker, out _)));
    }
}
