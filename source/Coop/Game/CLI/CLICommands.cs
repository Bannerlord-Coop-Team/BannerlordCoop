using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Coop.Game.Patch;
using Coop.Network;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.CLI
{
    public static class CLICommands
    {
        private const string sGroupName = "coop";

        [CommandLineFunctionality.CommandLineArgumentFunction("info", sGroupName)]
        public static string DumpInfo(List<string> parameters)
        {
            string sMessage = "";
            sMessage += CoopServer.Instance + Environment.NewLine;
            sMessage += Environment.NewLine + "*** Client ***" + Environment.NewLine;
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
            if (parameters.Count != 2 ||
                !IPAddress.TryParse(parameters[0], out IPAddress ip) ||
                !int.TryParse(parameters[1], out int iPort))
            {
                return $"Usage: \"{sGroupName}.connect_to [IP] [Port]\"." +
                       Environment.NewLine +
                       $"\tExample: \"{sGroupName}.connect_to 127.0.0.1 4201\".";
            }

            CoopClient.Instance.Connect(ip, iPort);
            return "Client connection request sent.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("set_time_control", sGroupName)]
        public static string SetTimeControl(List<string> parameters)
        {
            IEnumerable<CampaignTimeControlMode> possibleValues =
                Enum.GetValues(typeof(CampaignTimeControlMode)).Cast<CampaignTimeControlMode>();
            string options = string.Join(
                Environment.NewLine,
                possibleValues.Select((e, index) => $"[{index}]: {e}"));
            string sUsage = $"Usage: \"{sGroupName}.set_time_control [value]\". Valid [value]:" +
                            Environment.NewLine +
                            options +
                            Environment.NewLine +
                            $"\tExample: \"{sGroupName}.set_time_control 2\".";

            if (parameters.Count != 1 || !int.TryParse(parameters[0], out int iValue))
            {
                return $"Usage: \"{sGroupName}.set_time_control [value]\". Valid [value] are:" +
                       Environment.NewLine +
                       options +
                       Environment.NewLine +
                       $"\tExample: \"{sGroupName}.set_time_control 2\".";
            }

            if (!Enum.IsDefined(typeof(CampaignTimeControlMode), iValue))
            {
                return $"Invalid value: {iValue}. Valid values:" + Environment.NewLine + options;
            }

            TimeControl.SetForced_Campaign_TimeControlMode((CampaignTimeControlMode) iValue);
            return "Success.";
        }
    }
}
