using GameInterface.Services.Chat.Messages;
using GameInterface.Services.UI;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Chat;

/// <summary>Bounded, session-only chat history and channel selection.</summary>
internal sealed class ChatVM : ViewModel
{
    internal const string AllChannelId = "__all__";
    internal const string EventsChannelId = "__events__";
    internal const string GlobalChannelId = "__global__";
    private const int MaxHistoryPerChannel = 50;
    private const int VisibleHistoryLines = 12;
    internal const float DefaultChatBoxSizeX = 520f;
    internal const float DefaultChatBoxSizeY = 350f;
    internal const float MinChatBoxSizeX = 425f;
    internal const float MaxChatBoxSizeX = 650f;
    internal const float MinChatBoxSizeY = 170f;
    internal const float MaxChatBoxSizeY = 470f;

    private readonly Action<NetworkSendChatMessage> send;
    private readonly Func<string> getLocalControllerId;
    private readonly Func<string, Color> getPlayerColor;
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

    public ChatVM(
        Action<NetworkSendChatMessage> send,
        Func<string> getLocalControllerId,
        Func<string, Color> getPlayerColor = null)
    {
        if (send == null) throw new ArgumentNullException(nameof(send));
        if (getLocalControllerId == null) throw new ArgumentNullException(nameof(getLocalControllerId));

        this.send = send;
        this.getLocalControllerId = getLocalControllerId;
        // Same colors as nameplates
        this.getPlayerColor = getPlayerColor ?? PlayerColorAssigner.GetColor;

        Channels = new MBBindingList<ChatChannelVM>();
        VisibleLines = new MBBindingList<ChatLineVM>();
        // Don't seed from BannerlordConfig
        //  it is shared with vanilla MP chat and is larger.
        chatBoxSizeX = DefaultChatBoxSizeX;
        chatBoxSizeY = DefaultChatBoxSizeY;

        EnsureFixedChannel(AllChannelId, "All", ChatChannelKind.All);
        EnsureFixedChannel(EventsChannelId, "Events", ChatChannelKind.Events);
        EnsureFixedChannel(GlobalChannelId, "Global", ChatChannelKind.Global);
        SelectChannel(channelsById[AllChannelId]);
    }

    public event Action FeedScrolledToBottomRequested;

    [DataSourceProperty]
    public MBBindingList<ChatChannelVM> Channels { get; }

    [DataSourceProperty]
    public MBBindingList<ChatLineVM> VisibleLines { get; }

    [DataSourceProperty]
    public int MaxMessageLength => ChatMessageLimits.MaxMessageLength;

    [DataSourceProperty]
    public string ActiveChannelText
    {
        get
        {
            if (selectedChannel == null) return "All";
            if (selectedChannel.IsDirect)
                return $"Direct message: {selectedChannel.Name.TrimEnd(' ', '*')}";
            if (selectedChannel.IsEvents) return "Events";
            if (selectedChannel.IsGlobal) return "Global chat";
            return "All";
        }
    }

    [DataSourceProperty]
    public bool IsMuteButtonVisible => selectedChannel?.IsDirect == true;

    [DataSourceProperty]
    public string MuteButtonText => selectedChannel?.IsMuted == true ? "Unmute" : "Mute";

    [DataSourceProperty]
    public bool HasUnreadNotification => unreadMessageCount > 0;

    [DataSourceProperty]
    public string UnreadNotificationText => unreadMessageCount > 99
        ? "99+"
        : unreadMessageCount.ToString();

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

    public void ActionSend()
    {
        if (!IsPlayerChatEnabled) return;
        if (selectedChannel == null || selectedChannel.IsEvents) return;

        string text = WrittenText.Trim();
        if (text.Length == 0) return;

        var channel = selectedChannel.IsDirect ? ChatChannel.Direct : ChatChannel.Global;
        string recipientControllerId = channel == ChatChannel.Direct
            ? selectedChannel.ControllerId
            : string.Empty;

        send(new NetworkSendChatMessage(channel, recipientControllerId, text));
        WrittenText = string.Empty;
    }

