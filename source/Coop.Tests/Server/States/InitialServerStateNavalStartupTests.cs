#if DEBUG
using Common;
using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.States;
using GameInterface.Registry;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Server.States;

[Collection(ModInformationRoleCollection.Name)]
public sealed class InitialServerStateNavalStartupTests : IDisposable
{
    private readonly bool previousRole = ModInformation.IsServer;
    private readonly bool previousNavalDlc = ModInformation.IsNavalDlcActive;

    public InitialServerStateNavalStartupTests()
    {
        ModInformation.IsServer = true;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Start_StartsNewCampaignOnlyWithNavalDlc(bool navalDlcActive)
    {
        ModInformation.IsNavalDlcActive = navalDlcActive;
        var game = new Mock<IGameStateInterface>();
        using var broker = new MessageBroker();
        using var state = new InitialServerState(Mock.Of<IServerLogic>(), broker, Mock.Of<IRegistryManager>(),
            Mock.Of<IMapEventLoadCleaner>(), Mock.Of<IModuleInfoProvider>(), Mock.Of<IModuleValidator>(),
            game.Object, Mock.Of<ILoadingInterface>());

        state.Start();

        game.Verify(value => value.StartNewGame(), navalDlcActive ? Times.Once() : Times.Never());
        game.Verify(value => value.LoadGame("MP"), navalDlcActive ? Times.Never() : Times.Once());
        game.VerifyNoOtherCalls();
    }

    public void Dispose()
    {
        ModInformation.IsServer = previousRole;
        ModInformation.IsNavalDlcActive = previousNavalDlc;
    }
}
#endif
