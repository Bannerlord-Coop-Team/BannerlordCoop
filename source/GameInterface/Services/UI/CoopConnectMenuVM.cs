using Common.Messaging;
using GameInterface.Services.UI.Messages;
using System.Linq;
using System.Net;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI
{
    internal class CoopConnectMenuVM : ViewModel
    {
        public string JoinButtonText => "Join";
        public string GithubButtonText => "Github";
        public string DiscordButtonText => "Discord";
        public string MovieTextHeader => "Join Co-op Campaign";
        public string CommunityText => "Join the Community";
        public string IpText => "IP-Address:";
        public string PortText => "Port:";
        public string PasswordText => "Password:";

        public string connectIP = "bannerlordcoop.duckdns.org";

        public string connectPort = "4200";

        public string connectPassword = "";

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

        public void ActionConnect()
        {
            int port;

            //Connect to IP
            if (!int.TryParse(connectPort, out port))
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: The port has to be a number"));
                return;
            }

            IPHostEntry hostEntry;

            try
            {
                hostEntry = Dns.GetHostEntry(connectIP);
            }
            catch (System.Exception)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: The connection address could not be resolved"));
                return;
            }
            

            if (hostEntry.AddressList.Length <= 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: The connection address is invalid"));
                return;
            }

            IPAddress ip = hostEntry.AddressList.First();

            MessageBroker.Instance.Publish(this, new AttemptJoin(ip, port));
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
    }
}
