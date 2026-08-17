using Common.Messaging;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// Server-authoritative location NPC host election + successor line + epochs (SR-010..SR-017), driven
/// end to end over the mock campaign network: <see cref="LocationMissionReady"/> →
/// <c>NetworkRequestLocationHost</c> → server election → <c>NetworkLocationHostAssigned</c> fan-out.
/// Mirrors the battle <c>HostElectionTests</c>.
/// </summary>
public class LocationHostElectionTests : LocationHostTestEnvironment
{
    public LocationHostElectionTests(ITestOutputHelper output) : base(output, numClients: 3)
    {
    }

    [Fact]
    public void FirstMissionReady_BecomesHost_LaterReadyClientsAppendInOrder()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B", "C");
        var clients = Clients.ToArray();

        MakeLocationMissionReady(clients[0], instanceId);
        AssertLocationHost(Server, instanceId, "A");

        MakeLocationMissionReady(clients[1], instanceId);
        AssertLocationHost(Server, instanceId, "A", "B");

        MakeLocationMissionReady(clients[2], instanceId);
        AssertLocationHost(Server, instanceId, "A", "B", "C");

        // The assignment broadcast reached every client.
        foreach (var client in clients)
            AssertLocationHost(client, instanceId, "A", "B", "C");

        // Idempotent: a duplicate ready request changes nothing.
        MakeLocationMissionReady(clients[1], instanceId);
        AssertLocationHost(Server, instanceId, "A", "B", "C");

