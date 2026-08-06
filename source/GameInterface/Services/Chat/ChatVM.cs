using GameInterface.Services.Chat.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace GameInterface.Services.Chat;

/// <summary>Bounded, session-only chat history and channel selection.</summary>
internal sealed class ChatVM : ViewModel
{
    private const string GlobalChannelId = "";
    private const int MaxHistoryPerChannel = 50;
    private const int VisibleHistoryLines = 12;

    private readonly Action<NetworkSendChatMessage> send;
    private readonly Func<string> getLocalControllerId;
    private readonly Dictionary<string, ChatChannelVM> channelsById =
        new Dictionary<string, ChatChannelVM>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> histories =
        new Dictionary<string, List<string>>(StringComparer.Ordinal);

    private ChatChannelVM selectedChannel;
    private string writtenText = string.Empty;
    private string transcriptText = string.Empty;
    private bool isOpen;
    private int unreadMessageCount;

    public ChatVM(Action<NetworkSendChatMessage> send, Func<string> getLocalControllerId)
    {
        this.send = send ?? throw new ArgumentNullException(nameof(send));
        this.getLocalControllerId = getLocalControllerId ?? throw new ArgumentNullException(nameof(getLocalControllerId));

        Channels = new MBBindingList<ChatChannelVM>();
        var global = EnsureChannel(GlobalChannelId, "Global");
        SelectChannel(global);
    }

    public event Action CloseRequested;
    public event Action OpenRequested;

    [DataSourceProperty]
    public MBBindingList<ChatChannelVM> Channels { get; }

    [DataSourceProperty]
    public int MaxMessageLength => ChatMessageLimits.MaxMessageLength;

    [DataSourceProperty]
    public string ActiveChannelText => selectedChannel?.IsGlobal == false
        ? $"Direct message: {selectedChannel.Name.TrimEnd(' ', '*')}"
        : "Global chat";

    [DataSourceProperty]
    public string InputHintText => "Enter or click Send to send    Esc: close";

    [DataSourceProperty]
    public string SendButtonText => "Send";

    [DataSourceProperty]
    public bool IsMuteButtonVisible => selectedChannel?.IsGlobal == false;

    [DataSourceProperty]
    public string MuteButtonText => selectedChannel?.IsMuted == true ? "Unmute" : "Mute";

    [DataSourceProperty]
    public string RibbonText => "Chat";

    [DataSourceProperty]
    public bool IsRibbonVisible => !IsOpen;

    [DataSourceProperty]
    public bool HasUnreadNotification => unreadMessageCount > 0;

    [DataSourceProperty]
    public string UnreadNotificationText => unreadMessageCount > 99
        ? "99+"
        : unreadMessageCount.ToString();

    [DataSourceProperty]
    public string WrittenText
    {
        get => writtenText;
        set
        {
            value ??= string.Empty;
            if (writtenText == value) return;

            writtenText = value;
            OnPropertyChanged(nameof(WrittenText));
        }
    }

    [DataSourceProperty]
    public string TranscriptText
    {
        get => transcriptText;
        private set
        {
            if (transcriptText == value) return;

            transcriptText = value;
            OnPropertyChanged(nameof(TranscriptText));
        }
    }

