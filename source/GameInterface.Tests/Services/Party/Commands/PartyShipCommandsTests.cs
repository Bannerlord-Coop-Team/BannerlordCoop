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

    [Fact]
    public void ListShips_RequiresNavalDlc()
    {
        var result = RunWithRole(true, false, new PartyShipCommands.ListShipsCoopCommand(), "PlayerOne");

        Assert.False(result.Succeeded);
        Assert.Equal("NavalDLC is not active.", result.Output);
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
