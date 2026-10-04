#if DEBUG
using Autofac;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab.Sections;
using GameInterface.Services.UI.ServerInfo;
using Moq;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

/// <summary>Protects the DEBUG chat fixture a live run uses to send !motd and a chat line without keyboard input.</summary>
[Collection(ViewModelCollection.Name)]
public class ChatDebugCommandsTests
{
    private readonly Mock<INetwork> network = new();
    private readonly Mock<IServerInfoService> serverInfo = new();

    [Fact]
    public void CommandIsClientSideUnderTheUiPrefix()
    {
        var command = new ChatDebugCommands.ChatSubmitCoopCommand();

        Assert.Equal("coop.debug.ui.chat_submit", command.Prefix + "." + command.Name);
        Assert.Equal(CoopCommandSide.Client, command.Side);
        var text = Assert.Single(command.ExpectedArgs);
        Assert.Equal("text", text.Name);
        Assert.True(text.IsRequired);
    }

    // The same ActionSend as the Send button: !motd reopens the panel and nothing reaches the server.
    [Theory]
    [InlineData("!motd")]
    [InlineData("!MOTD")]
    [InlineData("  !MoTd  ")]
    public void Motd_ReopensThePanelAndSendsNothing(string text)
    {
        serverInfo.Setup(service => service.Reopen()).Returns(true);
        using var chat = Create();

        var result = ChatDebugCommands.ChatSubmitCoopCommand.Submit(chat, text);

        Assert.True(result.Succeeded);
        Assert.Equal("Sent: False\nChannel: Global chat\nInput: empty\nLast line: none", result.Output);
        serverInfo.Verify(service => service.Reopen(), Times.Once);
        network.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void MotdWithoutServerInfo_WritesTheLocalLineAndSendsNothing()
    {
        serverInfo.Setup(service => service.Reopen()).Returns(false);
        using var chat = Create();

        var result = ChatDebugCommands.ChatSubmitCoopCommand.Submit(chat, "!motd");

        Assert.True(result.Succeeded);
        Assert.Equal("Sent: False\nChannel: Global chat\nInput: empty\nLast line: [Chat] This server has no server info.", result.Output);
        network.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    // Anything but !motd alone goes to the server as a chat line and never opens the panel.
    [Theory]
    [InlineData("hello")]
    [InlineData("!motd extra")]
    [InlineData("see !motd")]
    public void OtherText_IsSentAsAChatLine(string text)
    {
        using var chat = Create();

        var result = ChatDebugCommands.ChatSubmitCoopCommand.Submit(chat, text);

        Assert.True(result.Succeeded);
        Assert.Equal("Sent: True\nChannel: Global chat\nInput: empty\nLast line: none", result.Output);
        network.Verify(value => value.SendAll(It.Is<IMessage>(message =>
            message is NetworkSendChatMessage && ((NetworkSendChatMessage)message).Channel == ChatChannel.Global &&
            ((NetworkSendChatMessage)message).Text == text)), Times.Once);
        serverInfo.Verify(service => service.Reopen(), Times.Never);
    }

    // No player can send with chat turned off, so the fixture does not either.
    [Fact]
    public void ChatTurnedOff_TypesAndSendsNothing()
    {
        var options = new CoopOptionsData();
        options.SetSection(ChatOptionsTabProvider.TabId, ChatSection.SectionId, new ChatSectionOptions { ShowChat = false });
        var optionsStore = new Mock<ICoopOptionsStore>();
        optionsStore.Setup(store => store.LoadOrDefault()).Returns(options);
        using var chat = Create(optionsStore);

        var result = ChatDebugCommands.ChatSubmitCoopCommand.Submit(chat, "!motd");

        Assert.False(result.Succeeded);
        Assert.Equal("chat_disabled", result.ErrorCode);
        serverInfo.Verify(service => service.Reopen(), Times.Never);
        network.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    // The command finds the chat through the Debug-only hook, so IChatService is the same in Debug and Release.
    [Fact]
    public void Command_TypesThroughTheRegisteredLiveTestHook()
    {
        using var chat = Create();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(chat).As<IChatLiveTestHook>().ExternallyOwned();
        using var container = builder.Build();
        var command = new ChatDebugCommands.ChatSubmitCoopCommand();

        CoopCommandResult result;
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            using (ContainerProvider.UseContainerThreadSafe(container))
                result = command.ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "hello" }));
        }
        finally
        {
            if (hadPreviousContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }

        Assert.True(result.Succeeded);
        Assert.StartsWith("Sent: True", result.Output);
        network.Verify(value => value.SendAll(It.Is<IMessage>(message =>
            message is NetworkSendChatMessage && ((NetworkSendChatMessage)message).Text == "hello")), Times.Once);
    }

    // Checked before the chat is looked up, so nothing is typed.
    [Fact]
    public void TextTheInputCannotHold_IsRefused()
    {
        var command = new ChatDebugCommands.ChatSubmitCoopCommand();
        var factory = new CoopCommandArgsFactory();

        var tooLong = command.ProcessCommand(factory.FromValues(new[] { new string('a', ChatMessageLimits.MaxMessageLength + 1) }));
        var blank = command.ProcessCommand(factory.FromValues(new[] { "   " }));
        var missing = command.ProcessCommand(factory.FromValues(Array.Empty<string>()));

        Assert.Equal((false, "text_too_long"), (tooLong.Succeeded, tooLong.ErrorCode));
        Assert.Equal((false, "invalid_arguments"), (blank.Succeeded, blank.ErrorCode));
        Assert.Equal((false, "invalid_arguments"), (missing.Succeeded, missing.ErrorCode));
    }

    private ChatService Create(Mock<ICoopOptionsStore>? optionsStore = null) => ChatServiceTests.CreateService(
        network: network,
        optionsStore: optionsStore,
        serverInfo: new Lazy<IServerInfoService>(() => serverInfo.Object));
}
#endif
