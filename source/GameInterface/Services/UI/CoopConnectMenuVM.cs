using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.UI.Messages;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI
{
    internal class CoopConnectMenuVM : ViewModel
    {
        public string JoinButtonText => "Join";
        public string GithubButtonText => "Github";
        public string DiscordButtonText => "Discord";
        public string PatreonButtonText => "Patreon";
        public string BuyMeACoffeeButtonText => "Buy a Coffee";
        public string MovieTextHeader => "Join Co-op Sandbox";
        public string CommunityText => "Join the Community";
        public string IpText => "Private IP Address:";
        public string PortText => "Port:";
        public string PasswordText => "Password:";
        public string PublicAddressText => "Public IP Address:";

        [DataSourceProperty]
        public HintViewModel PrivateIpHint { get; } = new HintViewModel(new TextObject(
            "Where this game connects. Keep localhost when hosting on this PC; type the host's address to join a friend's server directly."));

        [DataSourceProperty]
        public HintViewModel PublicIpHint { get; } = new HintViewModel(new TextObject(
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
                if (value == publicAddress)
                    return;
                publicAddress = value;
                OnPropertyChanged(nameof(publicAddress));
            }
        }

        [DataSourceProperty]
        public string Ip
        {
            get => connectIP;
            set
            {
                if (value == connectIP)
                    return;
                connectIP = value;
                OnPropertyChanged(nameof(connectIP));
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
                if (value == connectPort)
                    return;
                connectPort = value;
                OnPropertyChanged(nameof(connectPort));
            }
        }

        [DataSourceProperty]
        public string Password
        {
            get => connectPassword;
            set
            {
                if (value == connectPassword)
                    return;
                connectPassword = value;
                OnPropertyChanged(nameof(connectPassword));
            }
        }

        private static bool IsLoopbackAddress(string address)
        {
            return string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
                (IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip));
        }

        public void ActionConnect()
        {
            if (!int.TryParse(connectPort, out var port) || port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: The connection port is invalid"));
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

                MessageBroker.Instance.Publish(this, new AttemptJoin(ip, port, steamInvites, publicAddress?.Trim()));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"ERROR: The connection address could not be resolved: {ex.Message}"));
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
    }
}
