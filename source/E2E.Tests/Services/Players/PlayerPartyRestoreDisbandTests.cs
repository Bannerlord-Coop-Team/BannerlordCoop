using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Players;

/// <summary>
/// #2891: <c>PlayerPartyRestorer.Restore</c>, which runs on every server load and returning join, cancels a disband
/// saved with a player party. A disband started before the party was guarded as a player's is saved with it, and the
/// next hourly tick of native <see cref="DisbandPartyCampaignBehavior"/> marks it disbanding again, after which native
/// disbanding strips it.
/// </summary>
public class PlayerPartyRestoreDisbandTests : MapEventTestBase
{
    private const string BesiegerControllerId = "Besieger";
    private const string DefenderControllerId = "Defender";

    // Outlasts the one-day wait native OnPartyDisbandStarted gives a party before it is marked disbanding
    private const int HoursPastDisbandWait = 25;

    private DisbandPartyCampaignBehavior disbandBehavior = null!;

    public enum StaleDisband
    {
        Waiting,
        Disbanding,
    }

    public PlayerPartyRestoreDisbandTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// A save already holding a disbanding player party is repaired when its player rejoins, which runs the same
    /// restore as a server load.
    /// </summary>
    [Theory]
    [InlineData(StaleDisband.Waiting)]
    [InlineData(StaleDisband.Disbanding)]
    public void Rejoin_PartyLeftDisbanding_CancelsTheDisband(StaleDisband staleDisband)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        InstallDisbandBehavior();
        StartDisbandBeforeRegistration(partyId, staleDisband);

        var heroId = RegisterOwnerAsPlayer(DefenderControllerId, partyId);
        var validated = TestEnvironment.ConnectReturningClient(Clients.First(), DefenderControllerId);
        Assert.Equal(partyId, validated.Player.MobilePartyId);
        PassHours(HoursPastDisbandWait);

