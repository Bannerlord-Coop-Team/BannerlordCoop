using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
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

    private readonly ISteamLobbyBrowser steamLobbyBrowser;
    private readonly IMessageBroker messageBroker;
    private readonly List<SteamLobbyListItemVM> discoveredSteamLobbies = new();

    private CoopConnectionTabVM selectedTab;
    private string steamLobbyHostSearchText = string.Empty;
    private string steamLobbyStatusText = string.Empty;
    private bool isRefreshingSteamLobbies;
    private bool disposed;
    private int lobbyRequestGeneration;

    public string JoinButtonText => "Join";
    public string RefreshButtonText => "Refresh";
    public string SearchButtonText => "Search";
    public string GithubButtonText => "Github";
    public string DiscordButtonText => "Discord";
    public string PatreonButtonText => "Patreon";
    public string BuyMeACoffeeButtonText => "Buy a Coffee";
    public string MovieTextHeader => "Join Co-op Sandbox";
    public string CommunityText => "Join the Community";
    public string SteamLobbiesHeaderText => $"Hosted Steam Servers ({SteamLobbies.Count})";
    public string HostSearchLabelText => "Hostname:";
    public string HostColumnText => "Host";
    public string PasswordColumnText => "Access";
    public string CompatibilityColumnText => "Status";
    public string IpText => "Server IP Address:";
    public string PortText => "Port:";
    public string PasswordText => "Password:";
    public string PublicAddressText => "IP Address for Friends:";

    [DataSourceProperty]
    public HintViewModel ServerAddressHint { get; } = new HintViewModel(new TextObject(
        "The address of the co-op server to join. Keep localhost if you are the host; otherwise, type the address your friend shared to join their game."));

    [DataSourceProperty]
    public HintViewModel FriendsAddressHint { get; } = new HintViewModel(new TextObject(
        "When you're hosting, this is the address friends use to reach your session over the internet. Search 'what is my IP' to find it, and forward UDP ports 4200-4201 on your router. Friends on your own network can use your LAN address instead: run ipconfig in command prompt and share the 'IPv4 Address' with your friends."));

    [DataSourceProperty]
    public HintViewModel PortHint { get; } = new HintViewModel(new TextObject(
        "The port the co-op server listens on. Leave 4200 unless the host changed it."));

    [DataSourceProperty]
    public HintViewModel PasswordHint { get; } = new HintViewModel(new TextObject(
        "The session password set by the host. Leave empty if the host has not set one."));

    public string connectIP = "localhost";
    public string connectPort = "4200";
    public string connectPassword = "";
    public string publicAddress = "";

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
        }
    }

    [DataSourceProperty]
    public bool IsRefreshSteamLobbiesDisabled => steamLobbyBrowser == null || IsRefreshingSteamLobbies;

    [DataSourceProperty]
    public bool IsSearchSteamLobbiesDisabled => IsRefreshingSteamLobbies;

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

    // Connecting to your own local server is hosting, so the public address to advertise
    // to Steam friends is asked for exactly then; any other address is a direct join.
    [DataSourceProperty]
    public bool PublicAddressVisible => SessionDiscovery.SteamAvailable && IsLoopbackAddress(connectIP);

    [DataSourceProperty]
    public string PublicAddress
    {
        get => publicAddress;
        set
        {
            if (value == publicAddress) return;

            publicAddress = value;
            OnPropertyChanged(nameof(PublicAddress));
        }
    }

    [DataSourceProperty]
    public string Ip
    {
        get => connectIP;
        set
        {
            if (value == connectIP) return;

            connectIP = value;
            OnPropertyChanged(nameof(Ip));
            OnPropertyChanged(nameof(PublicAddressVisible));
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
        SteamLobbies.Clear();
        OnPropertyChanged(nameof(SteamLobbiesHeaderText));

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

        ApplySteamLobbyHostFilter();
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
            // Advertise exactly when the screen offered the public address field.
            bool steamInvites = PublicAddressVisible;

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

            messageBroker.Publish(this, new AttemptJoin(
                ip, port, connectPassword, steamInvites, publicAddress?.Trim()));
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

    public void ActionGithub()
    {
        System.Diagnostics.Process.Start("https://github.com/Bannerlord-Coop-Team/BannerlordCoop");
    }

    public void ActionDiscord()
    {
        System.Diagnostics.Process.Start("https://discord.gg/ngC4RVb");
    }

    public void ActionPatreon()
    {
        System.Diagnostics.Process.Start("https://www.patreon.com/c/bannerlordcoop");
    }

    public void ActionBuyMeACoffee()
    {
        System.Diagnostics.Process.Start("https://buymeacoffee.com/bannerlordcoop");
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        lobbyRequestGeneration++;
        IsRefreshingSteamLobbies = false;
        discoveredSteamLobbies.Clear();
        SteamLobbies.Clear();
        OnPropertyChanged(nameof(SteamLobbiesHeaderText));
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
        SteamLobbies.Clear();
        OnPropertyChanged(nameof(SteamLobbiesHeaderText));

        if (!string.IsNullOrWhiteSpace(error))
        {
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
                lobby.ProtocolVersion,
                lobby.ModVersion,
                lobby.PasswordRequired,
                lobby.IsCompatible,
                RequestSteamLobbyJoin));
        }

        ApplySteamLobbyHostFilter();
    }

    private void ApplySteamLobbyHostFilter()
    {
        SteamLobbies.Clear();

        string searchText = SteamLobbyHostSearchText.Trim();
        foreach (var lobby in discoveredSteamLobbies)
        {
            if (searchText.Length == 0 ||
                lobby.HostText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SteamLobbies.Add(lobby);
            }
        }

        OnPropertyChanged(nameof(SteamLobbiesHeaderText));

        if (SteamLobbies.Count > 0)
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
