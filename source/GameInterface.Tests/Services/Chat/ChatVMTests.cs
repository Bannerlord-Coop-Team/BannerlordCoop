using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatVMTests
{
    [Fact]
    public void ActionOpen_RaisesRequestForOverlay()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        bool openRequested = false;
        vm.OpenRequested += () => openRequested = true;

        vm.ActionOpen();

        Assert.True(openRequested);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void ActionSend_DefaultChannel_SendsTrimmedGlobalRequest()
    {
        var sent = new List<NetworkSendChatMessage>();
        var vm = new ChatVM(sent.Add, () => "local");
        vm.SetOpen(true);
        vm.WrittenText = "  hello world  ";

        vm.ActionSend();

        var request = Assert.Single(sent);
        Assert.Equal(ChatChannel.Global, request.Channel);
        Assert.Equal(string.Empty, request.RecipientControllerId);
        Assert.Equal("hello world", request.Text);
        Assert.Equal(string.Empty, vm.WrittenText);
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public void ActionSend_SelectedPlayer_SendsDirectRequestToControllerId()
    {
        var sent = new List<NetworkSendChatMessage>();
        var vm = new ChatVM(sent.Add, () => "local");
        vm.AddParticipant("other-controller", "Other Hero");
        vm.Channels.Single(channel => channel.ControllerId == "other-controller").ExecuteSelection();
        vm.WrittenText = "secret";

        vm.ActionSend();

        var request = Assert.Single(sent);
        Assert.Equal(ChatChannel.Direct, request.Channel);
        Assert.Equal("other-controller", request.RecipientControllerId);
        Assert.Equal("secret", request.Text);
    }

    [Fact]
    public void Receive_DirectMessage_AddsRibbonNotificationWithoutOpeningChat()
    {
        var vm = new ChatVM(_ => { }, () => "local");

        vm.Receive(new NetworkChatMessage(
            ChatChannel.Direct,
            "other-controller",
            "Other Hero",
            "local",
            "Local Hero",
            "meet me in Pravend"));

        var global = vm.Channels.Single(channel => channel.IsGlobal);
        var direct = vm.Channels.Single(channel => channel.ControllerId == "other-controller");
        Assert.True(global.IsSelected);
        Assert.True(direct.HasUnreadMessages);
        Assert.False(vm.IsOpen);
        Assert.True(vm.IsRibbonVisible);
        Assert.True(vm.HasUnreadNotification);
        Assert.Equal("1", vm.UnreadNotificationText);
        Assert.Equal(string.Empty, vm.TranscriptText);

        vm.SetOpen(true);

        Assert.False(vm.IsRibbonVisible);
        Assert.False(vm.HasUnreadNotification);
        Assert.Equal(string.Empty, vm.TranscriptText);

        direct.ExecuteSelection();

        Assert.False(direct.HasUnreadMessages);
        Assert.Contains("[From Other Hero] Other Hero: meet me in Pravend", vm.TranscriptText);
    }

    [Fact]
    public void Receive_OwnGlobalEcho_DoesNotAddRibbonNotification()
    {
        var vm = new ChatVM(_ => { }, () => "local");

        vm.Receive(new NetworkChatMessage(
            ChatChannel.Global,
            "local",
            "Local Hero",
            string.Empty,
            string.Empty,
            "hello everyone"));

        Assert.False(vm.HasUnreadNotification);
        Assert.Equal("0", vm.UnreadNotificationText);
    }

    [Fact]
    public void ActionToggleMute_SelectedPlayer_UpdatesChannelAndButtonState()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        vm.AddParticipant("other-controller", "Other Hero");
        var direct = vm.Channels.Single(channel => channel.ControllerId == "other-controller");

        Assert.False(vm.IsMuteButtonVisible);

        direct.ExecuteSelection();

        Assert.True(vm.IsMuteButtonVisible);
        Assert.Equal("Mute", vm.MuteButtonText);

        vm.ActionToggleMute();

        Assert.True(direct.IsMuted);
        Assert.Equal("Unmute", vm.MuteButtonText);
        Assert.Equal("Other Hero (Muted)", direct.Name);

        vm.ActionToggleMute();

        Assert.False(direct.IsMuted);
        Assert.Equal("Mute", vm.MuteButtonText);
        Assert.Equal("Other Hero", direct.Name);
    }

    [Fact]
    public void Receive_FromMutedPlayer_SuppressesGlobalAndDirectMessages()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        vm.AddParticipant("muted-controller", "Muted Hero");
        var global = vm.Channels.Single(channel => channel.IsGlobal);
        var muted = vm.Channels.Single(channel => channel.ControllerId == "muted-controller");
        muted.ExecuteSelection();
        vm.ActionToggleMute();
        global.ExecuteSelection();

        vm.Receive(new NetworkChatMessage(
            ChatChannel.Global,
            "muted-controller",
            "Muted Hero",
            string.Empty,
            string.Empty,
            "global noise"));
        vm.Receive(new NetworkChatMessage(
            ChatChannel.Direct,
            "muted-controller",
            "Muted Hero",
            "local",
            "Local Hero",
            "direct noise"));

        Assert.Equal(string.Empty, vm.TranscriptText);
        Assert.False(vm.HasUnreadNotification);
        Assert.False(muted.HasUnreadMessages);

        muted.ExecuteSelection();
        Assert.Equal(string.Empty, vm.TranscriptText);

        vm.ActionToggleMute();
        global.ExecuteSelection();
        vm.Receive(new NetworkChatMessage(
            ChatChannel.Global,
            "muted-controller",
            "Muted Hero",
            string.Empty,
            string.Empty,
            "audible again"));

        Assert.Contains("[Global] Muted Hero: audible again", vm.TranscriptText);
    }
}
