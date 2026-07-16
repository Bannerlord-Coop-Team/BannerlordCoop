using GameInterface.Services.MapEvents.TroopSupply;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Tests the authoritative troop reserves supplied to coop battle owners.</summary>
public class BattleTroopReserveBuilderTests : MissionTestEnvironment
{
    public BattleTroopReserveBuilderTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GetOwnedReserves_ExcludesTroopsThatCannotJoinBattle()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.DefenderSide.Parties[0];
            var troop = Server.CreateRegisteredObject<CharacterObject>("reserve_eligibility_troop");
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));

            party.Party.MemberRoster.Clear();
            party.Party.MemberRoster.AddToCounts(troop, 4, woundedCount: 1);
            party.Update();

            var battleReady = party.Troops
                .Where(element => !element.IsWounded && !element.IsRouted && !element.IsKilled)
                .ToArray();
            Assert.Equal(3, battleReady.Length);

            party.OnTroopKilled(battleReady[0].Descriptor);
            party.OnTroopRouted(battleReady[1].Descriptor);

            var reserves = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(mapEvent, "defender", isHost: false);
            var defenderReserve = reserves.Single(reserve => reserve.Side == BattleSideEnum.Defender);
            var partyReserve = Assert.Single(defenderReserve.Parties);
            var entry = Assert.Single(partyReserve.Entries);

            Assert.Equal(troopId, entry.CharacterId);
        });
    }
}
