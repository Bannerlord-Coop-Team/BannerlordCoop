using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>The panel's tabs, in the order they are shown.</summary>
internal enum ServerInfoTab
{
    Motd = 0,
    Rules = 1,
    Links = 2,
    News = 3,
}

/// <summary>Presents the server info as tabs, with a confirmation inside the panel before a link opens.</summary>
internal sealed class ServerInfoVM : ViewModel
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerInfoVM>();
    private static readonly ServerInfoTab[] TabOrder = { ServerInfoTab.Motd, ServerInfoTab.Rules, ServerInfoTab.Links, ServerInfoTab.News };

    // Gauntlet's double-click window. The dialog opens under the pointer, so a quick second click on a row would land on Open.
    internal const float OpenLinkDelaySeconds = 0.5f;
    // A long frame counts as at most this much, so a hitch during a double-click does not end the wait early.
    internal const float MaxOpenLinkTickSeconds = 0.125f;

    private readonly Action close;
    private readonly IServerInfoLinkRules linkRules;
    private readonly IBrowserLinkOpener opener;
    private bool isOpen;
    private ServerInfoTab selectedTab;
    private ServerInfoLinkVM pendingLink;
    private float openLinkWait;

    [DataSourceProperty] public MBBindingList<ServerInfoParagraphVM> Paragraphs { get; } = new MBBindingList<ServerInfoParagraphVM>();
    [DataSourceProperty] public MBBindingList<ServerInfoRuleVM> Rules { get; } = new MBBindingList<ServerInfoRuleVM>();
    [DataSourceProperty] public MBBindingList<ServerInfoLinkVM> Links { get; } = new MBBindingList<ServerInfoLinkVM>();
    [DataSourceProperty] public MBBindingList<ServerInfoNewsVM> News { get; } = new MBBindingList<ServerInfoNewsVM>();

    [DataSourceProperty] public string Title => new TextObject("{=coop_server_info_title}Server Info").ToString();
    [DataSourceProperty] public string MotdTabText => new TextObject("{=coop_server_info_motd}Message of the Day").ToString();
    [DataSourceProperty] public string RulesTabText => new TextObject("{=coop_server_info_rules}Rules").ToString();
    [DataSourceProperty] public string LinksTabText => new TextObject("{=coop_server_info_links}Links").ToString();
    [DataSourceProperty] public string NewsTabText => new TextObject("{=coop_server_info_news}News").ToString();
    [DataSourceProperty] public string HintText => new TextObject("{=coop_server_info_hint}Type !motd in chat to open this again").ToString();
    [DataSourceProperty] public string CloseText => new TextObject("{=coop_server_info_close}Close").ToString();
    [DataSourceProperty] public string LinkDialogTitle => new TextObject("{=coop_server_info_link_title}Open link").ToString();
    [DataSourceProperty] public string LinkDialogText => new TextObject("{=coop_server_info_link_text}Open this link in your browser?").ToString();
    [DataSourceProperty] public string OpenLinkText => new TextObject("{=coop_server_info_link_open}Open").ToString();
    [DataSourceProperty] public string CancelLinkText => new TextObject("{=coop_server_info_link_cancel}Cancel").ToString();

    [DataSourceProperty] public string LinkDialogAddress => pendingLink?.Address ?? string.Empty;
    [DataSourceProperty] public bool IsLinkDialogOpen => pendingLink != null;
    [DataSourceProperty] public bool IsOpenLinkEnabled => pendingLink != null && openLinkWait <= 0f;

    [DataSourceProperty] public bool HasMotd => Paragraphs.Count > 0;
    [DataSourceProperty] public bool HasRules => Rules.Count > 0;
    [DataSourceProperty] public bool HasLinks => Links.Count > 0;
    [DataSourceProperty] public bool HasNews => News.Count > 0;
    [DataSourceProperty] public bool IsMotdSelected => IsShown(ServerInfoTab.Motd);
    [DataSourceProperty] public bool IsRulesSelected => IsShown(ServerInfoTab.Rules);
    [DataSourceProperty] public bool IsLinksSelected => IsShown(ServerInfoTab.Links);
    [DataSourceProperty] public bool IsNewsSelected => IsShown(ServerInfoTab.News);

    [DataSourceProperty] public bool IsOpen
    {
        get => isOpen;
        set
        {
            if (isOpen == value) return;
            isOpen = value;
            // A dialog left open would come back with the next open.
            if (!value) SetPendingLink(null);
            OnPropertyChanged(nameof(IsOpen));
        }
    }

    public bool HasContent => HasMotd || HasRules || HasLinks || HasNews;
    public ServerInfoTab SelectedTab => selectedTab;
    public IEnumerable<ServerInfoTab> VisibleTabs => TabOrder.Where(HasTab);

    // Receives the overlay's close action, the client's own link check and the browser opener.
    public ServerInfoVM(Action close, IServerInfoLinkRules linkRules, IBrowserLinkOpener opener)
    {
        if (close == null) throw new ArgumentNullException(nameof(close));
        if (linkRules == null) throw new ArgumentNullException(nameof(linkRules));
        if (opener == null) throw new ArgumentNullException(nameof(opener));

        this.close = close;
        this.linkRules = linkRules;
        this.opener = opener;
    }

    // Replaces what every tab shows and reports whether any tab has content. A link the client's own
    // check refuses is never shown, whatever the server sent.
    public bool SetContent(NetworkServerInfo info)
    {
        SetPendingLink(null);
        Replace(Paragraphs, Texts(info?.Motd, ServerInfoLimits.MaxMotdParagraphs)
            .Select(text => new ServerInfoParagraphVM(text)));
        Replace(Rules, Texts(info?.Rules, ServerInfoLimits.MaxRules)
            .Select((text, index) => new ServerInfoRuleVM(index + 1, text)));
        // A server sends at most MaxLinks links, so checking only those keeps a bad server from flooding the log.
        Replace(Links, (info?.Links ?? Array.Empty<ServerInfoLink>())
            .Take(ServerInfoLimits.MaxLinks)
            .Select((link, index) => CreateLink(link, index))
            .Where(link => link != null));
        Replace(News, (info?.News ?? Array.Empty<ServerInfoNews>())
            .Where(item => item != null && (!string.IsNullOrWhiteSpace(item.Title) || !string.IsNullOrWhiteSpace(item.Text)))
            .Take(ServerInfoLimits.MaxNews)
            .Select((item, index) => new ServerInfoNewsVM(item, hasDivider: index > 0)));

        if (!HasTab(selectedTab)) selectedTab = VisibleTabs.FirstOrDefault();
        OnTabsChanged();
        return HasContent;
    }

    // Opens on the first tab that has content, in the order the tabs are shown.
    public void SelectFirstTab()
    {
        SetPendingLink(null);
        selectedTab = VisibleTabs.FirstOrDefault();
        OnTabsChanged();
    }

    // Bound to the tab buttons; an unknown index or a tab without content is ignored.
    public void ExecuteSelectTab(int tab)
    {
        if (!Enum.IsDefined(typeof(ServerInfoTab), tab) || !HasTab((ServerInfoTab)tab)) return;

        SetPendingLink(null);
        selectedTab = (ServerInfoTab)tab;
        OnTabsChanged();
    }

    // Allows the native close button to release overlay input focus.
    public void ExecuteClose() => close();

    // Opens the confirmed link; nothing opens without this click, and not before the wait after the dialog appeared.
    public void ExecuteOpenLink()
    {
        var link = pendingLink;
        if (link == null || !IsOpenLinkEnabled) return;

        SetPendingLink(null);
        opener.Open(link.Address);
    }

    public void ExecuteCancelLink() => SetPendingLink(null);

    // Runs each frame while the panel is open and counts down the wait before Open takes a click.
    public void Tick(float dt)
    {
        if (pendingLink == null || IsOpenLinkEnabled || !(dt > 0f)) return;

        openLinkWait -= Math.Min(dt, MaxOpenLinkTickSeconds);
        if (openLinkWait <= 0f) OnPropertyChanged(nameof(IsOpenLinkEnabled));
    }

    // Escape closes the link dialog first and the panel only when no dialog is open.
    public void HandleEscape()
    {
        if (IsLinkDialogOpen) ExecuteCancelLink();
        else ExecuteClose();
    }

    public override void OnFinalize()
    {
        base.OnFinalize();
        Replace(Paragraphs, Enumerable.Empty<ServerInfoParagraphVM>());
        Replace(Rules, Enumerable.Empty<ServerInfoRuleVM>());
        Replace(Links, Enumerable.Empty<ServerInfoLinkVM>());
        Replace(News, Enumerable.Empty<ServerInfoNewsVM>());
    }

    // The log names only the index: the address came from the server and is not trusted.
    private ServerInfoLinkVM CreateLink(ServerInfoLink link, int index)
    {
        if (link == null || !linkRules.TryNormalize(link.Url, out string address))
        {
            Logger.Warning("Server info link at index {Index} from the server has no usable http or https url; skipping it", index);
            return null;
        }

        // An empty label shows the address, so every row still says where it goes.
        string label = string.IsNullOrWhiteSpace(link.Label) ? address : link.Label;
        return new ServerInfoLinkVM(label, address, SetPendingLink);
    }

    private void SetPendingLink(ServerInfoLinkVM link)
    {
        if (ReferenceEquals(pendingLink, link)) return;

        pendingLink = link;
        openLinkWait = link == null ? 0f : OpenLinkDelaySeconds;
        OnPropertyChanged(nameof(IsLinkDialogOpen));
        OnPropertyChanged(nameof(LinkDialogAddress));
        OnPropertyChanged(nameof(IsOpenLinkEnabled));
    }

    private bool HasTab(ServerInfoTab tab)
    {
        switch (tab)
        {
            case ServerInfoTab.Motd: return HasMotd;
            case ServerInfoTab.Rules: return HasRules;
            case ServerInfoTab.Links: return HasLinks;
            case ServerInfoTab.News: return HasNews;
            default: return false;
        }
    }

    private bool IsShown(ServerInfoTab tab) => selectedTab == tab && HasTab(tab);

    private void OnTabsChanged()
    {
        OnPropertyChanged(nameof(HasMotd));
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasLinks));
        OnPropertyChanged(nameof(HasNews));
        OnPropertyChanged(nameof(IsMotdSelected));
        OnPropertyChanged(nameof(IsRulesSelected));
        OnPropertyChanged(nameof(IsLinksSelected));
        OnPropertyChanged(nameof(IsNewsSelected));
    }

    private static IEnumerable<string> Texts(string[] texts, int maxCount) =>
        (texts ?? Array.Empty<string>()).Where(text => !string.IsNullOrWhiteSpace(text)).Take(maxCount);

    private static void Replace<T>(MBBindingList<T> list, IEnumerable<T> items) where T : ViewModel
    {
        foreach (var item in list) item.OnFinalize();
        list.Clear();
        foreach (var item in items) list.Add(item);
    }
}

