using Common;
using Common.Commands;
using System.Collections;
using System.Collections.Generic;
using Xunit;
using StartNavalCoopCommand = GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartNavalCoopCommand;

namespace GameInterface.Tests.Services.MapEvents.Commands;

[Collection(ModInformationRoleCollection.Name)]
public class StartNavalCoopCommandTests
{
    [Theory]
    [InlineData(false, true, "Run this command on the server.", "PlayerOne")]
    [InlineData(true, false, "NavalDLC is not active on the server.", "PlayerOne")]
    [InlineData(true, true, "The two players must be different.", "PlayerOne", "PlayerOne")]
    public void ProcessCommand_RejectsInvalidRequestsBeforeTouchingTheCampaign(
        bool isServer, bool navalDlcActive, string expectedOutput, params string[] args)
    {
        bool originalIsServer = ModInformation.IsServer;
        bool originalNavalDlcActive = ModInformation.IsNavalDlcActive;
        try
        {
            ModInformation.IsServer = isServer;
            ModInformation.IsNavalDlcActive = navalDlcActive;

            var result = new StartNavalCoopCommand().ProcessCommand(new TestArgs(args));

            Assert.False(result.Succeeded);
            Assert.Equal(expectedOutput, result.Output);
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
