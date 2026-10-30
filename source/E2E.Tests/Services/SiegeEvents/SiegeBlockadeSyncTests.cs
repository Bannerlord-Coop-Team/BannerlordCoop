using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// The server activating or lifting a naval blockade: the blockade reaches every client and only the besieging players'
/// anchors are locked while it holds.
/// </summary>
public class SiegeBlockadeSyncTests : SyncTestBase
{
    private static readonly IReadOnlyList<MethodBase> SiegeCreationDisabledMethods = new[]
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    private readonly string siegeEventId;
    private readonly string playerPartyId;
    private readonly string aiPartyId;

    public SiegeBlockadeSyncTests(ITestOutputHelper output) : base(output)
    {
        siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);
        playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        aiPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var camp = instance.GetRegisteredObject<SiegeEvent>(siegeEventId).BesiegerCamp;
                camp._besiegerParties.Clear();
                foreach (var partyId in new[] { playerPartyId, aiPartyId })
                {
                    var party = instance.GetRegisteredObject<MobileParty>(partyId);
                    party._besiegerCamp = camp;
                    camp._besiegerParties.Add(party);
                }
                camp._leaderParty = camp._besiegerParties[0];
            });
        }

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(playerPartyId);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("blockader", heroId, playerPartyId, clanId, characterId)));
        });
    }

    [Fact]
    public void ServerActivateBlockade_ReachesClientsAndLocksOnlyThePlayerAnchor()
    {
        Server.Call(() => Server.GetRegisteredObject<SiegeEvent>(siegeEventId).ActivateBlockade());

        AssertBlockade(isActive: true);
    }

    [Fact]
    public void ServerDeactivateBlockade_ReachesClientsAndUnlocksThePlayerAnchor()
    {
        Server.Call(() => Server.GetRegisteredObject<SiegeEvent>(siegeEventId).ActivateBlockade());
        AssertBlockade(isActive: true);

        Server.Call(() => Server.GetRegisteredObject<SiegeEvent>(siegeEventId).DeactivateBlockade());

        AssertBlockade(isActive: false);
    }

    // The siege can end right after its last blockading fleet leaves, so a late blockade change for it is dropped
    [Fact]
    public void NetworkChangeSiegeBlockade_UnknownSiege_IsIgnored()
    {
        var client = Clients.First();

        client.SimulateMessage(this, new NetworkChangeSiegeBlockade("SiegeEvent_Missing", true));
        client.PumpGameThread();

        client.Call(() => Assert.False(client.GetRegisteredObject<SiegeEvent>(siegeEventId).IsBlockadeActive));
    }

    private void AssertBlockade(bool isActive)
    {
        Server.Call(() =>
        {
            Assert.Equal(isActive, Server.GetRegisteredObject<MobileParty>(playerPartyId).Anchor.IsDisabled);
            Assert.False(Server.GetRegisteredObject<MobileParty>(aiPartyId).Anchor.IsDisabled);
        });

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                Assert.Equal(isActive, client.GetRegisteredObject<SiegeEvent>(siegeEventId).IsBlockadeActive);
                Assert.Equal(isActive, client.GetRegisteredObject<MobileParty>(playerPartyId).Anchor.IsDisabled);
            });
        }
    }
}
