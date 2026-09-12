using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Messages;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-052 (Retreat Casualties): troops killed, wounded, captured, routed, or otherwise lost BEFORE a
/// retreat shall not be restored merely because the party retreated. This drives a real retreat departure
/// (<see cref="MissionMemberDeparted"/> with <c>wasRetreat: true</c>), which the server-side host handler
/// turns into <c>BattleTroopReserveBuilder.ForgetController</c> — dropping the retreating controller's
/// reserve so a re-engagement re-flattens its party fresh. The requirement then demands the re-flatten
/// exclude the casualties taken before the retreat, which is what the rebuilt reserve is asserted to do.
/// </summary>
public class BattleRetreatCasualtyTests : MissionTestEnvironment
{
    public BattleRetreatCasualtyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-052")]
    public void RetreatThenReEngage_DoesNotRestoreCasualtiesTakenBeforeRetreat()
    {
        // Two players in one field battle: "host-ctrl" (attacker) becomes host, "retreat-ctrl" (defender)
        // is the successor that will retreat.
        var (mapEventId, _) = SetupCoopBattle("host-ctrl", "retreat-ctrl");
        var clients = Clients.ToArray();

        // Seed the retreating player's party with five identical troops BEFORE any reserve is flattened, and
        // build its flattened battle roster the way the engine does (OnTroopKilled/Wounded then key off it).
        var troop = Server.CreateRegisteredObject<CharacterObject>("retreat_casualty_troop");
        string retreatPartyId = null;
        string troopId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var defenderParty = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(defenderParty, out retreatPartyId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out troopId));

            defenderParty.Party.MemberRoster.AddToCounts(troop, 5);
            defenderParty.Update();
            Assert.Equal(5, CountAvailable(defenderParty.Troops, troop));
        });

        // Both players become mission-ready: host-ctrl is elected host, retreat-ctrl joins the successor line.
        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);

        // Before the retreat the defender's authoritative reserve holds the party's full fighting roster —
        // the harness party's own (varying) men plus the five seeded troops. Capture it as the baseline the
        // casualties are measured against.
        int reserveBeforeCasualties = GetDefenderReserveEntryCount(mapEventId, "retreat-ctrl");
        Assert.True(reserveBeforeCasualties >= 5,
            $"defender reserve should include the 5 seeded troops, has {reserveBeforeCasualties} entries");

        // Mid-battle casualties reported through the real casualty pipeline: two killed, one wounded.
        Server.Call(() =>
        {
            var broker = Server.Resolve<IMessageBroker>();
            broker.Publish(this, new NetworkRequestBattleCasualty(retreatPartyId, troopId, wounded: false));
            broker.Publish(this, new NetworkRequestBattleCasualty(retreatPartyId, troopId, wounded: false));
            broker.Publish(this, new NetworkRequestBattleCasualty(retreatPartyId, troopId, wounded: true));

            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.Equal(2, CountAvailable(mapEvent.DefenderSide.Parties[0].Troops, troop));
        });

        // The player retreats: a real graceful departure (wasRetreat) which forgets its reserve so a rejoin
        // re-flattens the party fresh.
        DepartBattle("retreat-ctrl", mapEventId, wasRetreat: true);

        // Re-engaging re-flattens the party FROM SCRATCH. BR-052: the three troops lost before the retreat
        // must NOT be restored by the re-flatten — the rebuilt reserve is exactly three men short of the
        // pre-casualty baseline.
        Assert.Equal(reserveBeforeCasualties - 3, GetDefenderReserveEntryCount(mapEventId, "retreat-ctrl"));
    }

    private int GetDefenderReserveEntryCount(string mapEventId, string controllerId)
    {
        int count = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var reserves = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(mapEvent, controllerId, isHost: false);

            var defenderReserve = reserves.Single(reserve => reserve.Side == BattleSideEnum.Defender);
            var partyReserve = Assert.Single(defenderReserve.Parties);
            count = partyReserve.Entries.Length;
        });
        return count;
    }

    /// <summary>Counts troops of <paramref name="troop"/> still available to fight — the ones a reserve
    /// rebuild keeps (killed, wounded and routed are excluded exactly as <c>FlattenParty</c> excludes them).</summary>
    private static int CountAvailable(FlattenedTroopRoster roster, CharacterObject troop)
    {
        int n = 0;
        foreach (var element in roster)
            if (!element.IsKilled && !element.IsWounded && !element.IsRouted && element.Troop == troop) n++;
        return n;
    }
}
