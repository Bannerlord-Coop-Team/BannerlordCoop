#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.UI.Commands;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class CharacterNotificationFixtureCommandsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void FixtureCommand_WrongSide_RejectsBeforeAccessingCampaignOrArguments(int command, bool isServer)
    {
        var wasServer = ModInformation.IsServer;
        var args = new Mock<ICoopCommandArgs>(MockBehavior.Strict);
        ICoopCommand fixture = command switch
        {
            0 => new CharacterNotificationDebugCommand.CharacterNotificationPrepareCoopCommand(null, null, null),
            1 => new CharacterNotificationDebugCommand.CharacterNotificationAdvanceCoopCommand(null, null),
            _ => new CharacterNotificationDebugCommand.CharacterNotificationDismissCoopCommand(),
        };
        try
        {
            ModInformation.IsServer = isServer;
            var result = fixture.ProcessCommand(args.Object);
            Assert.False(result.Succeeded);
            args.VerifyNoOtherCalls();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
#endif
