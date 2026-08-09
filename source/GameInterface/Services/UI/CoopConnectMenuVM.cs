using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using GameInterface.Services.UI.Donate;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

/// <summary>View model for direct connection and the active storefront's session browser.</summary>
public class CoopConnectMenuVM : ViewModel, IDisposable
{
    public const string DirectTabId = "direct";
    public const string SessionBrowserTabId = "provider_sessions";
    public const int SessionPageSize = 4;

    public event Action SessionBrowserTabActivated;

    private readonly ISessionBrowser sessionBrowser;
    private readonly IMessageBroker messageBroker;
    private readonly List<SessionListingListItemVM> discoveredSessions = new();

    private CoopConnectionTabVM selectedTab;
    private string sessionHostSearchText = string.Empty;
    private string sessionStatusText = string.Empty;
    private bool isRefreshingSessions;
    private bool disposed;
    private int lobbyRequestGeneration;
    private int filteredSessionCount;
    private long filteredSessionPlayerCount;
    private int sessionPageIndex;

    public string JoinButtonText => "Join";
    public string RefreshButtonText => "Refresh";
    public string SearchButtonText => "Search";
    public string PreviousPageButtonText => "Previous";
    public string NextPageButtonText => "Next";
    public string DiscordButtonText => "Discord";
    public string PatreonButtonText => "Patreon";
    public string DonateButtonText => "Donate";
    public string CreditsButtonText => "Credits";
    public string MovieTextHeader => "Join Co-op Sandbox";
    public string CommunityText => "Join the Community";
    public string SessionBrowserHeaderText =>
        $"Hosted {sessionBrowser?.DisplayName} Servers ({filteredSessionCount} servers; " +
        $"{filteredSessionPlayerCount} players)";
    public string SessionPageText => $"Page {CurrentSessionPage} of {SessionPageCount}";
    public int CurrentSessionPage => filteredSessionCount == 0 ? 0 : sessionPageIndex + 1;
    public int SessionPageCount => filteredSessionCount == 0
        ? 0
        : ((filteredSessionCount - 1) / SessionPageSize) + 1;
    public string HostSearchLabelText => "Host Name";
    public string HostSearchPlaceholderText => "Type a host name...";
    public string HostColumnText => "Host Name";
    public string ConnectedPlayersColumnText => "Connected Players";
    public string PasswordColumnText => "Access";
    public string CompatibilityColumnText => "Status";
    public string IpText => "Server IP Address:";
    public string PortText => "Port:";
    public string PasswordText => "Password:";

    [DataSourceProperty]
    public HintViewModel ServerAddressHint { get; } = new HintViewModel(new TextObject(
        "The address of the co-op server to join. Keep localhost if you are the host; otherwise, type the address your friend shared to join their game."));

    [DataSourceProperty]
    public HintViewModel PortHint { get; } = new HintViewModel(new TextObject(
        "The port the co-op server listens on. Leave 4200 unless the host changed it."));

    [DataSourceProperty]
    public HintViewModel PasswordHint { get; } = new HintViewModel(new TextObject(
        "The session password set by the host. Leave empty if the host has not set one."));

    public string connectIP = "localhost";
    public string connectPort = "4200";
    public string connectPassword = "";

    public CoopConnectMenuVM()
        : this(SessionDiscovery.Browser, MessageBroker.Instance)
    {
    }

