using Common.Tests.Utils;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Save.Handlers;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

/// <summary>
/// Verifies co-op session initialization when campaign saves have no sidecar data.
/// </summary>
public class SaveGameHandlerTests
{
    [Fact]
    public void GameLoaded_WithoutSidecar_InitializesEmptyCoopSession()
    {
        var messageBroker = new TestMessageBroker();
        var saveManager = new CoopSaveManager();
        var sessionProvider = new CoopSessionProvider();
        var playerManager = new Mock<IPlayerManager>();
        string saveName = $"ConvertedVanilla_{Guid.NewGuid():N}";

        using var handler = new SaveGameHandler(
            messageBroker,
            saveManager,
            sessionProvider,
            playerManager.Object);

        messageBroker.Publish(this, new GameLoaded(saveName));

        Assert.Equal(saveName, sessionProvider.CoopSession.UniqueGameId);
        Assert.Empty(sessionProvider.CoopSession.Players);
        Assert.NotNull(sessionProvider.CoopSession.CraftingPlayerData);
        Assert.NotNull(sessionProvider.CoopSession.WorkshopPlayerData);
        Assert.NotNull(sessionProvider.CoopSession.CaravansPlayerData);
    }
}
