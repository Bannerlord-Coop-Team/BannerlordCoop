using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Coop
{
    internal class CoopConnectionVM : ViewModel
    {
        private readonly Action _onCancel;
        private readonly Action<string,int,string> _onConnect;
        private readonly Action<int,string> _onHost;
        private readonly Action _onGithub;
        private readonly Action _onDiscord;

        private string _movieTextHeader;
        private string _ipText;
        private string _portText;
        private string _passwordText;
        private string _joinButtonText;
        private string _hostButtonText;
        private string _communityText;
        private string _githubButtonText;
        private string _discordButtonText;

        private string _ip;
        private string _port;
        private string _password;

        public CoopConnectionVM(Action onCancel,
                                 Action<string,int,string> onConnect,
                                 Action<int,string> onHost,
                                 Action onGithub,
                                 Action onDiscord)
        {
            _onCancel = onCancel;
            _onConnect = onConnect;
            _onHost = onHost;
            _onGithub = onGithub;
            _onDiscord = onDiscord;

            MovieTextHeader = "Coop";
            IpText = "IP";
            PortText = "Port";
            PasswordText = "Mot de passe";
            JoinButtonText = "Se connecter";
            HostButtonText = "Héberger";
            CommunityText = "Communauté";
            GithubButtonText = "Github";
            DiscordButtonText = "Discord";

            Ip = "127.0.0.1";
            Port = "4200";
            Password = "";
        }

        public void ActionCancel()
        {
            _onCancel?.Invoke();
        }

        public void ExecuteActionCancel()
        {
            ActionCancel();
        }

        public void ActionConnect()
        {
            if (int.TryParse(Port, out var p) == false) return;
            _onConnect?.Invoke(Ip, p, Password);
        }

        public void ExecuteActionConnect()
        {
            ActionConnect();
        }

        public void ActionHost()
        {
            if (int.TryParse(Port, out var p) == false) return;
            _onHost?.Invoke(p, Password);
        }

        public void ExecuteActionHost()
        {
            ActionHost();
        }

        public void ActionGithub()
        {
            _onGithub?.Invoke();
        }

        public void ActionDiscord()
        {
            _onDiscord?.Invoke();
        }

        [DataSourceProperty]
        public string MovieTextHeader { get => _movieTextHeader; set { _movieTextHeader = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string IpText { get => _ipText; set { _ipText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string PortText { get => _portText; set { _portText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string PasswordText { get => _passwordText; set { _passwordText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string JoinButtonText { get => _joinButtonText; set { _joinButtonText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string HostButtonText { get => _hostButtonText; set { _hostButtonText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string CommunityText { get => _communityText; set { _communityText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string GithubButtonText { get => _githubButtonText; set { _githubButtonText = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string DiscordButtonText { get => _discordButtonText; set { _discordButtonText = value; OnPropertyChanged(); } }

        [DataSourceProperty]
        public string Ip { get => _ip; set { _ip = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string Port { get => _port; set { _port = value; OnPropertyChanged(); } }
        [DataSourceProperty]
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
    }
}
