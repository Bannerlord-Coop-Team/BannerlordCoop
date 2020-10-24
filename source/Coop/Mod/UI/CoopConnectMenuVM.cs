using Network.Infrastructure;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Missions;

namespace Coop.Mod.UI
{
    internal class CoopConnectMenuVM : ViewModel
    {
        public string JoinButtonText => "Join";
        public string CancelButtonText => "Cancel";
        public string GithubButtonText => "Github";
        public string DiscordButtonText => "Discord";
        public string MovieTextHeader => "Join Co-op Campaign";
        public string CommunityText => "Join the Community";
        public string IpText => "IP-Address:";
        public string PortText => "Port:";
        public string PasswordText => "Password:";

        public string connectIP = new ServerConfiguration().NetworkConfiguration.LanAddress.ToString();

        public string connectPort = new ServerConfiguration().NetworkConfiguration.LanPort.ToString();

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
            System.Net.IPAddress ip;

            //Connect to IP
            if (!int.TryParse(connectPort, out port))
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: The port has to be a number"));
                return;
            }

            if (!System.Net.IPAddress.TryParse(connectIP, out ip))
            {
                InformationManager.DisplayMessage(new InformationMessage("ERROR: The ip is not formatted correctly"));
                return;
            }

            InformationManager.DisplayMessage( new InformationMessage("Trying to connect to "+ ip.ToString() + ":" + port.ToString()));

            CoopClient.Instance.Connect(
            ip,
            port);

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
