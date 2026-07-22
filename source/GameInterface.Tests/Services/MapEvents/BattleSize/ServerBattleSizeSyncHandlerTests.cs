using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.Messages;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.BattleSize;

[Collection(ModInformationRoleCollection.Name)]
public class ServerBattleSizeSyncHandlerTests
{
    [Fact]
    public void ServerSelection_AppliesAndRequestsConsolidatedOptionsUpdate()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var messageBroker = new MessageBroker();
            var optionsStore = new Mock<ICoopOptionsStore>();
            var battleSizeProvider = new ServerBattleSizeProvider();
            optionsStore.Setup(m => m.LoadOrDefault()).Returns(new CoopOptionsData());

            int updates = 0;
            messageBroker.Subscribe<UpdateOtherOptions>(_ => updates++);

            using var handler = new ServerBattleSizeSyncHandler(
                messageBroker,
                optionsStore.Object,
                battleSizeProvider);

            messageBroker.Publish(this, new ServerBattleSizeSelected(650));

            Assert.Equal(650, battleSizeProvider.BattleSize);
            Assert.Equal(1, updates);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ClientSelection_DoesNotApplyOrRequestUpdate()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var messageBroker = new MessageBroker();
            var optionsStore = new Mock<ICoopOptionsStore>();
            var battleSizeProvider = new ServerBattleSizeProvider();
            int updates = 0;
            messageBroker.Subscribe<UpdateOtherOptions>(_ => updates++);

            using var handler = new ServerBattleSizeSyncHandler(
                messageBroker,
                optionsStore.Object,
                battleSizeProvider);

            messageBroker.Publish(this, new ServerBattleSizeSelected(300));

            Assert.Equal(ServerBattleSizeProvider.DefaultBattleSize, battleSizeProvider.BattleSize);
            Assert.Equal(0, updates);
            optionsStore.Verify(m => m.LoadOrDefault(), Times.Never);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
