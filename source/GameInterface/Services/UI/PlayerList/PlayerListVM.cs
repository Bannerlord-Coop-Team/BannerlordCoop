using System;
using System.Linq;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.PlayerList;

/// <summary>Maintains stable UI rows while the server's roster presentation changes.</summary>
internal sealed class PlayerListVM : ViewModel
{
    private readonly Action close;
    private bool isOpen;
    [DataSourceProperty] public MBBindingList<PlayerListRowVM> Rows { get; } = new MBBindingList<PlayerListRowVM>();
    [DataSourceProperty] public string Title => new TextObject("{=coop_player_list_title}Players in Server").ToString();
    [DataSourceProperty] public string PlatformHeading => new TextObject("{=coop_platform_name}Platform Name").ToString();
    [DataSourceProperty] public string HeroHeading => new TextObject("{=coop_hero_name}Hero Name").ToString();
    [DataSourceProperty] public string StatusHeading => new TextObject("{=coop_player_status}Status").ToString();
    [DataSourceProperty] public string CloseText => new TextObject("{=coop_player_list_close_label}Close").ToString();
    [DataSourceProperty] public float PanelHeight => 236f + (56f * Math.Min(8, Math.Max(2, Rows.Count)));
    [DataSourceProperty] public string OnlineSummary
    {
        get
        {
            var count = Rows.Count(row => row.IsOnline);
            return new TextObject("{=coop_player_list_online_count}Players Online: {COUNT}")
                .SetTextVariable("COUNT", count).ToString();
        }
    }
    [DataSourceProperty] public bool IsOpen
    {
        get => isOpen;
        set { if (isOpen == value) return; isOpen = value; OnPropertyChanged(nameof(IsOpen)); }
    }

    // Supplies the overlay's close action without coupling rows to the screen system.
    public PlayerListVM(Action close) => this.close = close;

    // Updates existing rows in place to preserve scrolling and hover targets.
    public void Update(PlayerListEntry[] entries, string localControllerId)
    {
        foreach (var row in Rows.ToArray())
        {
            if (entries.Any(entry => entry.ControllerId == row.ControllerId)) continue;
            Rows.Remove(row);
            row.OnFinalize();
        }
        foreach (var entry in entries)
        {
            var row = Rows.FirstOrDefault(item => item.ControllerId == entry.ControllerId);
            if (row == null) Rows.Add(new PlayerListRowVM(entry));
            else row.Update(entry);
        }
        var sortedRows = Rows.OrderByDescending(row => row.ControllerId == localControllerId)
            .ThenByDescending(row => row.IsOnline)
            .ThenBy(row => row.PlatformName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.ControllerId, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < sortedRows.Length; index++)
        {
            var row = sortedRows[index];
            if (Rows[index] != row)
            {
                Rows.Remove(row);
                Rows.Insert(index, row);
            }
            row.IsAlternate = index % 2 != 0;
        }
        OnPropertyChanged(nameof(OnlineSummary));
        OnPropertyChanged(nameof(PanelHeight));
    }

    // Allows the native close button to release overlay input focus.
    public void ExecuteClose() => close();
}

/// <summary>Formats one roster entry with full-name tooltips and localized activity text.</summary>
internal sealed class PlayerListRowVM : ViewModel
{
    private PlayerListEntry entry;
    private bool isAlternate;
    private HintViewModel nameHint;
    public string ControllerId => entry.ControllerId;
    [DataSourceProperty] public bool IsOnline => entry.Online;
    [DataSourceProperty] public bool IsOffline => !entry.Online;
    public string PresenceText => new TextObject(entry.Online ? "{=coop_player_online}Online" : "{=coop_player_offline}Offline").ToString();
    [DataSourceProperty] public float RowOpacity => entry.Online ? 1f : 0.55f;
    [DataSourceProperty] public bool IsAlternate
    {
        get => isAlternate;
        set { if (isAlternate == value) return; isAlternate = value; OnPropertyChanged(nameof(IsAlternate)); }
    }
    [DataSourceProperty] public string ActivityIcon => !entry.Online ? @"SPGeneral\GameMenu\leave_icon" : entry.Activity switch
    {
        PlayerActivity.Town => @"SPGeneral\GameMenu\visit_town_icon",
        PlayerActivity.Village => @"SPGeneral\MapOverlay\Settlement\icon_village_big",
        PlayerActivity.Castle => @"SPGeneral\MapOverlay\Settlement\icon_wall_big",
        PlayerActivity.Battle => @"SPGeneral\GameMenu\ordertroopstoattack_icon",
        PlayerActivity.Siege => @"SPGeneral\GameMenu\besiege_icon",
        PlayerActivity.Hideout => @"SPGeneral\GameMenu\ambush_icon",
        PlayerActivity.Travelling => @"SPGeneral\GameMenu\leave_icon",
        _ => @"SPGeneral\GameMenu\wait_icon"
    };
    [DataSourceProperty] public string PlatformName => string.IsNullOrWhiteSpace(entry.PlatformName)
        ? new TextObject("{=coop_platform_unknown}Unknown platform name").ToString() : entry.PlatformName;
    [DataSourceProperty] public string HeroName => string.IsNullOrWhiteSpace(entry.HeroName)
        ? new TextObject("{=coop_hero_unassigned}Hero not assigned").ToString() : entry.HeroName;
    [DataSourceProperty] public string Status => !entry.Online
        ? new TextObject("{=coop_player_offline}Offline").ToString()
        : ActivityText(entry.Activity);

    // Initializes the display fields for a single stable controller identity.
    public PlayerListRowVM(PlayerListEntry entry) => Update(entry);

    // Refreshes bindings only when a presentation field changed.
    public void Update(PlayerListEntry value)
    {
        if (entry == value) return;
        entry = value;
        nameHint = new HintViewModel(new TextObject("{=coop_player_list_presence_names}{PRESENCE}\n{PLATFORM}\n{HERO}")
            .SetTextVariable("PRESENCE", PresenceText).SetTextVariable("PLATFORM", PlatformName).SetTextVariable("HERO", HeroName));
        OnPropertyChanged(nameof(PlatformName));
        OnPropertyChanged(nameof(HeroName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(RowOpacity));
        OnPropertyChanged(nameof(ActivityIcon));
    }

    // Displays both full names while the native row owns hover and its highlight.
    public void ExecuteBeginHint() => nameHint.ExecuteBeginHint();

    // Releases the shared name tooltip when the pointer leaves the row.
    public void ExecuteEndHint() => nameHint.ExecuteEndHint();

    // Keeps wire activity values independent from the client's chosen language.
    private string ActivityText(PlayerActivity activity) => new TextObject(activity switch
    {
        PlayerActivity.Idle => "{=coop_activity_idle}Idle",
        PlayerActivity.Town => "{=coop_activity_town}In Town",
        PlayerActivity.Village => "{=coop_activity_village}In Village",
        PlayerActivity.Castle => "{=coop_activity_castle}In Castle",
        PlayerActivity.Battle => "{=coop_activity_battle}In Battle",
        PlayerActivity.Siege => "{=coop_activity_siege}In Siege",
        PlayerActivity.Hideout => "{=coop_activity_hideout}In Hideout",
        PlayerActivity.Travelling => "{=coop_activity_travelling}Travelling",
        _ => "{=coop_activity_connecting}Connecting"
    }).ToString();
}
