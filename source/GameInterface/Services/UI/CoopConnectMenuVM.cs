using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.Donate;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Serilog;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;
/// <summary>
/// Available password-status filters for hosted steam lobbies
/// </summary>
public enum SteamLobbyPasswordFilter
{
    Any,
    NoPassword,
    PasswordRequired,
}

/// <summary>View model for direct connection and public standalone Steam-lobby discovery.</summary>
public class CoopConnectMenuVM : ViewModel, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopConnectMenuVM>();

    public const string DirectTabId = "direct";
    public const string SteamLobbiesTabId = "steam_lobbies";
    public const string OptionsTabId = "Connection";
    public const string OptionsSectionId = "DirectConnection";
    public const int SteamLobbyPageSize = 4;

    private const string DefaultServerAddress = "localhost";
    private const int DefaultConnectionPort = 4200;

    public event Action SteamLobbiesTabActivated;

    private readonly ISteamLobbyBrowser steamLobbyBrowser;
    private readonly IMessageBroker messageBroker;
    private readonly ICoopOptionsStore optionsStore;
    private readonly List<SteamLobbyListItemVM> discoveredSteamLobbies = new();

    private CoopConnectionTabVM selectedTab;
    private string steamLobbyHostSearchText = string.Empty;
    private SteamLobbyPasswordFilter steamLobbyPasswordFilter;
    private int minimumSteamLobbyPlayers;
    private string steamLobbyStatusText = string.Empty;
    private bool isRefreshingSteamLobbies;
    private bool disposed;
    private int lobbyRequestGeneration;
    private int filteredSteamLobbyCount;
    private long filteredSteamLobbyPlayerCount;
    private int steamLobbyPageIndex;

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
    public string SteamLobbiesHeaderText =>
        $"Hosted Steam Servers ({filteredSteamLobbyCount} servers; " +
        $"{filteredSteamLobbyPlayerCount} players)";
    public string SteamLobbyPageText => $"Page {CurrentSteamLobbyPage} of {SteamLobbyPageCount}";
    public int CurrentSteamLobbyPage => filteredSteamLobbyCount == 0 ? 0 : steamLobbyPageIndex + 1;
    public int SteamLobbyPageCount => filteredSteamLobbyCount == 0
        ? 0
        : ((filteredSteamLobbyCount - 1) / SteamLobbyPageSize) + 1;
    public string HostSearchLabelText => "Host Name";
    public string HostSearchPlaceholderText => "Type a host name...";
    public string PasswordFilterLabelText => "Password";
    public string MinimumPlayersFilterLabelText => "Minimum Players";
    public string HostColumnText => "Host Name";
    public string ConnectedPlayersColumnText => "Connected Players";
    public string PasswordColumnText => "Access";
    public string CompatibilityColumnText => "Status";
    public string IpText => "Server Address:";
    public string PasswordText => "Password:";

    [DataSourceProperty]
    public HintViewModel ServerAddressHint { get; } = new HintViewModel(new TextObject(
        "The co-op server's IP address or host name. Add a custom port after a colon, such as localhost:4300. Port 4200 is used when omitted."));

    [DataSourceProperty]
    public HintViewModel PasswordHint { get; } = new HintViewModel(new TextObject(
        "The session password set by the host. Leave empty if the host has not set one."));

    [DataSourceProperty]
    public string PasswordFilterButtonText => SteamLobbyPasswordFilter switch
    {
        SteamLobbyPasswordFilter.NoPassword => "No Password",
        SteamLobbyPasswordFilter.PasswordRequired => "Password Required",
        _ => "Any Password",
    };

    public string connectIP = DefaultServerAddress;
    public string connectPassword = "";

    public CoopConnectMenuVM()
        : this(SessionDiscovery.SteamLobbyBrowser, MessageBroker.Instance, new CoopOptionsStore())
    {
    }

    public CoopConnectMenuVM(ISteamLobbyBrowser steamLobbyBrowser, IMessageBroker messageBroker)
        : this(steamLobbyBrowser, messageBroker, new CoopOptionsStore())
    {
    }

    public CoopConnectMenuVM(
        ISteamLobbyBrowser steamLobbyBrowser,
        IMessageBroker messageBroker,
        ICoopOptionsStore optionsStore)
    {
        this.steamLobbyBrowser = steamLobbyBrowser;
        this.messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        connectIP = LoadLastServerAddress();

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
    public SteamLobbyPasswordFilter SteamLobbyPasswordFilter
    {
        get => steamLobbyPasswordFilter;
        private set
        {
            if (steamLobbyPasswordFilter == value) return;
            steamLobbyPasswordFilter = value;
            OnPropertyChanged(nameof(SteamLobbyPasswordFilter));
            OnPropertyChanged(nameof(PasswordFilterButtonText));

            if (!disposed && !IsRefreshingSteamLobbies)
            {
                ApplySteamLobbyHostFilter(resetPage: true);
            }
        }
    }
    [DataSourceProperty]
    public int MinimumSteamLobbyPlayers
    {
        get => minimumSteamLobbyPlayers;
        set
        {
            value = Math.Max(0, value);
            if (minimumSteamLobbyPlayers == value) return;

            minimumSteamLobbyPlayers = value;
            OnPropertyChanged(nameof(MinimumSteamLobbyPlayers));

            if (!disposed && !IsRefreshingSteamLobbies)
            {
                ApplySteamLobbyHostFilter(resetPage: true);
            }
        }
    }
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

    public void ActionCycleSteamLobbyPasswordFilter()
    {
        if (disposed || IsRefreshingSteamLobbies) return;

        SteamLobbyPasswordFilter = SteamLobbyPasswordFilter switch
        {
            SteamLobbyPasswordFilter.Any => SteamLobbyPasswordFilter.NoPassword,
            SteamLobbyPasswordFilter.NoPassword => SteamLobbyPasswordFilter.PasswordRequired,
            _ => SteamLobbyPasswordFilter.Any,
        };
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
        if (!TryParseServerAddress(connectIP, out var host, out var port))
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "ERROR: Enter a valid server address with an optional port"));
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
            bool steamInvites = SessionDiscovery.SteamAvailable && IsLoopbackAddress(host);

            IPAddress ip;

            if (IPAddress.TryParse(host, out var enteredIp))
            {
                ip = enteredIp;
            }
            else
            {
                var addresses = Dns.GetHostAddresses(host);
                ip = addresses.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

                if (ip == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage("ERROR: No IPv4 address found for host"));
                    return;
                }
            }

            messageBroker.Publish(this, new AttemptJoin(ip, port, connectPassword, steamInvites));
            SaveLastServerAddress();
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
            .Where(MatchesSteamLobbyPasswordFilter)
            .Where(lobby => lobby.ConnectedPlayers >= MinimumSteamLobbyPlayers)
            .ToList();

        filteredSteamLobbyCount = filteredLobbies.Count;
        filteredSteamLobbyPlayerCount = filteredLobbies.Sum(
            lobby => (long)lobby.ConnectedPlayers);
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
            SteamLobbyStatusText = "No hosted Steam lobbies match the current filters.";
        }
    }

    private bool MatchesSteamLobbyPasswordFilter(SteamLobbyListItemVM lobby)
    {
        return SteamLobbyPasswordFilter switch
        {
            SteamLobbyPasswordFilter.NoPassword => !lobby.PasswordRequired,
            SteamLobbyPasswordFilter.PasswordRequired => lobby.PasswordRequired,
            _ => true,
        };
    }

    private void ClearSteamLobbyDisplay()
    {
        filteredSteamLobbyCount = 0;
        filteredSteamLobbyPlayerCount = 0;
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

    private string LoadLastServerAddress()
    {
        try
        {
            var options = optionsStore.LoadOrDefault();
            if (options.TryGetSection(
                    OptionsTabId,
                    OptionsSectionId,
                    out DirectConnectionOptions saved) &&
                TryParseServerAddress(saved.LastServerAddress, out _, out _))
            {
                return saved.LastServerAddress.Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Last direct connection address could not be loaded");
        }

        return DefaultServerAddress;
    }

    private void SaveLastServerAddress()
    {
        try
        {
            var options = optionsStore.LoadOrDefault();
            options.SetSection(
                OptionsTabId,
                OptionsSectionId,
                new DirectConnectionOptions { LastServerAddress = connectIP.Trim() });
            optionsStore.Save(options);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Last direct connection address could not be saved");
        }
    }

    internal static bool TryParseServerAddress(string enteredAddress, out string host, out int port)
    {
        host = string.Empty;
        port = DefaultConnectionPort;

        if (string.IsNullOrWhiteSpace(enteredAddress)) return false;

        string address = enteredAddress.Trim();
        if (address[0] == '[')
        {
            int closingBracket = address.IndexOf(']');
            if (closingBracket <= 1) return false;

            host = address.Substring(1, closingBracket - 1);
            string suffix = address.Substring(closingBracket + 1);
            if (suffix.Length == 0) return true;

            return suffix[0] == ':' && TryParsePort(suffix.Substring(1), out port);
        }

        int firstColon = address.IndexOf(':');
        int lastColon = address.LastIndexOf(':');
        if (firstColon >= 0 && firstColon == lastColon)
        {
            host = address.Substring(0, firstColon).Trim();
            return host.Length > 0 && TryParsePort(address.Substring(firstColon + 1), out port);
        }

        if (firstColon >= 0 && !IPAddress.TryParse(address, out _)) return false;

        host = address;
        return true;
    }

    private static bool TryParsePort(string enteredPort, out int port)
    {
        return int.TryParse(enteredPort, out port) &&
            port > IPEndPoint.MinPort && port <= IPEndPoint.MaxPort;
    }

    private static bool IsLoopbackAddress(string address)
    {
        return string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip));
    }
}

/// <summary>Persisted address from the last direct connection attempt.</summary>
public class DirectConnectionOptions
{
    [JsonPropertyName("lastServerAddress")]
    public string LastServerAddress { get; set; }
}
