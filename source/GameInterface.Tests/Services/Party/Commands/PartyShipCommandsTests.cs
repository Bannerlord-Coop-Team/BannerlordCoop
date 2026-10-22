using Common;
using Common.Commands;
using GameInterface.Services.Party.Commands;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Party.Commands;

[Collection(ModInformationRoleCollection.Name)]
public class PartyShipCommandsTests
{
    [Theory]
    [InlineData(false, true, "Run this command on the server.")]
    [InlineData(true, false, "NavalDLC is not active on the server.")]
    public void AddShip_RejectsInvalidRequestsBeforeTouchingTheCampaign(bool isServer, bool navalDlcActive, string expectedOutput)
    {
        var result = RunWithRole(isServer, navalDlcActive, new PartyShipCommands.AddShipCoopCommand(), "PlayerOne");

        Assert.False(result.Succeeded);
        Assert.Equal(expectedOutput, result.Output);
    }

    [Theory]
    [InlineData(false, "Run this command on the server.", "PlayerOne")]
    [InlineData(true, "count must be an integer from 1 to 20.", "PlayerOne", "0")]
    [InlineData(true, "count must be an integer from 1 to 20.", "PlayerOne", "many")]
    public void AddTroops_RejectsInvalidRequestsBeforeTouchingTheCampaign(bool isServer, string expectedOutput, params string[] args)
    {
        var result = RunWithRole(isServer, true, new PartyShipCommands.AddTroopsCoopCommand(), args);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedOutput, result.Output);
    }

#if DEBUG
    [Fact]
    public void NavalFleet_RequiresNavalDlc()
    {
        var result = RunWithRole(true, false, new PartyShipCommands.NavalFleetCoopCommand());

        Assert.False(result.Succeeded);
        Assert.Equal("NavalDLC is not active.", result.Output);
    }
#endif

    [Fact]
    public void ListShips_RequiresNavalDlc()
    {
        var result = RunWithRole(true, false, new PartyShipCommands.ListShipsCoopCommand(), "PlayerOne");

        Assert.False(result.Succeeded);
        Assert.Equal("NavalDLC is not active.", result.Output);
    }

    [Theory]
    [InlineData(false, true, "Run this command on the server.", "PlayerOne", "0", "200")]
    [InlineData(true, false, "NavalDLC is not active on the server.", "PlayerOne", "0", "200")]
    [InlineData(true, true, "damage must be a number greater than 0.", "PlayerOne", "0", "0")]
    [InlineData(true, true, "damage must be a number greater than 0.", "PlayerOne", "0", "lots")]
    public void DamageShip_RejectsInvalidRequestsBeforeTouchingTheCampaign(bool isServer, bool navalDlcActive, string expectedOutput, params string[] args)
    {
        var result = RunWithRole(isServer, navalDlcActive, new PartyShipCommands.DamageShipCoopCommand(), args);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedOutput, result.Output);
    }

    [Theory]
    [InlineData(false, true, "Run this command on the server.")]
    [InlineData(true, false, "NavalDLC is not active on the server.")]
    public void RemoveShip_RejectsInvalidRequestsBeforeTouchingTheCampaign(bool isServer, bool navalDlcActive, string expectedOutput)
    {
        var result = RunWithRole(isServer, navalDlcActive, new PartyShipCommands.RemoveShipCoopCommand(), "PlayerOne", "0");

        Assert.False(result.Succeeded);
        Assert.Equal(expectedOutput, result.Output);
    }

    private static readonly PartyCandidate PlayerParty = new PartyCandidate("Player", "Player", "Hero_Player", "Player3325");
    private static readonly PartyCandidate OtherPlayerParty = new PartyCandidate("Player538", "Player538", "Hero_Player538", "Player538");
    private static readonly PartyCandidate LordParty = new PartyCandidate("lord_1_1_party_1", "Lucon's Party", "lord_1_1", "Lucon");
    private static readonly PartyCandidate NamesakeLordParty = new PartyCandidate("lord_2_party_1", "Player's Party", "lord_2", "Player3325");

    [Theory]
    [InlineData("Player", "Player")]
    [InlineData("player3325", "Player")]
    [InlineData("Hero_Player538", "Player538")]
    [InlineData("Lucon", "lord_1_1_party_1")]
    [InlineData("lucon's party", "lord_1_1_party_1")]
    public void TryMatchByName_FindsThePartyByIdPartyNameOrLeaderName(string name, string expectedPartyId)
    {
        var error = MatchName(name, out var match);

        Assert.Null(error);
        Assert.Equal(expectedPartyId, match.PartyId);
    }

    [Fact]
    public void TryMatchByName_PrefersAPlayerPartyOverALordWithTheSameName()
    {
        var error = MatchName("Player3325", out var match);

        Assert.Null(error);
        Assert.Same(PlayerParty, match);
    }

    [Fact]
    public void TryMatchByName_RefusesANameSeveralPartiesInOneTierAnswerTo()
    {
        var namesake = new PartyCandidate("Player_copy", "Copy", "Hero_Copy", "Player3325");
        var error = PartyShipCommands.TryMatchByName("Player3325", candidate => candidate.Names, out PartyCandidate match,
            new[] { PlayerParty, namesake });

        Assert.Null(match);
        Assert.Contains("2 parties answer to Player3325", error);
    }

    [Fact]
    public void TryMatchByName_ReportsANameNoPartyAnswersTo()
    {
        var error = MatchName("testclient1", out var match);

        Assert.Null(match);
        Assert.Contains("testclient1", error);
    }

    [Fact]
    public void TryMatchByName_IgnoresMissingNames()
    {
        var unnamed = new PartyCandidate("MobileParty_1", null!, null!, null!);
        var error = PartyShipCommands.TryMatchByName("MobileParty_1", candidate => candidate.Names, out PartyCandidate match,
            new[] { unnamed });

        Assert.Null(error);
        Assert.Same(unnamed, match);
    }

    // Matches against the player parties first, then every lord party, as fleet does.
    private static string MatchName(string name, out PartyCandidate match) =>
        PartyShipCommands.TryMatchByName(name, candidate => candidate.Names, out match,
            new[] { PlayerParty, OtherPlayerParty },
            new[] { PlayerParty, OtherPlayerParty, LordParty, NamesakeLordParty });

    // A party's id, name and its leader's id and name, as fleet reads them from a MobileParty.
    private sealed class PartyCandidate
    {
        // Assigns the candidate's names.
        public PartyCandidate(string partyId, string partyName, string leaderId, string leaderName)
        {
            PartyId = partyId;
            Names = new[] { partyId, partyName, leaderId, leaderName };
        }

        public string PartyId { get; }
        public string[] Names { get; }
    }

    private static CoopCommandResult RunWithRole(bool isServer, bool navalDlcActive, ICoopCommand command, params string[] args)
    {
        bool originalIsServer = ModInformation.IsServer;
        bool originalNavalDlcActive = ModInformation.IsNavalDlcActive;
        try
        {
            ModInformation.IsServer = isServer;
            ModInformation.IsNavalDlcActive = navalDlcActive;
            return command.ProcessCommand(new TestArgs(args));
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            ModInformation.IsNavalDlcActive = originalNavalDlcActive;
        }
    }

    private sealed class TestArgs : ICoopCommandArgs
    {
        private readonly IReadOnlyList<string> values;

        public TestArgs(IReadOnlyList<string> values)
        {
            this.values = values;
        }

        public int Count => values.Count;

        public string this[int index] => values[index];

        public IEnumerator<string> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