        Assert.Equal(1, GetLocationEpoch(Server, instanceId));
    }

    [Fact]
    public void OnlyTheHost_IsLocalHost()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B", "C");
        var clients = Clients.ToArray();

        foreach (var client in clients)
            MakeLocationMissionReady(client, instanceId);

        AssertIsLocalLocationHost(clients[0], instanceId, true);
        AssertIsLocalLocationHost(clients[1], instanceId, false);
        AssertIsLocalLocationHost(clients[2], instanceId, false);
    }

    [Theory]
    [InlineData(true)]  // graceful leave (walked out the settlement door)
    [InlineData(false)] // disconnect
    public void HostDeparture_PromotesFirstSuccessor_AtHigherEpoch(bool wasRetreat)
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B", "C");
        var clients = Clients.ToArray();

        foreach (var client in clients)
            MakeLocationMissionReady(client, instanceId);
        Assert.Equal(1, GetLocationEpoch(Server, instanceId));

        DepartBattle("A", instanceId, wasRetreat);

        AssertLocationHost(Server, instanceId, "B", "C");
        foreach (var client in clients)
            AssertLocationHost(client, instanceId, "B", "C");

        AssertIsLocalLocationHost(clients[1], instanceId, true);
        Assert.Equal(2, GetLocationEpoch(Server, instanceId));
    }

    [Fact]
    public void SuccessorDeparture_DropsFromLine_KeepsEpoch()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B", "C");
        var clients = Clients.ToArray();

        foreach (var client in clients)
            MakeLocationMissionReady(client, instanceId);

        DepartBattle("B", instanceId, wasRetreat: true);

        AssertLocationHost(Server, instanceId, "A", "C");
        Assert.Equal(1, GetLocationEpoch(Server, instanceId));
    }

    [Fact]
    public void EmptyInstance_ClearsServerAssignment_AndReelectionUsesHigherEpoch()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B");
        var clients = Clients.ToArray();

        MakeLocationMissionReady(clients[0], instanceId);
        MakeLocationMissionReady(clients[1], instanceId);
        Assert.Equal(1, GetLocationEpoch(Server, instanceId));

        DepartBattle("B", instanceId, wasRetreat: true);
        DepartBattle("A", instanceId, wasRetreat: true, isInstanceEmpty: true);

        AssertNoLocationHost(Server, instanceId);

        // Re-entry elects fresh — at a HIGHER epoch than the abandoned generation, so clients holding
        // the old assignment apply the new one instead of ignoring it as stale (SR-016).
        MakeLocationMissionReady(clients[1], instanceId);
        AssertLocationHost(Server, instanceId, "B");
        Assert.Equal(2, GetLocationEpoch(Server, instanceId));
        AssertLocationHost(clients[0], instanceId, "B");
        AssertIsLocalLocationHost(clients[1], instanceId, true);
    }

    [Fact]
    public void HostDepartsWithNoReadySuccessors_NextReadyClientElects_AtHigherEpoch()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B");
        var clients = Clients.ToArray();

        // Only A is mission-ready; B is still loading when A leaves (instance NOT empty).
        MakeLocationMissionReady(clients[0], instanceId);
        AssertLocationHost(Server, instanceId, "A");

        DepartBattle("A", instanceId, wasRetreat: true);
        AssertNoLocationHost(Server, instanceId);

        // B finishes loading and elects itself at the next generation.
        MakeLocationMissionReady(clients[1], instanceId);
        AssertLocationHost(Server, instanceId, "B");
        Assert.Equal(2, GetLocationEpoch(Server, instanceId));
    }

    [Fact]
    public void ExHostRejoin_LandsAtSuccessorTail_WithoutPreemptingTheNewHost()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B", "C");
        var clients = Clients.ToArray();

        foreach (var client in clients)
            MakeLocationMissionReady(client, instanceId);

        DepartBattle("A", instanceId, wasRetreat: true);
        AssertLocationHost(Server, instanceId, "B", "C");

        // A re-enters the settlement and becomes mission-ready again: appended, never re-promoted.
        MakeLocationMissionReady(clients[0], instanceId);
        AssertLocationHost(Server, instanceId, "B", "C", "A");
        AssertIsLocalLocationHost(clients[1], instanceId, true);
        AssertIsLocalLocationHost(clients[0], instanceId, false);
    }

    [Fact]
    public void RequestFromPartyOutsideTheSettlement_IsIgnored()
    {
        var (instanceId, _) = SetupSettlementLocation("A");
        var clients = Clients.ToArray();

        // B is a registered player but its party is NOT in the settlement (SetupSettlementLocation only
        // parked A's). Give B an identity + player registration without settlement membership.
        SetControllerId(clients[1], "B");
        var heroId = CreateRegisteredObject<Hero>();
        var partyId = CreateRegisteredObject<TaleWorlds.CampaignSystem.Party.MobileParty>();
        RegisterAsPlayerParty("B", heroId, partyId);

        MakeLocationMissionReady(clients[1], instanceId);
        AssertNoLocationHost(Server, instanceId);

        // The valid participant still elects normally afterwards.
        MakeLocationMissionReady(clients[0], instanceId);
        AssertLocationHost(Server, instanceId, "A");
    }

    [Fact]
    public void StaleLowerEpochAssignment_IsIgnoredByClients()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B");
        var clients = Clients.ToArray();

        MakeLocationMissionReady(clients[0], instanceId);
        MakeLocationMissionReady(clients[1], instanceId);
        DepartBattle("A", instanceId, wasRetreat: true);
        AssertLocationHost(clients[1], instanceId, "B");
        Assert.Equal(2, GetLocationEpoch(clients[1], instanceId));

        // A re-delivered epoch-1 broadcast (out-of-order around the migration) must not overwrite.
        clients[1].Call(() =>
        {
            clients[1].Resolve<IMessageBroker>().Publish(this,
                new NetworkLocationHostAssigned(instanceId, "A", new[] { "B" }, 1));
        });

        AssertLocationHost(clients[1], instanceId, "B");
        Assert.Equal(2, GetLocationEpoch(clients[1], instanceId));
    }

    [Fact]
    public void LocationAndBattleRegistries_AreIsolated()
    {
        // Same controllers (and the same parties) hold a battle AND a location instance simultaneously —
        // the battle parties are parked in a settlement, since a player has exactly one party.
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var instanceId = ParkPartiesInNewSettlement(partyIds);
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        MakeLocationMissionReady(clients[0], instanceId);
        MakeLocationMissionReady(clients[1], instanceId);

        AssertHost(Server, mapEventId, "A", "B");
        AssertLocationHost(Server, instanceId, "A", "B");

        // A location departure touches ONLY the location assignment...
        DepartBattle("A", instanceId, wasRetreat: true);
        AssertLocationHost(Server, instanceId, "B");
        AssertHost(Server, mapEventId, "A", "B");

        // ...and a battle departure touches ONLY the battle assignment.
        DepartBattle("B", mapEventId, wasRetreat: true);
        AssertHost(Server, mapEventId, "A");
        AssertLocationHost(Server, instanceId, "B");
    }
}
