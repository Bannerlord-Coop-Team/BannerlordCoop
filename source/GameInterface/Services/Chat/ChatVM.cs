using GameInterface.Services.Chat.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Chat;

/// <summary>Bounded, session-only chat history and channel selection.</summary>
internal sealed class ChatVM : ViewModel
{
    private const string GlobalChannelId = "";
    private const int MaxHistoryPerChannel = 50;
    private const int VisibleHistoryLines = 12;
    internal const float DefaultChatBoxSizeX = 520f;
    internal const float DefaultChatBoxSizeY = 350f;
    internal const float MinChatBoxSizeX = 425f;
    internal const float MaxChatBoxSizeX = 650f;
    internal const float MinChatBoxSizeY = 170f;
    internal const float MaxChatBoxSizeY = 470f;

    private static readonly Color PlayerChatColor = Color.White;

    private readonly Action<NetworkSendChatMessage> send;
    private readonly Func<string> getLocalControllerId;
    private readonly Dictionary<string, ChatChannelVM> channelsById =
        new Dictionary<string, ChatChannelVM>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ChatLineVM>> histories =
        new Dictionary<string, List<ChatLineVM>>(StringComparer.Ordinal);

    private ChatChannelVM selectedChannel;
    private string writtenText = string.Empty;
    private bool isOpen;
    private bool playerChatEnabled = true;
    private int unreadMessageCount;
    private float chatBoxSizeX;
    private float chatBoxSizeY;

    public ChatVM(Action<NetworkSendChatMessage> send, Func<string> getLocalControllerId)
    {
        if (send == null) throw new ArgumentNullException(nameof(send));
        if (getLocalControllerId == null) throw new ArgumentNullException(nameof(getLocalControllerId));

        this.send = send;
        this.getLocalControllerId = getLocalControllerId;

        Channels = new MBBindingList<ChatChannelVM>();
        VisibleLines = new MBBindingList<ChatLineVM>();
        // Original fixed panel size. Don't seed from BannerlordConfig — that value is shared
        // with vanilla MP chat and is often much larger than this overlay's prior 520x350.
        chatBoxSizeX = DefaultChatBoxSizeX;
        chatBoxSizeY = DefaultChatBoxSizeY;
        var global = EnsureChannel(GlobalChannelId, "Global");
        SelectChannel(global);
    }

    public event Action OpenRequested;
    public event Action FeedScrolledToBottomRequested;

    [DataSourceProperty]
    public MBBindingList<ChatChannelVM> Channels { get; }

    [DataSourceProperty]
    public MBBindingList<ChatLineVM> VisibleLines { get; }

    [DataSourceProperty]
    public int MaxMessageLength => ChatMessageLimits.MaxMessageLength;

    [DataSourceProperty]
    public string ActiveChannelText => selectedChannel?.IsGlobal == false
        ? $"Direct message: {selectedChannel.Name.TrimEnd(' ', '*')}"
        : "Global chat";

    [DataSourceProperty]
    public bool IsMuteButtonVisible => selectedChannel?.IsGlobal == false;

    [DataSourceProperty]
    public string MuteButtonText => selectedChannel?.IsMuted == true ? "Unmute" : "Mute";

    [DataSourceProperty]
    public bool HasUnreadNotification => unreadMessageCount > 0;

    [DataSourceProperty]
    public string UnreadNotificationText => unreadMessageCount > 99
        ? "99+"
        : unreadMessageCount.ToString();

    [DataSourceProperty]
    public bool IsPlayerChatEnabled
    {
        get => playerChatEnabled;
        private set
        {
            if (playerChatEnabled == value) return;
            playerChatEnabled = value;
            OnPropertyChanged(nameof(IsPlayerChatEnabled));
            UpdateVisibleLines();
        }
    }

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
    public bool IsOpen
    {
        get => isOpen;
        private set
        {
            if (isOpen == value) return;

            isOpen = value;
            OnPropertyChanged(nameof(IsOpen));
            RefreshForceVisible();
            UpdateVisibleLines();
        }
    }

