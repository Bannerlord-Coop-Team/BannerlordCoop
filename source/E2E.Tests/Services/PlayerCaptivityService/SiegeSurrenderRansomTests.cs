using Common.Network;
using Common.Network.Messages;
using Common.Util;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Entity;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Net;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PlayerCaptivityService;

/// <summary>
/// #2891: a player who surrendered in a PvP siege and was ransomed must get back a party that keeps its hero
/// and leader. A disband started on the party before player parties were guarded against it is saved with the
/// party, and native <see cref="DisbandPartyCampaignBehavior"/> strips the party again a day later at most.
/// </summary>
public class SiegeSurrenderRansomTests : MapEventTestBase
{
    private const string BesiegerControllerId = "Besieger";
    private const string DefenderControllerId = "Defender";

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
    public void RansomAfterSiegeSurrender_PartyKeepsItsHeroThroughADayAndARejoin(StaleDisband staleDisband)
    {
        var siege = CreateSiegeMapEvent(MapEvent.BattleTypes.Siege);
        var partyId = siege.DefenderPartyId;
        var defenderClient = Clients.Last();
        InstallDisbandBehavior();
        StartDisbandBeforeRegistration(partyId, staleDisband);

        RegisterOwnerAsPlayer(BesiegerControllerId, siege.AttackerPartyId);
        var heroId = RegisterOwnerAsPlayer(DefenderControllerId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), BesiegerControllerId);
        TestEnvironment.ConnectRegisteredPlayer(defenderClient, DefenderControllerId);

        Server.Call(() => Server.SimulateMessage(defenderClient.NetPeer,
            new NetworkPlayerSurrendered(partyId, siege.MapEventId)), BattleMenuSurrenderDisabledMethods());

        AssertCaptivity(Server, heroId, siege.AttackerPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, heroId, siege.AttackerPartyId);

        Ransom(defenderClient, heroId, partyId);
        PassHours(25);

        AssertCaptivity(Server, heroId, null);
        AssertWorkingParty(heroId, partyId);
        foreach (var instance in Clients.Append(Server))
        {
            // Captivity forfeits the troops, so the released party holds exactly its hero
            AssertHeroInPartyRoster(instance, heroId, partyId);
            instance.Call(() => Assert.True(instance.GetRegisteredObject<MobileParty>(partyId).IsActive));
        }

        Rejoin(defenderClient, DefenderControllerId, partyId);
        PassHours(25);

        AssertWorkingParty(heroId, partyId);
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
        var client = Clients.First();
        InstallDisbandBehavior();
        StartDisbandBeforeRegistration(partyId, staleDisband);

        var heroId = RegisterOwnerAsPlayer(DefenderControllerId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(client, DefenderControllerId);

        Rejoin(client, DefenderControllerId, partyId);
        PassHours(25);

        AssertWorkingParty(heroId, partyId);
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
        {
            PassHours(25);
            Server.Call(() => Assert.True(Server.GetRegisteredObject<MobileParty>(partyId).IsDisbanding));
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
    /// Drops the client and runs its returning connect and validate handshake, which restores its saved party.
    /// </summary>
    private void Rejoin(EnvironmentInstance client, string controllerId, string partyId)
    {
        var endPoint = (IPEndPoint)client.NetPeer;
        if (endPoint.Address == null)
        {
            endPoint.Address = IPAddress.Loopback;
            endPoint.Port = 4200;
        }

        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        Server.NetworkSentImmediateMessages.Clear();

        // The join save transfer after resolution needs a real SaveHandler, which the harness campaign lacks
        var disabledMethods = new MethodBase[]
        {
            AccessTools.Method(typeof(ResolveCharacterState), nameof(ResolveCharacterState.TransferSave)),
        };

        Server.Call(() =>
        {
            Server.SimulateMessage(this, new PlayerDisconnected(client.NetPeer, default));
            Server.SimulateMessage(this, new PlayerConnected(client.NetPeer));
            Server.SimulateMessage(client.NetPeer, new NetworkClientValidate(controllerId));
        }, disabledMethods);
        // Runs the deferred park of the dropped party
        Server.PumpGameThread();

        var validated = Assert.Single(Server.NetworkSentImmediateMessages.GetMessages<NetworkClientValidated>());
        Assert.True(validated.HeroExists, "The returning player was sent to character creation.");
        Assert.Equal(partyId, validated.Player.MobilePartyId);
        Assert.IsType<ResolveCharacterState>(Server.Resolve<ConnectionCollection>().ConnectionStates[client.NetPeer].State);
    }

    /// <summary>
    /// The party is still the registered player's, keeps its hero as leader, and isn't disbanding anywhere.
    /// </summary>
    private void AssertWorkingParty(string heroId, string partyId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var hero = Server.GetRegisteredObject<Hero>(heroId);

            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(DefenderControllerId, out var player));
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
