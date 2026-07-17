using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// End-to-end tests for BR-004 Player Eligibility: only a player whose party is a valid participant in the
/// corresponding map event may join (host) the battle mission. The server's
/// <c>BattleHostHandler.IsRequesterInBattle</c> guard rejects a host-election request from a controller whose
/// party is not in the map event, so no host/successor entry is ever created for that outsider. Uses three
/// players (two valid participants + one outsider) and the campaign <c>INetwork</c> round-trip the E2E mock
/// router replicates.
/// </summary>
public class BattleEligibilityTests : MissionTestEnvironment
{
    public BattleEligibilityTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    [Trait("Requirement", "BR-004")]
    public void NonParticipantHostRequest_IsRejected_NoHostEntryCreated()
    {
        // ctrl-A and ctrl-B are valid map-event participants; the third client is an OUTSIDER whose party is
        // never added to this map event.
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId); // ctrl-A, a valid participant, is the sole host so far
        AssertHost(Server, mapEventId, "ctrl-A");

        // Register the outsider: a player whose party exists on the server but was never joined to the map event
        // (so its MobileParty.MapEvent is null — not this battle).
        var outsiderPartyId = CreateRegisteredObject<MobileParty>(MapEventDisabledMethods);
        var outsiderHeroId = CreateRegisteredObject<Hero>();
        SetControllerId(clients[2], "ctrl-Outsider");
        RegisterAsPlayerParty("ctrl-Outsider", outsiderHeroId, outsiderPartyId);

        // The outsider tries to join/host the battle. BR-004: it is not a valid participant, so the server's
        // eligibility guard rejects the election request — the outsider must not become host nor be appended to
        // the successor line, on any instance.
        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A");
        AssertIsLocalHost(clients[2], mapEventId, false); // the outsider never became host
    }
}
