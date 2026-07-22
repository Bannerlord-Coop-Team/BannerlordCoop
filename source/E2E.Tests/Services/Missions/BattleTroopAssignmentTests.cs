using System.Linq;
using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Requirement-coverage tests for troop assignment when a battle is entered:
/// <list type="bullet">
/// <item>BR-020 (Initial Troop Assignment): when a player joins a battle mission it receives control of the
/// troops assigned from its OWN participating party — driven through the real entry path (EnterBattle ->
/// server host election -> <c>NetworkBattleTroopReserve</c> -> the client's side <see cref="CoopTroopSupplier"/>),
/// so the supplier is fed exactly its own party's reserve and no other.</item>
/// <item>BR-012 (NPC Party Control): the mission host controls (is issued the reserve of) every participating
/// NPC party that is not assigned to another player, while a non-host is not — asserted on the server's
/// authoritative <see cref="IBattleTroopReserveBuilder"/> with real map-event objects.</item>
/// </list>
/// </summary>
public class BattleTroopAssignmentTests : MissionTestEnvironment
{
    public BattleTroopAssignmentTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// BR-020: a single player enters its field battle. The server elects it host and feeds it its owned
    /// reserves; the client's own-side supplier must receive EXACTLY its own party's <see cref="PartyReserve"/>
    /// — one party, keyed by that party, at the fresh supplied pointer (0) — and the count of troops the player
    /// can command must equal its assigned roster.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-020")]
    public void PlayerJoinsBattle_SupplierFedWithExactlyItsOwnPartyReserve()
    {
        // client0 = "enemy" (attacker side), client1 = "solo" (defender side).
        var (mapEventId, _) = SetupCoopBattle("enemy", "solo");
        var solo = Clients.Last();
        const int assignedTroops = 3;

        string soloOwnPartyId = null; // the MapEventParty id the reserve is keyed by

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));

            // "solo" is the only party on the defender side — give it a known, battle-ready roster.
            var ownParty = Assert.Single(mapEvent.DefenderSide.Parties);
            var troop = Server.CreateRegisteredObject<CharacterObject>("br020_assigned_troop");
            ownParty.Party.MemberRoster.Clear();
            ownParty.Party.MemberRoster.AddToCounts(troop, assignedTroops);
            ownParty.Update();

            Assert.True(Server.ObjectManager.TryGetId(ownParty, out soloOwnPartyId));
        });

        // Stand in for the mission's real (injection-patched) supplier for the entrant's own side, so the
        // reserve the server delivers over the network lands somewhere observable in this headless harness.
        var ownSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null);
        CoopTroopSupplierRegistry.Register(ownSupplier);
        try
        {
            EnterBattle(solo, mapEventId); // join the battle: server elects host + sends owned reserves

            // Fed exactly its own party's reserve: one party, that party, nothing supplied yet.
            var supplied = ownSupplier.GetSuppliedByParty();
            var only = Assert.Single(supplied);
            Assert.Equal(soloOwnPartyId, only.partyId);
            Assert.Equal(0, only.supplied);

            // The troops the player receives control of equal the troops assigned from its own party.
            Assert.Equal(assignedTroops, ownSupplier.GetNumberOfPlayerControllableTroops());
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-012: an NPC (player-unowned) party fights alongside the host. The host's owned-reserve set must
    /// include that NPC party (the host controls it) plus the host's own party, but NOT a party assigned to
    /// another player; a non-host player must NOT be issued the NPC party. Asserted on the server's
    /// authoritative reserve builder.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-012")]
    public void HostControlsUnownedNpcParty_ButNotAnotherPlayersParty()
    {
        // "host" (attacker side) becomes the mission host; "playerB" (defender side) is a non-host participant.
        var (mapEventId, partyIds) = SetupCoopBattle("host", "playerB");
        var aiPartyMobileId = CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiPartyMobileId, out var aiParty));

            // An AI party with no player owner, fighting on the host's (attacker) side.
            aiParty.Party.MapEventSide = mapEvent.AttackerSide;
            var aiMep = mapEvent.AttackerSide.Parties.Last(p => p.Party == aiParty.Party);
            var npcTroop = Server.CreateRegisteredObject<CharacterObject>("br012_npc_troop");
            aiMep.Party.MemberRoster.Clear();
            aiMep.Party.MemberRoster.AddToCounts(npcTroop, 3);
            aiMep.Update();
            Assert.True(Server.ObjectManager.TryGetId(aiMep, out var aiMapEventPartyId));

            // The two players' MapEventParty ids (host on attacker, playerB on defender).
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var hostMobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var playerBMobileParty));
            var hostMep = mapEvent.AttackerSide.Parties.Single(p => p.Party == hostMobileParty.Party);
            var playerBMep = mapEvent.DefenderSide.Parties.Single(p => p.Party == playerBMobileParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(hostMep, out var hostMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(playerBMep, out var playerBMapEventPartyId));

            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var hostOwned = builder.GetOwnedReserves(mapEvent, "host", isHost: true)
                .SelectMany(side => side.Parties).Select(party => party.PartyId).ToArray();
            var nonHostOwned = builder.GetOwnedReserves(mapEvent, "playerB", isHost: false)
                .SelectMany(side => side.Parties).Select(party => party.PartyId).ToArray();

            // BR-012: the host controls the unowned NPC party...
            Assert.Contains(aiMapEventPartyId, hostOwned);
            // ...and its own party...
            Assert.Contains(hostMapEventPartyId, hostOwned);
            // ...but never a party assigned to another player.
            Assert.DoesNotContain(playerBMapEventPartyId, hostOwned);

            // The NPC party is the HOST's responsibility only — a non-host player is not issued it.
            Assert.DoesNotContain(aiMapEventPartyId, nonHostOwned);
            // A non-host still receives its own party.
            Assert.Contains(playerBMapEventPartyId, nonHostOwned);
        }, MapEventDisabledMethods);
    }
}

/// <summary>
/// BR-020 unit coverage for <see cref="CoopTroopSupplier.GetNumberOfPlayerControllableTroops"/>: the player
/// commands every troop in the reserve assigned to it, so the controllable count equals the owned entry count
/// (summed across the parties on the side). Pure supplier state — no game objects or environment needed.
/// </summary>
public class CoopTroopSupplierControllableCountTests
{
    private static TroopReserveEntry[] Entries(int count, int seedBase)
    {
        var entries = new TroopReserveEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = new TroopReserveEntry(seedBase + i, $"Char_{i}", formationClass: 0);
        return entries;
    }

    [Fact]
    [Trait("Requirement", "BR-020")]
    public void PlayerControllableTroops_EqualsOwnedEntryCount()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Attacker, null);
        supplier.SetReserve(new[] { new PartyReserve("own", 0, Entries(5, seedBase: 500)) });

        Assert.Equal(5, supplier.GetNumberOfPlayerControllableTroops());
    }

    [Fact]
    [Trait("Requirement", "BR-020")]
    public void PlayerControllableTroops_SumsAcrossOwnedParties()
    {
        var supplier = new CoopTroopSupplier("M1", BattleSideEnum.Defender, null);
        supplier.SetReserve(new[]
        {
            new PartyReserve("p1", 0, Entries(3, seedBase: 100)),
            new PartyReserve("p2", 0, Entries(2, seedBase: 200)),
        });

        Assert.Equal(5, supplier.GetNumberOfPlayerControllableTroops());
    }
}
