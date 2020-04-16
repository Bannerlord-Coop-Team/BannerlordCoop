using Coop.Common;
using System;
using Steamworks;

namespace Coop.Network
{
    class NetworkSteam : INetwork
    {
        private const int APP_ID_INT = 261550;
        private static readonly AppId_t APP_ID = new AppId_t(APP_ID_INT);
        public bool IsConnected { get; private set; }

        public bool Connect()
        {
            if (!IsConnected)
            {
                try
                {
                    IsConnected = SteamAPI.Init();
                    if (!IsConnected)
                        Log.Error("Steam API failed to initialize.");
                    else
                        SteamClient.SetWarningMessageHook((severity, text) => Console.WriteLine(text.ToString()));
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }
            return IsConnected;
        }

        public void Disconnect()
        {
            if(IsConnected)
            {
                SteamAPI.Shutdown();
            }
        }

        public NetworkSteam()
        {
            Environment.SetEnvironmentVariable("SteamAppId", APP_ID_INT.ToString());
        }

        ~NetworkSteam()
        {
            Disconnect();
        }
    }
}
