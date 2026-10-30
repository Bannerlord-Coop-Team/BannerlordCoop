using Common.Messaging;
using Common.Network;
using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;
using GameInterface.Services.UI.Messages;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

[Collection(ViewModelCollection.Name)]
public class ChatServiceTests
{
    [Fact]
    public void RequestParticipants_AsksServerForLiveMembership()
    {
        var network = new Mock<INetwork>();
        using var service = CreateService(network: network);

        service.RequestParticipants();

        network.Verify(value => value.SendAll(It.IsAny<NetworkRequestChatParticipants>()), Times.Once);
    }

    [Fact]
    public void ReceiveParticipants_ResolvesOnlyServerReportedPlayers()
    {
        var connected = Player("connected");
        var offline = Player("offline");
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { connected, offline });
        playerManager.Setup(manager => manager.TryGetPlayer("connected", out connected)).Returns(true);
        var playerNameResolver = new Mock<IChatPlayerNameResolver>();
        playerNameResolver.Setup(resolver => resolver.Resolve(connected)).Returns("Connected Hero");
        using var service = CreateService(
            playerManager: playerManager,
            playerNameResolver: playerNameResolver);

        service.ReceiveParticipants(new NetworkChatParticipants(new[] { "connected" }));

        playerNameResolver.Verify(resolver => resolver.Resolve(connected), Times.Once);
        playerNameResolver.Verify(resolver => resolver.Resolve(offline), Times.Never);
        playerManager.VerifyGet(manager => manager.Players, Times.Never);
    }

    [Theory]
    [InlineData(ChatChannel.Clan)]
    [InlineData(ChatChannel.Kingdom)]
    public void Receive_GroupMessage_RefreshesFactionMembership(ChatChannel channel)
    {
        var localPlayer = Player("local");
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(manager => manager.TryGetPlayer("local", out localPlayer)).Returns(true);
        var factionMembership = new Mock<IChatFactionMembership>();
        using var service = CreateService(
            playerManager: playerManager,
            factionMembership: factionMembership);

        service.Receive(new NetworkChatMessage(
            channel,
            "other-controller",
            "Other Hero",
            string.Empty,
            string.Empty,
            "group message"));

        factionMembership.Verify(membership => membership.GetClan(localPlayer), Times.Once);
        factionMembership.Verify(membership => membership.GetKingdom(localPlayer), Times.Once);
    }

    [Fact]
    public void VisibilityOption_LoadsAndAppliesWithoutRecreatingService()
    {
        var options = new CoopOptionsData();
        options.SetSection(
            ChatOptionsTabProvider.TabId,
            ChatSection.SectionId,
            new ChatSectionOptions { ShowChat = false });
        var optionsStore = new Mock<ICoopOptionsStore>();
        optionsStore.Setup(store => store.LoadOrDefault()).Returns(options);
        using var messageBroker = new MessageBroker();
        using var service = CreateService(optionsStore: optionsStore, messageBroker: messageBroker);

        Assert.False(service.IsPlayerChatEnabled);

        messageBroker.Publish(this, new ChatVisibilitySelected(true));

        Assert.True(service.IsPlayerChatEnabled);
    }

    /// <summary>Verifies dispose stops the event log and survives the overlay's vanilla restore with no vanilla chat log view present.</summary>
    /// <remarks>Only the null <c>GauntletChatLogView.Current</c> path is reachable here; the non-null restore needs the game runtime.</remarks>
    [Fact]
    public void Dispose_StopsEventLog()
    {
        var eventLog = new Mock<IChatEventLog>();
        var service = CreateService(eventLog: eventLog);

        service.Dispose();

        eventLog.Verify(value => value.Dispose(), Times.Once);
    }

    private static ChatService CreateService(
        Mock<INetwork>? network = null,
        Mock<IPlayerManager>? playerManager = null,
        Mock<IChatPlayerNameResolver>? playerNameResolver = null,
        Mock<IChatFactionMembership>? factionMembership = null,
        Mock<ICoopOptionsStore>? optionsStore = null,
        IMessageBroker? messageBroker = null,
        Mock<IChatEventLog>? eventLog = null)
    {
        network ??= new Mock<INetwork>();
        playerManager ??= new Mock<IPlayerManager>();
        playerNameResolver ??= new Mock<IChatPlayerNameResolver>();
        factionMembership ??= new Mock<IChatFactionMembership>();
        if (optionsStore == null)
        {
            optionsStore = new Mock<ICoopOptionsStore>();
            optionsStore.Setup(store => store.LoadOrDefault()).Returns(new CoopOptionsData());
        }
        messageBroker ??= new MessageBroker();
        eventLog ??= new Mock<IChatEventLog>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("local");

        return new ChatService(
            network.Object,
            playerManager.Object,
            playerNameResolver.Object,
            controllerIdProvider.Object,
            factionMembership.Object,
            optionsStore.Object,
            messageBroker,
            eventLog.Object,
            new PlayerKillFeedColorService());
    }

    private static Player Player(string controllerId)
    {
        return new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