    [DataSourceProperty]
    public float ChatBoxSizeX
    {
        get => chatBoxSizeX;
        set
        {
            float clamped = ClampSizeX(value);
            if (chatBoxSizeX == clamped) return;

            chatBoxSizeX = clamped;
            OnPropertyChanged(nameof(ChatBoxSizeX));
        }
    }

    [DataSourceProperty]
    public float ChatBoxSizeY
    {
        get => chatBoxSizeY;
        set
        {
            float clamped = ClampSizeY(value);
            if (chatBoxSizeY == clamped) return;

            chatBoxSizeY = clamped;
            OnPropertyChanged(nameof(ChatBoxSizeY));
        }
    }

    public void ActionOpen()
    {
        if (!IsPlayerChatEnabled) return;
        OpenRequested?.Invoke();
    }

    public void ActionSend()
    {
        if (!IsPlayerChatEnabled) return;

        string text = WrittenText.Trim();
        if (text.Length == 0) return;

        var channel = selectedChannel?.IsGlobal == false ? ChatChannel.Direct : ChatChannel.Global;
        string recipientControllerId = channel == ChatChannel.Direct
            ? selectedChannel.ControllerId
            : string.Empty;

        send(new NetworkSendChatMessage(channel, recipientControllerId, text));
        WrittenText = string.Empty;
    }

    public void ActionToggleMute()
    {
        if (selectedChannel == null || selectedChannel.IsGlobal) return;

        selectedChannel.SetMuted(!selectedChannel.IsMuted);
        OnPropertyChanged(nameof(MuteButtonText));
        OnPropertyChanged(nameof(ActiveChannelText));
    }

    public void ExecuteSaveSizes()
    {
        try
        {
            BannerlordConfig.ChatBoxSizeX = ChatBoxSizeX;
            BannerlordConfig.ChatBoxSizeY = ChatBoxSizeY;
            BannerlordConfig.Save();
        }
        catch (TypeInitializationException)
        {
            // Unit tests construct ChatVM without the engine; skip persist.
        }
    }

    internal static float ClampSizeX(float value)
    {
        if (value <= 0f) return DefaultChatBoxSizeX;
        return MBMath.ClampFloat(value, MinChatBoxSizeX, MaxChatBoxSizeX);
    }

    internal static float ClampSizeY(float value)
    {
        if (value <= 0f) return DefaultChatBoxSizeY;
        return MBMath.ClampFloat(value, MinChatBoxSizeY, MaxChatBoxSizeY);
    }

    public void SetOpen(bool open)
    {
        if (open && !IsPlayerChatEnabled) return;

        IsOpen = open;
        if (!open) return;

        SetUnreadMessageCount(0);
    }

    public void SetPlayerChatEnabled(bool enabled)
    {
        if (!enabled && IsOpen)
            SetOpen(false);

        IsPlayerChatEnabled = enabled;
    }

    public void Tick(float dt)
    {
        foreach (var history in histories.Values)
        {
            for (int i = 0; i < history.Count; i++)
                history[i].HandleFading(dt);
        }
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

    public void SetParticipants(IEnumerable<(string ControllerId, string DisplayName)> participants)
    {
        if (participants == null) throw new ArgumentNullException(nameof(participants));

        var availableIds = new HashSet<string>(StringComparer.Ordinal);
        var availableParticipants = new List<(string ControllerId, string DisplayName)>();
        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.ControllerId) ||
                string.Equals(participant.ControllerId, getLocalControllerId(), StringComparison.Ordinal) ||
                !availableIds.Add(participant.ControllerId))
            {
                continue;
            }