    [DataSourceProperty]
    public bool IsOpen
    {
        get => isOpen;
        private set
        {
            if (isOpen == value) return;

            isOpen = value;
            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(IsRibbonVisible));
        }
    }

    public void ActionOpen()
    {
        OpenRequested?.Invoke();
    }

    public void ActionSend()
    {
        string text = WrittenText.Trim();
        if (text.Length == 0) return;

        var channel = selectedChannel?.IsGlobal == false ? ChatChannel.Direct : ChatChannel.Global;
        string recipientControllerId = channel == ChatChannel.Direct
            ? selectedChannel.ControllerId
            : string.Empty;

        send(new NetworkSendChatMessage(channel, recipientControllerId, text));
        WrittenText = string.Empty;
    }

    public void ActionClose()
    {
        CloseRequested?.Invoke();
    }

    public void ActionToggleMute()
    {
        if (selectedChannel == null || selectedChannel.IsGlobal) return;

        selectedChannel.SetMuted(!selectedChannel.IsMuted);
        OnPropertyChanged(nameof(MuteButtonText));
        OnPropertyChanged(nameof(ActiveChannelText));
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (!open) return;

        SetUnreadMessageCount(0);
        UpdateTranscript();
    }

    public void AddParticipant(string controllerId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(controllerId) ||
            string.Equals(controllerId, getLocalControllerId(), StringComparison.Ordinal))
        {
            return;
        }

        EnsureChannel(controllerId, displayName);
    }

    public void Receive(NetworkChatMessage message)
    {
        if (message.Channel != ChatChannel.System &&
            !string.Equals(message.SenderControllerId, getLocalControllerId(), StringComparison.Ordinal) &&
            channelsById.TryGetValue(message.SenderControllerId ?? string.Empty, out var senderChannel) &&
            senderChannel.IsMuted)
        {
            return;
        }

        string channelId;
        string line;
        bool notify;

        switch (message.Channel)
        {
            case ChatChannel.Global:
                channelId = GlobalChannelId;
                line = $"[Global] {DisplayName(message.SenderName, message.SenderControllerId)}: {message.Text}";
                notify = !string.Equals(
                    message.SenderControllerId,
                    getLocalControllerId(),
                    StringComparison.Ordinal);
                AddParticipant(message.SenderControllerId, message.SenderName);
                break;
            case ChatChannel.Direct:
                bool sentByLocalPlayer = string.Equals(
                    message.SenderControllerId,
                    getLocalControllerId(),
                    StringComparison.Ordinal);
                channelId = sentByLocalPlayer ? message.RecipientControllerId : message.SenderControllerId;
                string otherName = sentByLocalPlayer
                    ? DisplayName(message.RecipientName, message.RecipientControllerId)
                    : DisplayName(message.SenderName, message.SenderControllerId);
                EnsureChannel(channelId, otherName);
                line = sentByLocalPlayer
                    ? $"[To {otherName}] You: {message.Text}"
                    : $"[From {otherName}] {otherName}: {message.Text}";
                notify = !sentByLocalPlayer;
                break;
            case ChatChannel.System:
                channelId = string.IsNullOrEmpty(message.RecipientControllerId)
                    ? selectedChannel?.ControllerId ?? GlobalChannelId
                    : message.RecipientControllerId;
                if (channelId.Length > 0)
                    EnsureChannel(channelId, DisplayName(message.RecipientName, channelId));
                line = $"[Chat] {message.Text}";
                notify = true;
                break;
            default:
                return;
        }

        AddLine(channelId, line, notify);
    }

    private ChatChannelVM EnsureChannel(string controllerId, string displayName)
    {
        controllerId ??= GlobalChannelId;
        if (channelsById.TryGetValue(controllerId, out var existing))
        {
            existing.UpdateDisplayName(displayName);
            return existing;
        }

        var channel = new ChatChannelVM(controllerId, displayName, SelectChannel);
        channelsById.Add(controllerId, channel);
        histories.Add(controllerId, new List<string>());
        Channels.Add(channel);
        return channel;
    }

    private void SelectChannel(ChatChannelVM channel)
    {
        if (channel == null || ReferenceEquals(selectedChannel, channel))
        {
            channel?.SetSelected(true);
            UpdateTranscript();
            return;
        }

        selectedChannel?.SetSelected(false);
        selectedChannel = channel;
        selectedChannel.SetSelected(true);
        OnPropertyChanged(nameof(ActiveChannelText));
        OnPropertyChanged(nameof(IsMuteButtonVisible));
        OnPropertyChanged(nameof(MuteButtonText));
        UpdateTranscript();
    }

    private void AddLine(string channelId, string line, bool notify)
    {
        if (!histories.TryGetValue(channelId, out var history))
            history = histories[EnsureChannel(channelId, channelId).ControllerId];

        history.Add(line ?? string.Empty);
        if (history.Count > MaxHistoryPerChannel)
            history.RemoveAt(0);

        if (string.Equals(selectedChannel?.ControllerId, channelId, StringComparison.Ordinal))
            UpdateTranscript();
        else if (channelsById.TryGetValue(channelId, out var channel))
            channel.MarkUnread();

        if (notify && !IsOpen)
            SetUnreadMessageCount(Math.Min(unreadMessageCount + 1, 999));
    }

    private void UpdateTranscript()
    {
        if (selectedChannel == null || !histories.TryGetValue(selectedChannel.ControllerId, out var history))
        {
            TranscriptText = string.Empty;
            return;
        }

        int firstLine = Math.Max(0, history.Count - VisibleHistoryLines);
        TranscriptText = string.Join("\n", history.GetRange(firstLine, history.Count - firstLine));
    }

    private void SetUnreadMessageCount(int count)
    {
        if (unreadMessageCount == count) return;

        unreadMessageCount = count;
        OnPropertyChanged(nameof(HasUnreadNotification));
        OnPropertyChanged(nameof(UnreadNotificationText));
    }

    private static string DisplayName(string name, string controllerId)
    {
        return string.IsNullOrWhiteSpace(name) ? controllerId ?? "Player" : name;
    }
}