    public void ActionToggleMute()
    {
        if (selectedChannel == null || !selectedChannel.IsDirect) return;

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
            // No BannerlordConfig under unit tests.
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
        // All reuses Events/Global line instances
        // Only tick the source lists
        TickHistory(EventsChannelId, dt);
        TickHistory(GlobalChannelId, dt);
        foreach (var pair in histories)
        {
            if (IsFixedChannelId(pair.Key)) continue;
            for (int i = 0; i < pair.Value.Count; i++)
                pair.Value[i].HandleFading(dt);
        }
    }

    public void AddParticipant(string controllerId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(controllerId) ||
            IsFixedChannelId(controllerId) ||
            string.Equals(controllerId, getLocalControllerId(), StringComparison.Ordinal))
        {
            return;
        }

        EnsureDirectChannel(controllerId, displayName);
    }

    public void SetParticipants(IEnumerable<(string ControllerId, string DisplayName)> participants)
    {
        if (participants == null) throw new ArgumentNullException(nameof(participants));

        var availableIds = new HashSet<string>(StringComparer.Ordinal);
        var availableParticipants = new List<(string ControllerId, string DisplayName)>();
        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.ControllerId) ||
                IsFixedChannelId(participant.ControllerId) ||
                string.Equals(participant.ControllerId, getLocalControllerId(), StringComparison.Ordinal) ||
                !availableIds.Add(participant.ControllerId))
            {
                continue;
            }

            availableParticipants.Add(participant);
        }

        if (selectedChannel?.IsDirect == true && !availableIds.Contains(selectedChannel.ControllerId))
            SelectChannel(channelsById[AllChannelId]);

        var directChannels = new List<ChatChannelVM>();
        foreach (var channel in Channels)
        {
            if (channel.IsDirect) directChannels.Add(channel);
        }

        foreach (var channel in directChannels)
            Channels.Remove(channel);

        foreach (var participant in availableParticipants)
            EnsureDirectChannel(participant.ControllerId, participant.DisplayName);
    }

    public void ReceiveEvent(string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        AddLine(EventsChannelId, new ChatLineVM(text, color, isPlayerChat: false), notify: false);
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
                EnsureDirectChannel(channelId, otherName);
                line = sentByLocalPlayer
                    ? $"[To {otherName}] You: {message.Text}"
                    : $"[From {otherName}] {otherName}: {message.Text}";
                notify = !sentByLocalPlayer;
                break;
            case ChatChannel.System:
                channelId = string.IsNullOrEmpty(message.RecipientControllerId)
                    ? selectedChannel?.IsDirect == true
                        ? selectedChannel.ControllerId
                        : GlobalChannelId
                    : message.RecipientControllerId;
                if (!IsFixedChannelId(channelId) && channelId.Length > 0)
                    EnsureDirectChannel(channelId, DisplayName(message.RecipientName, channelId));
                else if (IsFixedChannelId(channelId) && channelId != GlobalChannelId && channelId != EventsChannelId)
                    channelId = GlobalChannelId;
                line = $"[Chat] {message.Text}";
                notify = true;
                break;
            default:
                return;
        }

        AddLine(
            channelId,
            new ChatLineVM(line, getPlayerColor(message.SenderControllerId), isPlayerChat: true),
            notify);
    }

    private void EnsureFixedChannel(string channelId, string displayName, ChatChannelKind kind)
    {
        if (channelsById.ContainsKey(channelId)) return;

        var channel = new ChatChannelVM(channelId, displayName, kind, SelectChannel);
        channelsById.Add(channelId, channel);
        histories.Add(channelId, new List<ChatLineVM>());
        Channels.Add(channel);
    }

    private ChatChannelVM EnsureDirectChannel(string controllerId, string displayName)
    {
        if (channelsById.TryGetValue(controllerId, out var existing))
        {
            existing.UpdateDisplayName(displayName);
            if (!Channels.Contains(existing)) Channels.Add(existing);
            return existing;
        }

        var channel = new ChatChannelVM(controllerId, displayName, ChatChannelKind.Direct, SelectChannel);
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
            history = histories[EnsureDirectChannel(channelId, channelId).ControllerId];

        line.ToggleForceVisible(IsOpen);
        history.Add(line);
        ChatLineVM trimmed = TrimHistory(history);

        // Also append to All
        ChatLineVM allTrimmed = null;
        bool feedsAll = channelId == EventsChannelId || channelId == GlobalChannelId;
        if (feedsAll)
        {
            var allHistory = histories[AllChannelId];
            allHistory.Add(line);
            allTrimmed = TrimHistory(allHistory);
        }

        bool viewingThisChannel = IsOpen &&
            string.Equals(selectedChannel?.ControllerId, channelId, StringComparison.Ordinal);
        bool viewingAll = IsOpen && selectedChannel?.IsAll == true && feedsAll;
        bool passiveAll = !IsOpen && feedsAll;
        if (viewingThisChannel || viewingAll || passiveAll)
        {
            ChatLineVM visibleTrimmed = viewingAll || passiveAll ? allTrimmed : trimmed;
            AppendVisibleLine(line, visibleTrimmed);
        }
        else if (channelsById.TryGetValue(channelId, out var channel) && line.IsPlayerChat)
            channel.MarkUnread();

        if (notify && !IsOpen && line.IsPlayerChat && IsPlayerChatEnabled)
            SetUnreadMessageCount(Math.Min(unreadMessageCount + 1, 999));
    }

    private static ChatLineVM TrimHistory(List<ChatLineVM> history)
    {
        if (history.Count <= MaxHistoryPerChannel) return null;

        var trimmed = history[0];
        history.RemoveAt(0);
        return trimmed;
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
            ? selectedChannel?.ControllerId ?? AllChannelId
            : AllChannelId;

        VisibleLines.Clear();
        if (!histories.TryGetValue(channelId, out var history))
            return;

        // Closed shows recent fading lines
        // Open shows the full channel for scrolling
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

    private void TickHistory(string channelId, float dt)
    {
        if (!histories.TryGetValue(channelId, out var history)) return;
        for (int i = 0; i < history.Count; i++)
            history[i].HandleFading(dt);
    }

    private void RefreshForceVisible()
    {
        TickHistoryForceVisible(EventsChannelId);
        TickHistoryForceVisible(GlobalChannelId);
        foreach (var pair in histories)
        {
            if (IsFixedChannelId(pair.Key)) continue;
            for (int i = 0; i < pair.Value.Count; i++)
                pair.Value[i].ToggleForceVisible(IsOpen);
        }
    }

    private void TickHistoryForceVisible(string channelId)
    {
        if (!histories.TryGetValue(channelId, out var history)) return;
        for (int i = 0; i < history.Count; i++)
            history[i].ToggleForceVisible(IsOpen);
    }

    private void SetUnreadMessageCount(int count)
    {
        if (unreadMessageCount == count) return;

        unreadMessageCount = count;
        OnPropertyChanged(nameof(HasUnreadNotification));
        OnPropertyChanged(nameof(UnreadNotificationText));
    }

    private static bool IsFixedChannelId(string channelId)
    {
        return string.Equals(channelId, AllChannelId, StringComparison.Ordinal) ||
               string.Equals(channelId, EventsChannelId, StringComparison.Ordinal) ||
               string.Equals(channelId, GlobalChannelId, StringComparison.Ordinal);
    }

    private static string DisplayName(string name, string controllerId)
    {
        return string.IsNullOrWhiteSpace(name) ? controllerId ?? "Player" : name;
    }
}
