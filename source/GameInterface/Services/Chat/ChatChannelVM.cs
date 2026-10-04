using System;
using TaleWorlds.Library;

namespace GameInterface.Services.Chat;

internal enum ChatChannelKind
{
    All = 0,
    Events = 1,
    Global = 2,
    Direct = 3,
}

/// <summary>One selectable feed or direct-message channel.</summary>
internal sealed class ChatChannelVM : ViewModel
{
    private readonly Action<ChatChannelVM> select;
    private string displayName;
    private bool isSelected;
    private bool hasUnreadMessages;
    private bool isMuted;

    public ChatChannelVM(
        string controllerId,
        string displayName,
        ChatChannelKind kind,
        Action<ChatChannelVM> select)
    {
        if (select == null) throw new ArgumentNullException(nameof(select));

        ControllerId = controllerId ?? string.Empty;
        Kind = kind;
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? ControllerId : displayName;
        this.select = select;
    }

    public string ControllerId { get; }

    public ChatChannelKind Kind { get; }

    public bool IsAll => Kind == ChatChannelKind.All;

    public bool IsEvents => Kind == ChatChannelKind.Events;

    public bool IsGlobal => Kind == ChatChannelKind.Global;

    public bool IsDirect => Kind == ChatChannelKind.Direct;

    [DataSourceProperty]
    public string Name
    {
        get
        {
            string name = IsMuted ? $"{displayName} (Muted)" : displayName;
            return HasUnreadMessages ? $"{name} *" : name;
        }
    }

    public bool IsMuted => isMuted;

    [DataSourceProperty]
    public bool IsSelected
    {
        get => isSelected;
        private set
        {
            if (isSelected == value) return;
            isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public bool HasUnreadMessages
    {
        get => hasUnreadMessages;
        private set
        {
            if (hasUnreadMessages == value) return;
            hasUnreadMessages = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public void ExecuteSelection()
    {
        select(this);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selected) HasUnreadMessages = false;
    }

    public void MarkUnread()
    {
        if (!IsSelected) HasUnreadMessages = true;
    }

    public void SetMuted(bool muted)
    {
        if (isMuted == muted) return;

        isMuted = muted;
        OnPropertyChanged(nameof(Name));
    }

    public void UpdateDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(displayName, name, StringComparison.Ordinal)) return;

        displayName = name;
        OnPropertyChanged(nameof(Name));
    }
}
