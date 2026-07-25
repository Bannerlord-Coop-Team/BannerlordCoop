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

/// <summary>View model for direct connection and public standalone Steam-lobby discovery.</summary>
public class CoopConnectMenuVM : ViewModel, IDisposable
{
    public const string DirectTabId = "direct";
    public const string SteamLobbiesTabId = "steam_lobbies";
    public const int SteamLobbyPageSize = 4;

    public event Action SteamLobbiesTabActivated;

    private readonly ISteamLobbyBrowser steamLobbyBrowser;
    private readonly IMessageBroker messageBroker;
    private readonly List<SteamLobbyListItemVM> discoveredSteamLobbies = new();

    private CoopConnectionTabVM selectedTab;
    private string steamLobbyHostSearchText = string.Empty;
    private string steamLobbyStatusText = string.Empty;
    private bool isRefreshingSteamLobbies;
    private bool disposed;
    private int lobbyRequestGeneration;
    private int filteredSteamLobbyCount;
    private int steamLobbyPageIndex;

    public string JoinButtonText => "Join";
    public string RefreshButtonText => "Refresh";
    public string SearchButtonText => "Search";
    public string PreviousPageButtonText => "Previous";
    public string NextPageButtonText => "Next";
    public string DiscordButtonText => "Discord";
    public string PatreonButtonText => "Patreon";
    public string DonateButtonText => "Donate";
    public string MovieTextHeader => "Join Co-op Sandbox";
    public string CommunityText => "Join the Community";
    public string SteamLobbiesHeaderText => $"Hosted Steam Servers ({filteredSteamLobbyCount})";
    public string SteamLobbyPageText => $"Page {CurrentSteamLobbyPage} of {SteamLobbyPageCount}";
    public int CurrentSteamLobbyPage => filteredSteamLobbyCount == 0 ? 0 : steamLobbyPageIndex + 1;
    public int SteamLobbyPageCount => filteredSteamLobbyCount == 0
        ? 0
        : ((filteredSteamLobbyCount - 1) / SteamLobbyPageSize) + 1;
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
        : this(SessionDiscovery.SteamLobbyBrowser, MessageBroker.Instance)
    {
    }