        AssertWorkingParty(DefenderControllerId, heroId, partyId);
    }

    /// <summary>
    /// The #2891 report: the player surrendered in a PvP siege, its party was restored while the player was still
    /// captive, and the ransom then has to free a party that stays whole.
    /// </summary>
    [Fact]
    public void RejoinWhileCaptive_PartyLeftDisbanding_RansomedPartyStaysWhole()
    {
        var siege = CreateSiegeMapEvent(MapEvent.BattleTypes.Siege);
        var partyId = siege.DefenderPartyId;
        var defenderClient = Clients.Last();
        InstallDisbandBehavior();
        StartDisbandBeforeRegistration(partyId, StaleDisband.Disbanding);

        RegisterOwnerAsPlayer(BesiegerControllerId, siege.AttackerPartyId);
        var heroId = RegisterOwnerAsPlayer(DefenderControllerId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), BesiegerControllerId);
        TestEnvironment.ConnectRegisteredPlayer(defenderClient, DefenderControllerId);

        Server.Call(() => Server.SimulateMessage(defenderClient.NetPeer,
            new NetworkPlayerSurrendered(partyId, siege.MapEventId)), BattleMenuSurrenderDisabledMethods());

        AssertCaptivity(Server, heroId, siege.AttackerPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, heroId, siege.AttackerPartyId);

        Rejoin(defenderClient, DefenderControllerId, partyId);
        // The release only puts the party back on the map once its player is in the campaign again
        TestEnvironment.ConnectRegisteredPlayer(defenderClient, DefenderControllerId);

        ReleasePlayerByRequest(defenderClient, heroId, partyId, EndCaptivityDetail.Ransom);
        PassHours(HoursPastDisbandWait);

        AssertCaptivity(Server, heroId, null);
        AssertWorkingParty(DefenderControllerId, heroId, partyId);
        foreach (var instance in Clients.Append(Server))
        {
            // Captivity forfeits the troops, so the released party holds exactly its hero
            AssertHeroInPartyRoster(instance, heroId, partyId);
            instance.Call(() => Assert.True(instance.GetRegisteredObject<MobileParty>(partyId).IsActive));
        }
    }

    /// <summary>
    /// Registers the party's own lord as the player's hero, so the restore on rejoin finds a complete player graph.
    /// </summary>
    private string RegisterOwnerAsPlayer(string controllerId, string partyId)
    {
        string? heroId = null;
        string? clanId = null;
        string? characterObjectId = null;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var owner = party.LordPartyComponent.Owner;
            Assert.Same(party, owner.PartyBelongedTo);
            Assert.True(Server.ObjectManager.TryGetId(owner, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(owner.Clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(owner.CharacterObject, out characterObjectId));
        });

        RegisterAsPlayerParty(controllerId, heroId!, partyId, clanId!, characterObjectId!);
        return heroId!;
    }

    private void InstallDisbandBehavior()
    {
        Server.Call(() =>
        {
            disbandBehavior = new DisbandPartyCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { disbandBehavior }));
            disbandBehavior.RegisterEvents();
        });
    }

    /// <summary>
    /// Starts the native disband before the party is a registered player's, the way one started before
    /// <c>DisbandPartyActionPatches</c> guarded player parties. For <see cref="StaleDisband.Disbanding"/> it passes
    /// the wait so the hourly tick marks the party, and it checks every instance sees the disband.
    /// </summary>
    private void StartDisbandBeforeRegistration(string partyId, StaleDisband staleDisband)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            DisbandPartyAction.StartDisband(party);

            Assert.True(disbandBehavior.IsPartyWaitingForDisband(party));
        });

        if (staleDisband == StaleDisband.Disbanding)
            PassHours(HoursPastDisbandWait);

        // The clients see the disband too, so the cancel has to reach them
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.False(TextObject.IsNullOrEmpty(party.Party.CustomName),
                    $"Party {partyId} has no disband name on {instance.GetType().Name}");
                Assert.Equal(staleDisband == StaleDisband.Disbanding, party.IsDisbanding);
            });
        }
    }

    /// <summary>
    /// Advances the server clock and runs the disband behavior's hourly tick, which turns an expired wait into a disband.
    /// </summary>
    private void PassHours(int hours)
    {
        Server.Call(() =>
        {
            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Hours(hours).NumTicks;
            disbandBehavior.HourlyTick();
        });
    }

    /// <summary>
    /// Drops the client and brings it back through the returning handshake.
    /// </summary>
    private void Rejoin(EnvironmentInstance client, string controllerId, string partyId)
    {
        Server.Call(() => Server.SimulateMessage(this, new PlayerDisconnected(client.NetPeer, default)));
        var validated = TestEnvironment.ConnectReturningClient(client, controllerId);
        Assert.Equal(partyId, validated.Player.MobilePartyId);
        // Runs the deferred park of the dropped party
        Server.PumpGameThread();
    }

    /// <summary>
    /// The party is still the controller's registered party, keeps its hero as leader, and isn't disbanding anywhere.
    /// </summary>
    private void AssertWorkingParty(string controllerId, string heroId, string partyId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var hero = Server.GetRegisteredObject<Hero>(heroId);

            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            Assert.Equal(partyId, player.MobilePartyId);
            Assert.Same(hero, party.LeaderHero);
            Assert.False(disbandBehavior.IsPartyWaitingForDisband(party));
        });

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.MemberRoster.Contains(instance.GetRegisteredObject<Hero>(heroId).CharacterObject),
                    $"Party {partyId} lost its hero on {instance.GetType().Name}");
                Assert.False(party.IsDisbanding, $"Party {partyId} is still disbanding on {instance.GetType().Name}");
                Assert.True(TextObject.IsNullOrEmpty(party.Party.CustomName),
                    $"Party {partyId} kept its disband name {party.Party.CustomName} on {instance.GetType().Name}");
            });
        }
    }
}
