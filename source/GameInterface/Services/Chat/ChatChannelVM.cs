using System;
using TaleWorlds.Library;

namespace GameInterface.Services.Chat;

/// <summary>One selectable global or direct-message channel.</summary>
internal sealed class ChatChannelVM : ViewModel
{
    private readonly Action<ChatChannelVM> select;
    private string displayName;
    private bool isSelected;
    private bool hasUnreadMessages;
    private bool isMuted;

    public ChatChannelVM(string controllerId, string displayName, Action<ChatChannelVM> select)
    {
        ControllerId = controllerId ?? string.Empty;
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? ControllerId : displayName;
        this.select = select ?? throw new ArgumentNullException(nameof(select));
    }

    public string ControllerId { get; }
    public bool IsGlobal => ControllerId.Length == 0;

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
