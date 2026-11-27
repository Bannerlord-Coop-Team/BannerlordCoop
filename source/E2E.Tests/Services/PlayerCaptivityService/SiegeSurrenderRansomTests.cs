using Common.Network;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PlayerCaptivityService;

/// <summary>
/// #2891: a player who surrendered in a PvP siege and was ransomed must get back a working party. A disband
/// started on the party before it was guarded as a player's is saved with it, and the next hourly tick of native
/// <see cref="DisbandPartyCampaignBehavior"/> marks it disbanding again, after which native disbanding strips it.
/// The restore a server load or returning join runs cancels it.
/// </summary>
public class SiegeSurrenderRansomTests : MapEventTestBase
{
    private const string BesiegerControllerId = "Besieger";
    private const string DefenderControllerId = "Defender";

    // Outlasts the one-day wait native OnPartyDisbandStarted gives a party before it is marked disbanding
    private const int HoursPastDisbandWait = 25;

    public enum StaleDisband
    {
        None,
        Waiting,
        Disbanding,
    }

    public SiegeSurrenderRansomTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(StaleDisband.None)]
    [InlineData(StaleDisband.Waiting)]
    [InlineData(StaleDisband.Disbanding)]
    public void RansomAfterSiegeSurrender_RestoresThePartyWithoutItsDisband(StaleDisband staleDisband)
    {
        var siege = CreateSiegeMapEvent(MapEvent.BattleTypes.Siege);
        var partyId = siege.DefenderPartyId;
        var defenderClient = Clients.Last();
        InstallDisbandBehavior();
        StartDisbandBeforeRegistration(partyId, staleDisband);

        RegisterOwnerAsPlayer(BesiegerControllerId, siege.AttackerPartyId);
        var heroId = RegisterOwnerAsPlayer(DefenderControllerId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), BesiegerControllerId);
        // The defender's join runs the restore every server load and rejoin runs
        ReturnToServer(defenderClient, DefenderControllerId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(defenderClient, DefenderControllerId);

        Server.Call(() => Server.SimulateMessage(defenderClient.NetPeer,
            new NetworkPlayerSurrendered(partyId, siege.MapEventId)), BattleMenuSurrenderDisabledMethods());

        AssertCaptivity(Server, heroId, siege.AttackerPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, heroId, siege.AttackerPartyId);

        Ransom(defenderClient, heroId, partyId);
        PassHours(HoursPastDisbandWait);

        AssertCaptivity(Server, heroId, null);
        AssertWorkingParty(DefenderControllerId, heroId, partyId);
        foreach (var instance in Clients.Append(Server))
        {
            // Captivity forfeits the troops, so the released party holds exactly its hero
            AssertHeroInPartyRoster(instance, heroId, partyId);
            instance.Call(() => Assert.True(instance.GetRegisteredObject<MobileParty>(partyId).IsActive));
        }

        Rejoin(defenderClient, DefenderControllerId, partyId);
        PassHours(HoursPastDisbandWait);

        AssertWorkingParty(DefenderControllerId, heroId, partyId);
    }

    /// <summary>
    /// A save already holding a disbanding player party is repaired when its player rejoins, the same restore a
    /// server load runs, without needing a captivity release.
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
        ReturnToServer(Clients.First(), DefenderControllerId, partyId);
        PassHours(HoursPastDisbandWait);

        AssertWorkingParty(DefenderControllerId, heroId, partyId);
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
            var behavior = new DisbandPartyCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { behavior }));
            behavior.RegisterEvents();
        });
    }

    /// <summary>
    /// Starts the native disband while the party is not yet a registered player's, the way a disband started
    /// before <c>DisbandPartyActionPatches</c> guarded player parties, then lets the waiting period run out.
    /// </summary>
    private void StartDisbandBeforeRegistration(string partyId, StaleDisband staleDisband)
    {
        if (staleDisband == StaleDisband.None) return;

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            DisbandPartyAction.StartDisband(party);

            Assert.True(GetDisbandBehavior().IsPartyWaitingForDisband(party));
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
    /// The owning client pays its ransom from the captivity menu: the release request the server applies.
    /// </summary>
    private void Ransom(EnvironmentInstance client, string heroId, string partyId)
    {
        // A ransom releases from a still-active captor, so the server disengages the freed party, which needs a map scene
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius)))
            .ToList();

        client.Call(() =>
        {
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            client.Resolve<INetwork>().SendAll(new NetworkEndPlayerCaptivityAttempted(
                heroId, partyId, party.Position, EndCaptivityDetail.Ransom, null, 0));
        }, disabledMethods);
    }

    /// <summary>
    /// Advances the server clock and runs the disband behavior's hourly tick, which turns an expired wait into a disband.
    /// </summary>
    private void PassHours(int hours)
    {
        Server.Call(() =>
        {
            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Hours(hours).NumTicks;
            ((DisbandPartyCampaignBehavior)GetDisbandBehavior()).HourlyTick();
        });
    }

    /// <summary>
    /// Runs the client's returning connect and validate handshake, which restores its saved party.
    /// </summary>
    private void ReturnToServer(EnvironmentInstance client, string controllerId, string partyId)
    {
        var validated = TestEnvironment.ConnectReturningClient(client, controllerId);
        Assert.Equal(partyId, validated.Player.MobilePartyId);
    }

    /// <summary>
    /// Drops the client and brings it back through the returning handshake.
    /// </summary>
    private void Rejoin(EnvironmentInstance client, string controllerId, string partyId)
    {
        Server.Call(() => Server.SimulateMessage(this, new PlayerDisconnected(client.NetPeer, default)));
        ReturnToServer(client, controllerId, partyId);
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
            Assert.False(GetDisbandBehavior().IsPartyWaitingForDisband(party));
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

    private static IDisbandPartyCampaignBehavior GetDisbandBehavior()
        => Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
}
