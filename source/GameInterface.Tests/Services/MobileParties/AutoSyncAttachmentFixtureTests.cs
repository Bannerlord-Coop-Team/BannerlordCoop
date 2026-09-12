#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MobileParties.Commands;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(ModInformationRoleCollection.Name)]
public class AutoSyncAttachmentFixtureTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose() => ModInformation.IsServer = wasServer;

    [Fact]
    public void ClientMutationFailsBeforeAccessingWorld()
    {
        ModInformation.IsServer = false;
        var fixture = new AutoSyncAttachmentFixture(null, null);

        var result = fixture.Execute(Args("child", "parent", "attach"), true);

        Assert.False(result.Succeeded);
        Assert.Equal("Run attachment on the server.", result.Output);
    }

    [Theory]
    [InlineData("attach")]
    [InlineData("detach")]
    public void RunningCampaignFailsBeforeResolvingParties(string action)
    {
        ModInformation.IsServer = true;
        var time = new Mock<ITimeControlInterface>();
        time.Setup(value => value.GetTimeControl()).Returns(TimeControlEnum.Play_1x);
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var fixture = new AutoSyncAttachmentFixture(objects.Object, time.Object);

        var result = fixture.Execute(Args("child", "parent", action), true);

        Assert.False(result.Succeeded);
        Assert.Equal("Pause the campaign before changing attachment.", result.Output);
        objects.VerifyNoOtherCalls();
    }

    private static ICoopCommandArgs Args(params string[] values) =>
        new CoopCommandArgsFactory().FromValues(values);
}
#endif
