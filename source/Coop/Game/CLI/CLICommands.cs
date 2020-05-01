using System;
using System.Collections.Generic;
using System.Net;
using Coop.Network;
using TaleWorlds.Library;

namespace Coop.Game.CLI
{
    public static class CLICommands
    {
        private const string sGroupName = "coop";

        [CommandLineFunctionality.CommandLineArgumentFunction("dump", sGroupName)]
        public static string DumpInfo(List<string> parameters)
        {
            string sMessage = "";
            sMessage += CoopServer.Instance + Environment.NewLine;
            sMessage += CoopClient.Instance + Environment.NewLine;
            return sMessage;
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("start_local_server", sGroupName)]
        public static string StartServer(List<string> parameters)
        {
            CoopServer.Instance.StartServer();
            ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
            CoopClient.Instance.Connect(config.LanAddress, config.LanPort);
            return CoopServer.Instance.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("connect_to", sGroupName)]
        public static string ConnectTo(List<string> parameters)
        {
            IPAddress ip;
            int iPort;
            if (parameters.Count != 2 ||
                !IPAddress.TryParse(parameters[0], out ip) ||
                int.TryParse(parameters[1], out iPort))
            {
                return $"Usage: \"{sGroupName}.ConnectTo [IP] [Port]\"." +
                       Environment.NewLine +
                       "\tExample: \"{sGroupName}.ConnectTo [IP] [Port]\".";
            }

            CoopClient.Instance.Connect(ip, iPort);
            return "Client connection request sent.";
        }
    }
}
