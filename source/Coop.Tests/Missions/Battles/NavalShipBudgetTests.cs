#if COOP_NAVAL
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Ships;
using Missions.Naval;
using Moq;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalShipBudgetTests
{
    // The own party fields against the other players only, so AI lords joining after the players opened never shrink its share.
    // Row one puts the other player first, where vanilla would take it as the own party; row two has two AI lords joining after
    // two players, and only the other player is the ally team.
    [Theory]
    [InlineData("P2 P1", "P2")]
    [InlineData("P1 P2 AI1 AI2", "P2")]
    public void SplitPlayerSideParties_FieldsTheOwnPartyAgainstTheOtherPlayersOnly(string side, string allies)
    {
        var parties = side.Split(' ').ToDictionary(name => name, name => CreateMapEventParty(CreateParty()));
        var players = parties.Where(entry => entry.Key.StartsWith("P")).Select(entry => entry.Value.Party).ToHashSet();

        NavalShipBudget.SplitPlayerSideParties(
            new MBList<MapEventParty>(parties.Values),
            parties["P1"].Party,
            players.Contains,
            out var ownMapEventParty,
            out var ownTeamParties,
            out var allyTeamParties);

        Assert.Same(parties["P1"], ownMapEventParty);
        Assert.Equal(new[] { parties["P1"] }, ownTeamParties);
        Assert.Equal(allies.Split(' ').Select(name => parties[name]), allyTeamParties);
    }

    // The allies get what the players' fielded ships leave of the side's eight, never more than their own limit nor below none.
    // Row one has the 6+6 shape, with the player's share of 4 given as input: an AI lord limited to 6 gets the other 4.
    [Theory]
    [InlineData(4, 6, 4)]
    [InlineData(3, 2, 2)]
    [InlineData(8, 6, 0)]
    [InlineData(9, 6, 0)]
    public void LeftForAi_IsWhatThePlayersLeaveOfTheSidesEight_AtMostTheAiLimit(int playerHulls, int aiLimit, int expected)
    {
        Assert.Equal(expected, NavalShipBudget.LeftForAi(playerHulls, aiLimit));
    }

    // A player the side's AI fleet attempt didn't count gets the eight less the attempt and every uncounted player before it, at
    // the most that player can field, and at least its own ship. Rows: the phase 0 joiner onto a full side (3 + 5 AI, then 4),
    // room the AI left, two joiners in party order, one the attempt counted (it joined before it) and an uncounted
    // player after the own one, which counts the own one instead.
    [Theory]
    [InlineData("P1 AI J", "P1", "", 8, "J", 1)]
    [InlineData("P1 AI J", "P1", "", 5, "J", 3)]
    [InlineData("P1 AI J1 J2", "P1", "J1=2", 5, "J2", 1)]
    [InlineData("P1 AI J1 J2", "P1 J1", "", 8, "J1", 8)]
    [InlineData("P1 AI J1 J2", "P1", "J2=3", 5, "J1", 3)]
    public void LateJoinerCap_IsWhatTheAttemptAndTheEarlierJoinersLeave(string side, string counted, string mostFielded,
        int recordedHulls, string own, int expected)
    {
        var parties = side.Split(' ');
        var countedParties = counted.Split(' ');
        var fleets = mostFielded.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(entry => entry.Split('=')[0], entry => int.Parse(entry.Split('=')[1]));

        int cap = NavalShipBudget.LateJoinerCap(parties, parties.First(party => party == own),
            party => party != "AI" && !countedParties.Contains(party), party => fleets[party], recordedHulls);

        Assert.Equal(expected, cap);
    }

    // Only a player's party the attempt didn't count is uncounted; an attempt that counted none arrives with null ids, and a
    // player party without an id is left out, as the attempt left it out.
    [Theory]
    [InlineData(false, true, "MobileParty_P", false)]
    [InlineData(true, true, "MobileParty_P", false)]
    [InlineData(true, true, "MobileParty_Q", true)]
    [InlineData(true, true, null, true)]
    [InlineData(true, false, null, false)]
    public void IsUncountedPlayer_IsAPlayersPartyTheAttemptDidNotCount(bool isPlayer, bool hasId, string counted, bool expected)
    {
        var party = CreateParty();
        party.MobileParty = (MobileParty)RuntimeHelpers.GetUninitializedObject(typeof(MobileParty));
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(players => players.Contains(party.MobileParty)).Returns(isPlayer);
        var objectManager = new Mock<IObjectManager>();
        string partyId = "MobileParty_P";
        objectManager.Setup(objects => objects.TryGetIdWithLogging(party.MobileParty, out partyId)).Returns(hasId);
        var budget = new NavalShipBudget(playerManager.Object, objectManager.Object, Mock.Of<INavalNpcFleetAttemptTracker>());

        Assert.Equal(expected, budget.IsUncountedPlayer(party, counted == null ? null : new[] { counted }));
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
