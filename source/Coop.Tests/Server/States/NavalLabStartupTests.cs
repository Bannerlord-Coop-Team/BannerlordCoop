#if DEBUG
using Common;
using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.States;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Server.States;

[Collection(nameof(ModInformationRoleCollection))]
public sealed class NavalLabStartupTests : IDisposable
{
    private readonly string previous = ModInformation.NavalLabCapability;
    private readonly bool previousRole = ModInformation.IsServer;

    public NavalLabStartupTests()
    {
        typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, null);
        ModInformation.IsServer = true;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialServerState_StartsFreshOnlyWithExplicitLabOptIn(bool optedIn)
    {
        if (optedIn) Enable();
        var game = new Mock<IGameStateInterface>();
        using var broker = new MessageBroker();
        using var state = new InitialServerState(Mock.Of<IServerLogic>(), broker, Mock.Of<IRegistryManager>(),
            Mock.Of<IMapEventLoadCleaner>(), Mock.Of<IModuleInfoProvider>(), Mock.Of<IModuleValidator>(),
            game.Object, Mock.Of<ILoadingInterface>());
        state.Start();
        game.Verify(value => value.StartNewGame(), optedIn ? Times.Once() : Times.Never());
        game.Verify(value => value.LoadGame("MP"), optedIn ? Times.Never() : Times.Once());
        game.VerifyNoOtherCalls();
    }

    [Fact]
    public void OptedInSessionSidecar_NeverWritesEvenOnTransferOrRetry()
    {
        Enable();
        var manager = new CoopSaveManager();
        string directory = Path.Combine(Path.GetTempPath(), "naval-save-regression-" + Guid.NewGuid());
        typeof(CoopSaveManager).GetField("<DefaultPath>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(manager, directory + Path.DirectorySeparatorChar);
        foreach (var name in new[] { "MP", "default_new_game", "auto_save_1", "TransferSave", "TransferSave" })
            manager.SaveCoopSession(name, CoopSession.Empty);
        Assert.False(Directory.Exists(directory));
    }

    private static void Enable() => ModInformation.ConfigureNavalLab(
        "new-campaign:571cda18-4f3b-4787-9ac0-5997f17f083e", "startup-regression", true);

    public void Dispose()
    {
        typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, previous);
        ModInformation.IsServer = previousRole;
    }
}
#endif
