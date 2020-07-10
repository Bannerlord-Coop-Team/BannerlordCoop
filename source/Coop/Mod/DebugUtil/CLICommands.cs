using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using HarmonyLib;
using Network.Infrastructure;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod.DebugUtil
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

        private static DebugUI m_DebugUI;

        [CommandLineFunctionality.CommandLineArgumentFunction("show_debug_ui", sGroupName)]
        public static string ShowDebugUi(List<string> parameters)
        {
            if (m_DebugUI == null)
            {
                m_DebugUI = new DebugUI();
                Main.Instance.Updateables.Add(m_DebugUI);
            }
            m_DebugUI.Visible = true;
            return "Done.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("start_local_server", sGroupName)]
        public static string StartServer(List<string> parameters)
        {
            if (CoopServer.Instance.StartServer() == null)
            {
                ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
                CoopClient.Instance.Connect(config.LanAddress, config.LanPort);
                return CoopServer.Instance.ToString();
            }
            return "Server started already.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("stop_local_server", sGroupName)]
        public static string StopServer(List<string> parameters)
        {
            if (CoopServer.Instance.Current != null)
            {
                CoopServer.Instance.ShutDownServer();
                return "Done.";
            }
            return "Server not started.";
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

        [CommandLineFunctionality.CommandLineArgumentFunction("disconnect", sGroupName)]
        public static string Disconnect(List<string> parameters)
        {
            if (CoopClient.Instance.Connected)
            {
                CoopClient.Instance.Disconnect();
                return "Client disconnection request sent.";
            }

            return "Client not connected.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("random_seed", sGroupName)]
        public static string RandomSeed(List<string> parameters)
        {
            if (Game.Current != null)
            {
                FieldInfo fieldInfo = AccessTools.Field(typeof(Game), "_randomSeed");
                var seed = fieldInfo.GetValue(Game.Current);

                return $"Your random seed is '{seed}'.";
            }

            return "Your campaign game not started.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("help", sGroupName)]
        public static string Help(List<string> parameters)
        {
            return "Coop commands:\n" +
                "\tcoop.record <filename>\tStart record movements of all parties.\n" +
                "\tcoop.play <filename>\tPlayback recorded movements of main hero party.\n" +
                "\tcoop.stop\t\tStop record or playback.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("record", sGroupName)]
        public static string Record(List<string> parameters)
        {
            if (parameters.Count < 1)
                return Help(null);
            return Replay.StartRecord(parameters[0]);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("play", sGroupName)]
        public static string Play(List<string> parameters)
        {
            if (parameters.Count < 1)
                return Help(null);
            return Replay.Playback(parameters[0]);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("stop", sGroupName)]
        public static string Stop(List<string> parameters)
        {
            return Replay.Stop();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("disable_inconsistent_state_warnings", sGroupName)]
        public static string DisableWarn(List<string> parameters)
        {
            var help = "Disable(1) or enable(0) to show warnings about inconsistent internal state\n" +
                    $"Usage:\n" +
                    $"\t{sGroupName}.disable_inconsistent_state_warnings 1";
            if (parameters.Count < 1)
                return help;

            var entityManager = CoopServer.Instance?.Persistence?.EntityManager;
            if (entityManager == null)
                return "Server not started.";

            if (parameters[0] == "1")
            {
                entityManager.SuppressInconsistentStateWarnings = true;
                return "Inconsistent state warnings disabled.";
            }
            else if (parameters[0] == "0")
            {
                entityManager.SuppressInconsistentStateWarnings = false;
                return "Inconsistent state warnings enabled.";
            }

            return help;
        }
    }
}
