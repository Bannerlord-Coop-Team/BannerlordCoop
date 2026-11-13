#if COOP_NAVAL
using Missions.Naval;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalShipBudgetTests
{
    [Fact]
    public void SplitPlayerSideParties_TwoPlayersOnOneSide_FieldsOnlyTheOwnParty()
    {
        var ownParty = CreateParty();
        var otherPlayerParty = CreateParty();
        var own = CreateMapEventParty(ownParty);
        var otherPlayer = CreateMapEventParty(otherPlayerParty);

        NavalShipBudget.SplitPlayerSideParties(
            new MBList<MapEventParty> { otherPlayer, own },
            ownParty,
            out var ownMapEventParty,
            out var ownTeamParties,
            out var allyTeamParties);

        Assert.Same(own, ownMapEventParty);
        Assert.Equal(new[] { own }, ownTeamParties);
        Assert.Equal(new[] { otherPlayer }, allyTeamParties);
    }

    private static PartyBase CreateParty() =>
        (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));

    private static MapEventParty CreateMapEventParty(PartyBase party)
    {
        var mapEventParty = (MapEventParty)RuntimeHelpers.GetUninitializedObject(typeof(MapEventParty));
        mapEventParty.Party = party;
        return mapEventParty;
    }
}
#endif