            availableParticipants.Add(participant);
        }

        if (selectedChannel?.IsGlobal == false && !availableIds.Contains(selectedChannel.ControllerId))
            SelectChannel(channelsById[GlobalChannelId]);

        var directChannels = new List<ChatChannelVM>();
        foreach (var channel in Channels)
        {
            if (!channel.IsGlobal) directChannels.Add(channel);
        }

        foreach (var channel in directChannels)
            Channels.Remove(channel);

        foreach (var participant in availableParticipants)
            EnsureChannel(participant.ControllerId, participant.DisplayName);
    }

    public void ReceiveEvent(string text, Color color, string category)
    {
        if (string.IsNullOrEmpty(text)) return;
        _ = category;

        AddLine(GlobalChannelId, new ChatLineVM(text, color, isPlayerChat: false), notify: false);
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

        AddLine(channelId, new ChatLineVM(line, PlayerChatColor, isPlayerChat: true), notify);
    }

    private ChatChannelVM EnsureChannel(string controllerId, string displayName)
    {
        controllerId ??= GlobalChannelId;
        if (channelsById.TryGetValue(controllerId, out var existing))
        {
            existing.UpdateDisplayName(displayName);
            if (!Channels.Contains(existing)) Channels.Add(existing);
            return existing;
        }

        var channel = new ChatChannelVM(controllerId, displayName, SelectChannel);
        channelsById.Add(controllerId, channel);
        histories.Add(controllerId, new List<ChatLineVM>());
        Channels.Add(channel);
        return channel;
    }

    private void SelectChannel(ChatChannelVM channel)
    {
        if (channel == null || ReferenceEquals(selectedChannel, channel))
        {
            channel?.SetSelected(true);
            UpdateVisibleLines();
            return;
        }

        selectedChannel?.SetSelected(false);
        selectedChannel = channel;
        selectedChannel.SetSelected(true);
        OnPropertyChanged(nameof(ActiveChannelText));
        OnPropertyChanged(nameof(IsMuteButtonVisible));
        OnPropertyChanged(nameof(MuteButtonText));
        UpdateVisibleLines();
    }

    private void AddLine(string channelId, ChatLineVM line, bool notify)
    {
        if (!histories.TryGetValue(channelId, out var history))
            history = histories[EnsureChannel(channelId, channelId).ControllerId];

        line.ToggleForceVisible(IsOpen);
        history.Add(line);
        ChatLineVM trimmed = null;
        if (history.Count > MaxHistoryPerChannel)
        {
            trimmed = history[0];
            history.RemoveAt(0);
        }

        bool viewingThisChannel = IsOpen &&
            string.Equals(selectedChannel?.ControllerId, channelId, StringComparison.Ordinal);
        bool passiveGlobal = !IsOpen && string.Equals(channelId, GlobalChannelId, StringComparison.Ordinal);
        if (viewingThisChannel || passiveGlobal)
            AppendVisibleLine(line, trimmed);
        else if (channelsById.TryGetValue(channelId, out var channel) && line.IsPlayerChat)
            channel.MarkUnread();

        if (notify && !IsOpen && line.IsPlayerChat && IsPlayerChatEnabled)
            SetUnreadMessageCount(Math.Min(unreadMessageCount + 1, 999));
    }

    private void AppendVisibleLine(ChatLineVM line, ChatLineVM trimmed)
    {
        if (line.IsPlayerChat && !IsPlayerChatEnabled) return;

        if (trimmed != null)
            VisibleLines.Remove(trimmed);
        if (!IsOpen && VisibleLines.Count >= VisibleHistoryLines)
            VisibleLines.RemoveAt(0);

        VisibleLines.Add(line);
        if (IsOpen)
            FeedScrolledToBottomRequested?.Invoke();
    }

    private void UpdateVisibleLines()
    {
        string channelId = IsOpen
            ? selectedChannel?.ControllerId ?? GlobalChannelId
            : GlobalChannelId;

        VisibleLines.Clear();
        if (!histories.TryGetValue(channelId, out var history))
            return;

        // Closed: recent fading lines only. Open: full channel history so the scrollbar can move.
        int firstLine = IsOpen ? 0 : Math.Max(0, history.Count - VisibleHistoryLines);
        for (int i = firstLine; i < history.Count; i++)
        {
            var historyLine = history[i];
            if (historyLine.IsPlayerChat && !IsPlayerChatEnabled) continue;
            VisibleLines.Add(historyLine);
        }

        if (IsOpen)
            FeedScrolledToBottomRequested?.Invoke();
    }

    private void RefreshForceVisible()
    {
        foreach (var history in histories.Values)
        {
            for (int i = 0; i < history.Count; i++)
                history[i].ToggleForceVisible(IsOpen);
        }
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