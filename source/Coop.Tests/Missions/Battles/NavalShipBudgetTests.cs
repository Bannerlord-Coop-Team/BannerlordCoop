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

    // The allies get what the players' fielded ships leave of the side's eight, never more than their own limit nor below none.
    // Row one has the 6+6 shape, with the player's share of 4 given as input: an AI lord limited to 6 gets the other 4.
    [Theory]
    [InlineData(new[] { 4 }, 6, 4)]
    [InlineData(new[] { 3 }, 2, 2)]
    [InlineData(new[] { 4, 4 }, 6, 0)]
    [InlineData(new[] { 5, 4 }, 6, 0)]
    [InlineData(new int[] { }, 8, 8)]
    public void LeftForAi_IsWhatThePlayersLeaveOfTheSidesEight_AtMostTheAiLimit(int[] playerFleets, int aiLimit, int expected)
    {
        Assert.Equal(expected, NavalShipBudget.LeftForAi(playerFleets, aiLimit));
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
