using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Missions;
using E2E.Tests.Util;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Bandits;

/// <summary>The server applies a bandit surrender only for a player on that side of the battle.</summary>
public class BanditRequestSenderTests : MissionTestEnvironment
{
    public BanditRequestSenderTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void BanditSurrenderFromAPlayerOutsideTheBattle_IsRefused()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var outsider = Clients.ElementAt(2);
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("ctrl-C", heroId, partyId, null, null)));
            playerManager.SetPeer("ctrl-C", outsider.NetPeer);
        });

        Server.SimulateMessage(outsider.NetPeer, new NetworkBanditsSurrenderAsPrisoners(mapEventId, BattleSideEnum.Attacker));
        Server.PumpGameThread();

        Server.Call(() => Assert.False(Server.GetRegisteredObject<MapEvent>(mapEventId).DefenderSide.IsSurrendered));
    }
}
