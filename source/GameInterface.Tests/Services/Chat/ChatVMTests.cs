using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
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
    public void ActionOpen_WhenPlayerChatDisabled_DoesNothing()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        bool openRequested = false;
        vm.OpenRequested += () => openRequested = true;
        vm.SetPlayerChatEnabled(false);

        vm.ActionOpen();

        Assert.False(openRequested);
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
    public void Receive_DirectMessage_AddsUnreadNotificationWithoutOpeningChat()
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
        Assert.True(vm.HasUnreadNotification);
        Assert.Equal("1", vm.UnreadNotificationText);
        Assert.Empty(vm.VisibleLines);

        vm.SetOpen(true);

        Assert.False(vm.HasUnreadNotification);
        Assert.Empty(vm.VisibleLines);

        direct.ExecuteSelection();

        Assert.False(direct.HasUnreadMessages);
        Assert.Contains(vm.VisibleLines, line => line.Text.Contains("[From Other Hero] Other Hero: meet me in Pravend"));
    }

    [Fact]
    public void Receive_OwnGlobalEcho_DoesNotAddUnreadNotification()
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
        Assert.Contains(vm.VisibleLines, line => line.Text.Contains("[Global] Local Hero: hello everyone"));
    }

    [Fact]
    public void SetOpen_ShowsFullChannelHistoryForScrolling()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        for (int i = 0; i < 20; i++)
            vm.ReceiveEvent($"event {i}", Color.White, ChatEventLog.DefaultCategory);

        Assert.Equal(12, vm.VisibleLines.Count);

        vm.SetOpen(true);

        Assert.Equal(20, vm.VisibleLines.Count);
        Assert.Equal("event 0", vm.VisibleLines[0].Text);
        Assert.Equal("event 19", vm.VisibleLines[19].Text);
    }

    [Fact]
    public void ReceiveEvent_AppendsToGlobalAndShowsWhileClosed()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        vm.AddParticipant("other-controller", "Other Hero");
        vm.Channels.Single(channel => channel.ControllerId == "other-controller").ExecuteSelection();

        vm.ReceiveEvent("You received 2000 denars.", Color.White, ChatEventLog.DefaultCategory);

        Assert.False(vm.IsOpen);
        Assert.Contains(vm.VisibleLines, line => line.Text == "You received 2000 denars." && !line.IsPlayerChat);

        vm.SetOpen(true);
        Assert.DoesNotContain(vm.VisibleLines, line => line.Text == "You received 2000 denars.");

        vm.Channels.Single(channel => channel.IsGlobal).ExecuteSelection();
        Assert.Contains(vm.VisibleLines, line => line.Text == "You received 2000 denars.");
    }

    [Fact]
    public void ReceiveEvent_WhenPlayerChatDisabled_StillShowsEvents()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        vm.SetPlayerChatEnabled(false);
        vm.Receive(new NetworkChatMessage(
            ChatChannel.Global,
            "other",
            "Other Hero",
            string.Empty,
            string.Empty,
            "hidden chat"));
        vm.ReceiveEvent("Settlement captured.", Color.White, ChatEventLog.DefaultCategory);

        Assert.DoesNotContain(vm.VisibleLines, line => line.IsPlayerChat);
        Assert.Contains(vm.VisibleLines, line => line.Text == "Settlement captured.");
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

        Assert.Empty(vm.VisibleLines);
        Assert.False(vm.HasUnreadNotification);
        Assert.False(muted.HasUnreadMessages);

        muted.ExecuteSelection();
        Assert.Empty(vm.VisibleLines);

        vm.ActionToggleMute();
        global.ExecuteSelection();
        vm.Receive(new NetworkChatMessage(
            ChatChannel.Global,
            "muted-controller",
            "Muted Hero",
            string.Empty,
            string.Empty,
            "audible again"));

        Assert.Contains(vm.VisibleLines, line => line.Text.Contains("[Global] Muted Hero: audible again"));
    }

    [Fact]
    public void SetParticipants_ReplacesOfflineChannelsAndSelectsGlobalFallback()
    {
        var vm = new ChatVM(_ => { }, () => "local");
        vm.SetParticipants(new[]
        {
            (ControllerId: "offline", DisplayName: "Offline Hero"),
            (ControllerId: "online", DisplayName: "Online Hero"),
        });
        vm.Channels.Single(channel => channel.ControllerId == "offline").ExecuteSelection();

        vm.SetParticipants(new[]
        {
            (ControllerId: "online", DisplayName: "Online Hero"),
        });

        Assert.DoesNotContain(vm.Channels, channel => channel.ControllerId == "offline");
        Assert.Contains(vm.Channels, channel => channel.ControllerId == "online");
        Assert.True(vm.Channels.Single(channel => channel.IsGlobal).IsSelected);
    }
}