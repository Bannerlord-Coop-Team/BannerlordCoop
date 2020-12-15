using HarmonyLib;
using SimpleTCP;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace ModTestingFramework
{
    public class TestingFramework
    {
        public TestingFramework()
        {
            if (communicator.Connected)
            {
                communicator.SendData($"PID {Process.GetCurrentProcess().Id}");
                communicator.OnMessageReceived += Communicator_OnMessageReceived;
                CommandRegistry.RegisterAllCommands();
            }

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.TestEnv");
            harmony.PatchAll();
        }

        public void SendMessage(string message)
        {
            if (communicator.Connected)
            {
                communicator.SendData(message);
            }
        }

        private void Communicator_OnMessageReceived(object sender, Message msg)
        {
            if(msg.MessageString.StartsWith("COMMAND "))
            {
                ParseCommandAndActivate(msg.MessageString);
            }
        }

        private void ParseCommandAndActivate(string msg)
        {
            // COMMAND StartGame [args]
            string formattedMsg = msg.Remove(0, "COMMAND ".Length);
            string[] splitArray = formattedMsg.Split(' ');
            string[] args = splitArray.Skip(1).ToArray();
            string command = splitArray.First();

            if (!CommandRegistry.commands.ContainsKey(command)) 
            {
                throw new Exception("Command does not exist in command registry.");
            }

            MethodInfo methodInfo = CommandRegistry.commands[command];

            if(methodInfo.GetParameters().Length == 0)
            {
                methodInfo.Invoke(null, null);
            }
            else if(methodInfo.GetParameters().Length == 1 &&
                methodInfo.GetParameters()[0].ParameterType == typeof(string[]))
            {
                methodInfo.Invoke(null, new object[] { args });
            }
            else
            {
                throw new Exception($"{methodInfo.Name} does not meet generic requirements.");
            }
        }

        #region Private
        private static RunnerCommunicator communicator = RunnerCommunicator.Instance;
        #endregion

        #region Singleton
        public static TestingFramework Instance { get; } = new TestingFramework();
        #endregion
    }
}