    public CoopConnectMenuVM(ISessionBrowser sessionBrowser, IMessageBroker messageBroker)
    {
        this.sessionBrowser = sessionBrowser;
        this.messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));

        Tabs = new MBBindingList<CoopConnectionTabVM>();
        Tabs.Add(new CoopConnectionTabVM(DirectTabId, "Direct", SelectTab));
        if (sessionBrowser?.IsAvailable == true)
        {
            Tabs.Add(new CoopConnectionTabVM(
                SessionBrowserTabId,
                sessionBrowser.DisplayName + " Lobbies",
                SelectTab));
        }
        Sessions = new MBBindingList<SessionListingListItemVM>();

        SelectTab(Tabs[0]);
    }

    [DataSourceProperty]
    public MBBindingList<CoopConnectionTabVM> Tabs { get; }

    [DataSourceProperty]
    public MBBindingList<SessionListingListItemVM> Sessions { get; }

    [DataSourceProperty]
    public string SessionHostSearchText
    {
        get => sessionHostSearchText;
        set
        {
            value ??= string.Empty;
            if (sessionHostSearchText == value) return;

            sessionHostSearchText = value;
            OnPropertyChanged(nameof(SessionHostSearchText));

            if (!disposed && !IsRefreshingSessions)
            {
                ApplySessionHostFilter(resetPage: true);
            }
        }
    }

    [DataSourceProperty]
    public CoopConnectionTabVM SelectedTab
    {
        get => selectedTab;
        private set
        {
            if (selectedTab == value) return;

            selectedTab = value;
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(IsDirectTabVisible));
            OnPropertyChanged(nameof(IsSessionBrowserTabVisible));
        }
    }

    [DataSourceProperty]
    public bool IsDirectTabVisible => SelectedTab?.Id == DirectTabId;

    [DataSourceProperty]
    public bool IsSessionBrowserTabVisible => SelectedTab?.Id == SessionBrowserTabId;

    [DataSourceProperty]
    public bool IsRefreshingSessions
    {
        get => isRefreshingSessions;
        private set
        {
            if (isRefreshingSessions == value) return;

            isRefreshingSessions = value;
            OnPropertyChanged(nameof(IsRefreshingSessions));
            OnPropertyChanged(nameof(IsRefreshSessionsDisabled));
            OnPropertyChanged(nameof(IsSearchSessionsDisabled));
            OnPropertyChanged(nameof(IsPreviousSessionPageDisabled));
            OnPropertyChanged(nameof(IsNextSessionPageDisabled));
        }
    }

    [DataSourceProperty]
    public bool IsRefreshSessionsDisabled => sessionBrowser?.IsAvailable != true || IsRefreshingSessions;

    [DataSourceProperty]
    public bool IsSearchSessionsDisabled => IsRefreshingSessions;

    [DataSourceProperty]
    public bool IsSessionPaginationVisible => SessionPageCount > 1;

    [DataSourceProperty]
    public bool IsPreviousSessionPageDisabled => IsRefreshingSessions || sessionPageIndex == 0;

    [DataSourceProperty]
    public bool IsNextSessionPageDisabled => IsRefreshingSessions ||
        sessionPageIndex >= SessionPageCount - 1;

    [DataSourceProperty]
    public string SessionStatusText
    {
        get => sessionStatusText;
        private set
        {
            value ??= string.Empty;
            if (sessionStatusText == value) return;

            sessionStatusText = value;
            OnPropertyChanged(nameof(SessionStatusText));
            OnPropertyChanged(nameof(IsSessionStatusVisible));
        }
    }

    [DataSourceProperty]
    public bool IsSessionStatusVisible => !string.IsNullOrEmpty(SessionStatusText);

    [DataSourceProperty]
    public string Ip
    {
        get => connectIP;
        set
        {
            if (value == connectIP) return;

            connectIP = value;
            OnPropertyChanged(nameof(Ip));
        }
    }

    [DataSourceProperty]
    public string Port
    {
        get => connectPort;
        set
        {
            // TODO update config
            if (value == connectPort) return;

            connectPort = value;
            OnPropertyChanged(nameof(Port));
        }
    }

    [DataSourceProperty]
    public string Password
    {
        get => connectPassword;
        set
        {
            if (value == connectPassword) return;

            connectPassword = value;
            OnPropertyChanged(nameof(Password));
        }
    }

    public void ActionRefreshSessions()
    {
        if (disposed || IsRefreshingSessions) return;

        discoveredSessions.Clear();
        ClearSessionDisplay();

        if (sessionBrowser?.IsAvailable != true)
        {
            SessionStatusText = "Storefront session discovery is unavailable.";
            return;
        }

        int generation = ++lobbyRequestGeneration;
        IsRefreshingSessions = true;
        SessionStatusText = $"Searching for hosted {sessionBrowser.DisplayName} lobbies...";

        try
        {
            sessionBrowser.RequestSessions(
                (lobbies, error) => CompleteLobbyRefresh(generation, lobbies, error));
        }
        catch (Exception ex)
        {
            CompleteLobbyRefresh(generation, Array.Empty<SessionListing>(),
                $"Could not search {sessionBrowser.DisplayName} lobbies: {ex.Message}");
        }
    }

    public void ActionSearchSessions()
    {
        if (disposed || IsRefreshingSessions) return;

        ApplySessionHostFilter(resetPage: true);
    }

    public void ActionPreviousSessionPage()
    {
        if (disposed || IsPreviousSessionPageDisabled) return;

        sessionPageIndex--;
        ApplySessionHostFilter(resetPage: false);
    }

    public void ActionNextSessionPage()
    {
        if (disposed || IsNextSessionPageDisabled) return;

        sessionPageIndex++;
        ApplySessionHostFilter(resetPage: false);
    }

    public void ActionConnect()
    {
        if (!int.TryParse(connectPort, out var port) || port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
        {
            InformationManager.DisplayMessage(new InformationMessage("ERROR: The connection port is invalid"));
            return;
        }

        if (!ConnectionPassword.IsValid(connectPassword))
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"ERROR: The password cannot exceed {ConnectionPassword.MaxLength} characters"));
            return;
        }

        try
        {
            IPAddress ip;

            if (IPAddress.TryParse(connectIP, out var enteredIp))
            {
                ip = enteredIp;
            }
            else
            {
                var addresses = Dns.GetHostAddresses(connectIP);
                ip = addresses.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

                if (ip == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage("ERROR: No IPv4 address found for host"));
                    return;
                }
            }

            messageBroker.Publish(this, new AttemptJoin(ip, port, connectPassword));
        }
        catch (Exception ex)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"ERROR: The connection address could not be resolved: {ex.Message}"));
        }
    }

    public void ActionCancel()
    {
        ScreenManager.PopScreen();
    }

    public void ActionDiscord() => CommunityLinks.OpenDiscord();

    public void ActionPatreon() => CommunityLinks.OpenPatreon();

    // Opens a popup listing the individual donation platforms above a close button.
    public void ActionDonate() => CommunityLinks.ShowDonatePopup();

    // Opens a popup listing contributor, community, and supporter names.
    public void ActionCredits() => CommunityLinks.ShowCreditsPopup();

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        lobbyRequestGeneration++;
        IsRefreshingSessions = false;
        discoveredSessions.Clear();
        ClearSessionDisplay();
    }

    private void SelectTab(CoopConnectionTabVM tab)
    {
        if (disposed || tab == null || SelectedTab == tab) return;

        if (SelectedTab != null)
        {
            SelectedTab.IsSelected = false;
        }

        SelectedTab = tab;
        SelectedTab.IsSelected = true;

        if (SelectedTab.Id == SessionBrowserTabId)
        {
            SessionBrowserTabActivated?.Invoke();
            ActionRefreshSessions();
        }
    }

    private void CompleteLobbyRefresh(
        int generation,
        IReadOnlyList<SessionListing> lobbies,
        string error)
    {
        if (disposed || generation != lobbyRequestGeneration) return;

        IsRefreshingSessions = false;

        if (!string.IsNullOrWhiteSpace(error))
        {
            ClearSessionDisplay();
            SessionStatusText = error;
            return;
        }

        lobbies ??= Array.Empty<SessionListing>();

        foreach (var lobby in lobbies)
        {
            if (!lobby.Id.IsValid ||
                !string.Equals(lobby.Id.Provider, sessionBrowser.Provider, StringComparison.Ordinal))
            {
                continue;
            }

            discoveredSessions.Add(new SessionListingListItemVM(
                lobby,
                RequestSessionJoin));
        }

        ApplySessionHostFilter(resetPage: true);
    }

    private void ApplySessionHostFilter(bool resetPage)
    {
        string searchText = SessionHostSearchText.Trim();
        var filteredLobbies = discoveredSessions
            .Where(lobby => searchText.Length == 0 ||
                lobby.HostText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        filteredSessionCount = filteredLobbies.Count;
        filteredSessionPlayerCount = filteredLobbies.Sum(
            lobby => (long)lobby.ConnectedPlayers);
        if (resetPage)
        {
            sessionPageIndex = 0;
        }
        else
        {
            sessionPageIndex = Math.Min(sessionPageIndex, Math.Max(0, SessionPageCount - 1));
        }

        Sessions.Clear();
        foreach (var lobby in filteredLobbies
            .Skip(sessionPageIndex * SessionPageSize)
            .Take(SessionPageSize))
        {
            Sessions.Add(lobby);
        }

        NotifySessionDisplayChanged();

        if (filteredSessionCount > 0)
        {
            SessionStatusText = string.Empty;
        }
        else if (discoveredSessions.Count == 0)
        {
            SessionStatusText = $"No hosted {sessionBrowser.DisplayName} lobbies were found.";
        }
        else
        {
            SessionStatusText = $"No hosts match '{searchText}'.";
        }
    }

    private void ClearSessionDisplay()
    {
        filteredSessionCount = 0;
        filteredSessionPlayerCount = 0;
        sessionPageIndex = 0;
        Sessions.Clear();
        NotifySessionDisplayChanged();
    }

    private void NotifySessionDisplayChanged()
    {
        OnPropertyChanged(nameof(SessionBrowserHeaderText));
        OnPropertyChanged(nameof(SessionPageText));
        OnPropertyChanged(nameof(CurrentSessionPage));
        OnPropertyChanged(nameof(SessionPageCount));
        OnPropertyChanged(nameof(IsSessionPaginationVisible));
        OnPropertyChanged(nameof(IsPreviousSessionPageDisabled));
        OnPropertyChanged(nameof(IsNextSessionPageDisabled));
    }

    private void RequestSessionJoin(SessionListingId listingId)
    {
        if (disposed || !listingId.IsValid) return;

        messageBroker.Publish(this, new JoinSessionListing(listingId));
    }

}
