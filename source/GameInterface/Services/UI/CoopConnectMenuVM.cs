using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.UI.Messages;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TaleWorlds.Library;
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
        public string IpText => "IP-Address:";
        public string PortText => "Port:";
        public string PasswordText => "Password:";
        public string PublicAddressText => "Public Address:";

        public string connectIP = "localhost";

        public string connectPort = "4200";

        public string connectPassword = "";

        // Only takes effect for loopback connects (the host's own client), so defaulting on is safe.
        public bool enableSteamInvites = SessionDiscovery.SteamAvailable;

        public string publicAddress = "";

        [DataSourceProperty]
        public bool SteamAvailable => SessionDiscovery.SteamAvailable;

        [DataSourceProperty]
        public string SteamInvitesButtonText => $"Steam Invites: {(enableSteamInvites ? "On" : "Off")}";

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

        public void ActionToggleSteamInvites()
        {
            enableSteamInvites = !enableSteamInvites;
            OnPropertyChanged(nameof(SteamInvitesButtonText));
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

                // Only the host's own client (loopback connect) may advertise the session it hosts.
                bool steamInvites = enableSteamInvites && SessionDiscovery.SteamAvailable && IPAddress.IsLoopback(ip);

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