/// <summary>One operator-written paragraph, shown exactly as the server sent it.</summary>
internal sealed class ServerInfoParagraphVM : ViewModel
{
    // Bound as a plain string: a TextObject would expand {VARIABLES} and localization ids in operator text.
    [DataSourceProperty] public string Text { get; }

    public ServerInfoParagraphVM(string text) => Text = text;
}

/// <summary>One numbered rule, shown as written.</summary>
internal sealed class ServerInfoRuleVM : ViewModel
{
    [DataSourceProperty] public string Number { get; }
    [DataSourceProperty] public string Text { get; }

    public ServerInfoRuleVM(int number, string text)
    {
        Number = number + ".";
        Text = text;
    }
}

/// <summary>One link row; clicking it asks the panel to confirm before anything opens.</summary>
internal sealed class ServerInfoLinkVM : ViewModel
{
    private readonly Action<ServerInfoLinkVM> requestOpen;

    [DataSourceProperty] public string Label { get; }
    [DataSourceProperty] public string Address { get; }

    // Holds the address the client's own check produced, not the text the server sent.
    public ServerInfoLinkVM(string label, string address, Action<ServerInfoLinkVM> requestOpen)
    {
        Label = label;
        Address = address;
        this.requestOpen = requestOpen;
    }

    public void ExecuteOpen() => requestOpen(this);
}

/// <summary>One news entry; empty parts are hidden and entries after the first get a divider.</summary>
internal sealed class ServerInfoNewsVM : ViewModel
{
    [DataSourceProperty] public string Date { get; }
    [DataSourceProperty] public string Title { get; }
    [DataSourceProperty] public string Text { get; }
    [DataSourceProperty] public bool HasDate => Date.Length > 0;
    [DataSourceProperty] public bool HasTitle => Title.Length > 0;
    [DataSourceProperty] public bool HasText => Text.Length > 0;
    [DataSourceProperty] public bool HasDivider { get; }

    public ServerInfoNewsVM(ServerInfoNews item, bool hasDivider)
    {
        Date = string.IsNullOrWhiteSpace(item.Date) ? string.Empty : item.Date;
        Title = string.IsNullOrWhiteSpace(item.Title) ? string.Empty : item.Title;
        Text = string.IsNullOrWhiteSpace(item.Text) ? string.Empty : item.Text;
        HasDivider = hasDivider;
    }
}
