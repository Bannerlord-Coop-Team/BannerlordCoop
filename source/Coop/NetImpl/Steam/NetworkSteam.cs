using System;
using Network.Infrastructure;
using NLog;
using Steamworks;

namespace Coop.NetImpl.Steam
{
    internal class NetworkSteam : INetwork
    {
        private const int APP_ID_INT = 261550;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly AppId_t APP_ID = new AppId_t(APP_ID_INT);

        public NetworkSteam()
        {
            Environment.SetEnvironmentVariable("SteamAppId", APP_ID_INT.ToString());
        }

        public bool IsConnected { get; private set; }

        public bool Connect()
        {
            if (!IsConnected)
            {
                try
                {
                    IsConnected = SteamAPI.Init();
                    if (!IsConnected)
                    {
                        Logger.Error("Steam API failed to Initialize");
                    }
                    else
                    {
                        SteamClient.SetWarningMessageHook(
                            (severity, text) => Console.WriteLine(text.ToString()));
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }

            return IsConnected;
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                SteamAPI.Shutdown();
            }
        }

        ~NetworkSteam()
        {
            Disconnect();
        }
    }
}
