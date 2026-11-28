using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.BloodFeuds.Data;
using GameInterface.Services.BloodFeuds.Messages;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BloodFeuds;

public class ExecutionCampaignBehaviorInterfaceTests : MapEventTestBase
{
    public ExecutionCampaignBehaviorInterfaceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerChangingClan_IsSentTheNewClansFeuds()
    {
        var (playerId, _) = CreatePlayerHeroParty("Joiner");
        var oldClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var newClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            var player = Server.GetRegisteredObject<Hero>(playerId);
            var oldClan = Server.GetRegisteredObject<Clan>(oldClanId);
            var newClan = Server.GetRegisteredObject<Clan>(newClanId);

            using (new AllowedThread())
            {
                oldClan.InitMembers();
                newClan.InitMembers();
                player.Clan = oldClan;
            }

            Server.Resolve<IPlayerManager>().SetPeer("Joiner", Clients.First().NetPeer);
            Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds[newClanId] = new()
            {
                [feudClanId] = new BloodFeudData { HasBloodFeud = true },
            };

            player.Clan = newClan;

            var update = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkUpdateBloodFeuds>());
            Assert.True(update.Feuds[feudClanId].HasBloodFeud);
        });
    }
}
