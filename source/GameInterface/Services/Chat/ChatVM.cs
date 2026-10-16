using GameInterface.Services.Chat.Messages;
using GameInterface.Services.UI;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace GameInterface.Services.Chat;

/// <summary>Bounded, session-only chat history and channel selection.</summary>
internal sealed class ChatVM : ViewModel
{
    internal const string AllChannelId = "__all__";
    internal const string EventsChannelId = "__events__";
    internal const string GlobalChannelId = "__global__";
    internal const string ClanChannelId = "__clan__";
    internal const string KingdomChannelId = "__kingdom__";
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
    private float chatBoxSizeX;
    private float chatBoxSizeY;
    // Closed-panel fade feed (Events, Global, Clan, Kingdom, DMs). All stays Events+Global only.
    private readonly List<ChatLineVM> passiveHistory = new List<ChatLineVM>();

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
        // Session only, no BannerlordConfig save
        chatBoxSizeX = DefaultChatBoxSizeX;
        chatBoxSizeY = DefaultChatBoxSizeY;

        EnsureFixedChannel(AllChannelId, "All", ChatChannelKind.All, addToList: true);
        EnsureFixedChannel(EventsChannelId, "Events", ChatChannelKind.Events, addToList: true);
        EnsureFixedChannel(GlobalChannelId, "Global", ChatChannelKind.Global, addToList: true);
        // Listed only while the local player belongs to that group
        EnsureFixedChannel(ClanChannelId, "Clan", ChatChannelKind.Clan, addToList: false);
        EnsureFixedChannel(KingdomChannelId, "Kingdom", ChatChannelKind.Kingdom, addToList: false);
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
            if (selectedChannel.IsClan) return "Clan chat";
            if (selectedChannel.IsKingdom) return "Kingdom chat";
            return "All";
        }
    }

    [DataSourceProperty]
    public bool IsMuteButtonVisible => selectedChannel?.IsDirect == true;

    [DataSourceProperty]
    public string MuteButtonText => selectedChannel?.IsMuted == true ? "Unmute" : "Mute";

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
            OnPropertyChanged(nameof(IsChatInputEnabled));
            RefreshForceVisible();
            UpdateVisibleLines();
        }
    }

    /// <summary>False on Events (read-only) or when the panel is closed.</summary>
    [DataSourceProperty]
    public bool IsChatInputEnabled => isOpen && selectedChannel?.IsEvents != true;

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
        if (selectedChannel == null || selectedChannel.IsEvents) return;

        string text = WrittenText.Trim();
        if (text.Length == 0) return;

        if (!TryGetSendChannel(selectedChannel, out var channel)) return;

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
        IsOpen = open;
    }

    public void Tick(float dt)
    {
        // All reuses Events/Global line instances
        // Only tick the source lists
        TickHistory(EventsChannelId, dt);
        TickHistory(GlobalChannelId, dt);
        TickHistory(ClanChannelId, dt);
        TickHistory(KingdomChannelId, dt);
        foreach (var pair in histories)
        {
            if (IsFixedChannelId(pair.Key)) continue;
            for (int i = 0; i < pair.Value.Count; i++)
                pair.Value[i].HandleFading(dt);
        }
    }

    /// <summary>Lists Clan and Kingdom tabs before DMs when the local player belongs to them.</summary>
    public void SetGroupChannelMembership(bool inClan, bool inKingdom)
    {
        SetFixedChannelListed(ClanChannelId, inClan);
        SetFixedChannelListed(KingdomChannelId, inKingdom);
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

        AddLine(EventsChannelId, new ChatLineVM(text, color, isPlayerChat: false));
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

        switch (message.Channel)
        {
            case ChatChannel.Global:
                channelId = GlobalChannelId;
                line = $"[Global] {DisplayName(message.SenderName, message.SenderControllerId)}: {message.Text}";
                AddParticipant(message.SenderControllerId, message.SenderName);
                break;
            case ChatChannel.Clan:
                channelId = ClanChannelId;
                line = $"[Clan] {DisplayName(message.SenderName, message.SenderControllerId)}: {message.Text}";
                AddParticipant(message.SenderControllerId, message.SenderName);
                break;
            case ChatChannel.Kingdom:
                channelId = KingdomChannelId;
                line = $"[Kingdom] {DisplayName(message.SenderName, message.SenderControllerId)}: {message.Text}";
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
                break;
            case ChatChannel.System:
                channelId = string.IsNullOrEmpty(message.RecipientControllerId)
                    ? selectedChannel?.IsDirect == true
                        ? selectedChannel.ControllerId
                        : GlobalChannelId
                    : message.RecipientControllerId;
                if (channelId != GlobalChannelId)
                    EnsureDirectChannel(channelId, DisplayName(message.RecipientName, channelId));
                line = $"[Chat] {message.Text}";
                break;
            default:
                return;
        }

        AddLine(
            channelId,
            new ChatLineVM(line, getPlayerColor(message.SenderControllerId), isPlayerChat: true));
    }

    private void EnsureFixedChannel(string channelId, string displayName, ChatChannelKind kind, bool addToList)
    {
        if (channelsById.ContainsKey(channelId)) return;

        var channel = new ChatChannelVM(channelId, displayName, kind, SelectChannel);
        channelsById.Add(channelId, channel);
        histories.Add(channelId, new List<ChatLineVM>());
        if (addToList) Channels.Add(channel);
    }

    private void SetFixedChannelListed(string channelId, bool listed)
    {
        if (!channelsById.TryGetValue(channelId, out var channel)) return;

        bool present = Channels.Contains(channel);
        if (listed == present) return;

        if (!listed)
        {
            if (ReferenceEquals(selectedChannel, channel))
                SelectChannel(channelsById[AllChannelId]);
            Channels.Remove(channel);
            return;
        }

        Channels.Insert(IndexForGroupChannel(channel.Kind), channel);
    }

    private int IndexForGroupChannel(ChatChannelKind kind)
    {
        // After Global (and after Clan when inserting Kingdom), before the first DM
        for (int i = 0; i < Channels.Count; i++)
        {
            var existing = Channels[i];
            if (existing.IsDirect) return i;
            if (kind == ChatChannelKind.Clan && existing.IsKingdom) return i;
        }

        return Channels.Count;
    }

    private static bool TryGetSendChannel(ChatChannelVM selected, out ChatChannel channel)
    {
        if (selected.IsDirect)
        {
            channel = ChatChannel.Direct;
            return true;
        }

        if (selected.IsClan)
        {
            channel = ChatChannel.Clan;
            return true;
        }

        if (selected.IsKingdom)
        {
            channel = ChatChannel.Kingdom;
            return true;
        }

        // All and Global both send on Global. Events is blocked before this.
        channel = ChatChannel.Global;
        return selected.IsAll || selected.IsGlobal;
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
        OnPropertyChanged(nameof(IsChatInputEnabled));
        UpdateVisibleLines();
    }

    private void AddLine(string channelId, ChatLineVM line)
    {
        var history = histories[channelId];

        line.ToggleForceVisible(IsOpen);
        history.Add(line);
        ChatLineVM trimmed = TrimHistory(history);

        // All is Events+Global only (not Clan, Kingdom, or DMs)
        ChatLineVM allTrimmed = null;
        bool feedsAll = channelId == EventsChannelId || channelId == GlobalChannelId;
        if (feedsAll)
        {
            var allHistory = histories[AllChannelId];
            allHistory.Add(line);
            allTrimmed = TrimHistory(allHistory);
        }

        passiveHistory.Add(line);
        ChatLineVM passiveTrimmed = TrimHistory(passiveHistory);

        bool viewingThisChannel = IsOpen &&
            string.Equals(selectedChannel?.ControllerId, channelId, StringComparison.Ordinal);
        bool viewingAll = IsOpen && selectedChannel?.IsAll == true && feedsAll;
        if (!IsOpen)
            AppendVisibleLine(line, passiveTrimmed);
        else if (viewingThisChannel || viewingAll)
        {
            ChatLineVM visibleTrimmed = viewingAll ? allTrimmed : trimmed;
            AppendVisibleLine(line, visibleTrimmed);
        }
        else if (channelsById.TryGetValue(channelId, out var channel) && line.IsPlayerChat)
            channel.MarkUnread();
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
        if (trimmed != null)
            VisibleLines.Remove(trimmed);
        if (!IsOpen && VisibleLines.Count >= VisibleHistoryLines)
            VisibleLines.RemoveAt(0);

        VisibleLines.Add(line);
        FeedScrolledToBottomRequested?.Invoke();
    }

    private void UpdateVisibleLines()
    {
        VisibleLines.Clear();

        IList<ChatLineVM> history;
        if (!IsOpen)
            history = passiveHistory;
        else if (!histories.TryGetValue(
                     selectedChannel?.ControllerId ?? AllChannelId,
                     out var channelHistory))
            return;
        else
            history = channelHistory;

        int firstLine = !IsOpen
            ? Math.Max(0, history.Count - VisibleHistoryLines)
            : 0;

        for (int i = firstLine; i < history.Count; i++)
            VisibleLines.Add(history[i]);

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
        TickHistoryForceVisible(ClanChannelId);
        TickHistoryForceVisible(KingdomChannelId);
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

    private static bool IsFixedChannelId(string channelId)
    {
        return string.Equals(channelId, AllChannelId, StringComparison.Ordinal) ||
               string.Equals(channelId, EventsChannelId, StringComparison.Ordinal) ||
               string.Equals(channelId, GlobalChannelId, StringComparison.Ordinal) ||
               string.Equals(channelId, ClanChannelId, StringComparison.Ordinal) ||
               string.Equals(channelId, KingdomChannelId, StringComparison.Ordinal);
    }

    private static string DisplayName(string name, string controllerId)
    {
        return string.IsNullOrWhiteSpace(name) ? controllerId ?? "Player" : name;
    }
}