    public CoopConnectMenuVM(ISteamLobbyBrowser steamLobbyBrowser, IMessageBroker messageBroker)
    {
        this.steamLobbyBrowser = steamLobbyBrowser;
        this.messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));

        Tabs = new MBBindingList<CoopConnectionTabVM>
        {
            new CoopConnectionTabVM(DirectTabId, "Direct", SelectTab),
            new CoopConnectionTabVM(SteamLobbiesTabId, "Steam Lobbies", SelectTab),
        };
        SteamLobbies = new MBBindingList<SteamLobbyListItemVM>();

        SelectTab(Tabs[0]);
    }

    [DataSourceProperty]
    public MBBindingList<CoopConnectionTabVM> Tabs { get; }

    [DataSourceProperty]
    public MBBindingList<SteamLobbyListItemVM> SteamLobbies { get; }

    [DataSourceProperty]
    public string SteamLobbyHostSearchText
    {
        get => steamLobbyHostSearchText;
        set
        {
            value ??= string.Empty;
            if (steamLobbyHostSearchText == value) return;

            steamLobbyHostSearchText = value;
            OnPropertyChanged(nameof(SteamLobbyHostSearchText));

            if (!disposed && !IsRefreshingSteamLobbies)
            {
                ApplySteamLobbyHostFilter(resetPage: true);
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
            OnPropertyChanged(nameof(IsSteamLobbiesTabVisible));
        }
    }

    [DataSourceProperty]
    public bool IsDirectTabVisible => SelectedTab?.Id == DirectTabId;

    [DataSourceProperty]
    public bool IsSteamLobbiesTabVisible => SelectedTab?.Id == SteamLobbiesTabId;

    [DataSourceProperty]
    public bool IsRefreshingSteamLobbies
    {
        get => isRefreshingSteamLobbies;
        private set
        {
            if (isRefreshingSteamLobbies == value) return;

            isRefreshingSteamLobbies = value;
            OnPropertyChanged(nameof(IsRefreshingSteamLobbies));
            OnPropertyChanged(nameof(IsRefreshSteamLobbiesDisabled));
            OnPropertyChanged(nameof(IsSearchSteamLobbiesDisabled));
            OnPropertyChanged(nameof(IsPreviousSteamLobbyPageDisabled));
            OnPropertyChanged(nameof(IsNextSteamLobbyPageDisabled));
        }
    }

    [DataSourceProperty]
    public bool IsRefreshSteamLobbiesDisabled => steamLobbyBrowser == null || IsRefreshingSteamLobbies;

    [DataSourceProperty]
    public bool IsSearchSteamLobbiesDisabled => IsRefreshingSteamLobbies;

    [DataSourceProperty]
    public bool IsSteamLobbyPaginationVisible => SteamLobbyPageCount > 1;

    [DataSourceProperty]
    public bool IsPreviousSteamLobbyPageDisabled => IsRefreshingSteamLobbies || steamLobbyPageIndex == 0;

    [DataSourceProperty]
    public bool IsNextSteamLobbyPageDisabled => IsRefreshingSteamLobbies ||
        steamLobbyPageIndex >= SteamLobbyPageCount - 1;

    [DataSourceProperty]
    public string SteamLobbyStatusText
    {
        get => steamLobbyStatusText;
        private set
        {
            value ??= string.Empty;
            if (steamLobbyStatusText == value) return;

            steamLobbyStatusText = value;
            OnPropertyChanged(nameof(SteamLobbyStatusText));
            OnPropertyChanged(nameof(IsSteamLobbyStatusVisible));
        }
    }

    [DataSourceProperty]
    public bool IsSteamLobbyStatusVisible => !string.IsNullOrEmpty(SteamLobbyStatusText);

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

    public void ActionRefreshSteamLobbies()
    {
        if (disposed || IsRefreshingSteamLobbies) return;

        discoveredSteamLobbies.Clear();
        ClearSteamLobbyDisplay();

        if (steamLobbyBrowser == null)
        {
            SteamLobbyStatusText = "Steam lobby discovery is unavailable.";
            return;
        }

        int generation = ++lobbyRequestGeneration;
        IsRefreshingSteamLobbies = true;
        SteamLobbyStatusText = "Searching for hosted Steam lobbies...";

        try
        {
            steamLobbyBrowser.RequestLobbies(
                (lobbies, error) => CompleteLobbyRefresh(generation, lobbies, error));
        }
        catch (Exception ex)
        {
            CompleteLobbyRefresh(generation, Array.Empty<SteamLobbySummary>(),
                $"Could not search Steam lobbies: {ex.Message}");
        }
    }

    public void ActionSearchSteamLobbies()
    {
        if (disposed || IsRefreshingSteamLobbies) return;

        ApplySteamLobbyHostFilter(resetPage: true);
    }

    public void ActionPreviousSteamLobbyPage()
    {
        if (disposed || IsPreviousSteamLobbyPageDisabled) return;

        steamLobbyPageIndex--;
        ApplySteamLobbyHostFilter(resetPage: false);
    }

    public void ActionNextSteamLobbyPage()
    {
        if (disposed || IsNextSteamLobbyPageDisabled) return;

        steamLobbyPageIndex++;
        ApplySteamLobbyHostFilter(resetPage: false);
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
            bool steamInvites = SessionDiscovery.SteamAvailable && IsLoopbackAddress(connectIP);

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

            messageBroker.Publish(this, new AttemptJoin(ip, port, connectPassword, steamInvites));
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

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        lobbyRequestGeneration++;
        IsRefreshingSteamLobbies = false;
        discoveredSteamLobbies.Clear();
        ClearSteamLobbyDisplay();
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

        if (SelectedTab.Id == SteamLobbiesTabId)
        {
            SteamLobbiesTabActivated?.Invoke();
            ActionRefreshSteamLobbies();
        }
    }

    private void CompleteLobbyRefresh(
        int generation,
        IReadOnlyList<SteamLobbySummary> lobbies,
        string error)
    {
        if (disposed || generation != lobbyRequestGeneration) return;

        IsRefreshingSteamLobbies = false;

        if (!string.IsNullOrWhiteSpace(error))
        {
            ClearSteamLobbyDisplay();
            SteamLobbyStatusText = error;
            return;
        }

        lobbies ??= Array.Empty<SteamLobbySummary>();

        foreach (var lobby in lobbies)
        {
            if (lobby.LobbyId == 0) continue;

            discoveredSteamLobbies.Add(new SteamLobbyListItemVM(
                lobby.LobbyId,
                lobby.OwnerName,
                lobby.ConnectedPlayers,
                lobby.ProtocolVersion,
                lobby.ModVersion,
                lobby.PasswordRequired,
                lobby.IsCompatible,
                RequestSteamLobbyJoin));
        }

        ApplySteamLobbyHostFilter(resetPage: true);
    }

    private void ApplySteamLobbyHostFilter(bool resetPage)
    {
        string searchText = SteamLobbyHostSearchText.Trim();
        var filteredLobbies = discoveredSteamLobbies
            .Where(lobby => searchText.Length == 0 ||
                lobby.HostText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        filteredSteamLobbyCount = filteredLobbies.Count;
        if (resetPage)
        {
            steamLobbyPageIndex = 0;
        }
        else
        {
            steamLobbyPageIndex = Math.Min(steamLobbyPageIndex, Math.Max(0, SteamLobbyPageCount - 1));
        }

        SteamLobbies.Clear();
        foreach (var lobby in filteredLobbies
            .Skip(steamLobbyPageIndex * SteamLobbyPageSize)
            .Take(SteamLobbyPageSize))
        {
            SteamLobbies.Add(lobby);
        }

        NotifySteamLobbyDisplayChanged();

        if (filteredSteamLobbyCount > 0)
        {
            SteamLobbyStatusText = string.Empty;
        }
        else if (discoveredSteamLobbies.Count == 0)
        {
            SteamLobbyStatusText = "No hosted Steam lobbies were found.";
        }
        else
        {
            SteamLobbyStatusText = $"No hosts match '{searchText}'.";
        }
    }

    private void ClearSteamLobbyDisplay()
    {
        filteredSteamLobbyCount = 0;
        steamLobbyPageIndex = 0;
        SteamLobbies.Clear();
        NotifySteamLobbyDisplayChanged();
    }

    private void NotifySteamLobbyDisplayChanged()
    {
        OnPropertyChanged(nameof(SteamLobbiesHeaderText));
        OnPropertyChanged(nameof(SteamLobbyPageText));
        OnPropertyChanged(nameof(CurrentSteamLobbyPage));
        OnPropertyChanged(nameof(SteamLobbyPageCount));
        OnPropertyChanged(nameof(IsSteamLobbyPaginationVisible));
        OnPropertyChanged(nameof(IsPreviousSteamLobbyPageDisabled));
        OnPropertyChanged(nameof(IsNextSteamLobbyPageDisabled));
    }

    private void RequestSteamLobbyJoin(ulong lobbyId)
    {
        if (disposed || lobbyId == 0) return;

        messageBroker.Publish(this, new JoinSteamLobby(lobbyId));
    }

    private static bool IsLoopbackAddress(string address)
    {
        return string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip));
    }
}
