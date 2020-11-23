using HarmonyLib;
using System;

namespace ModTestingUtils
{
    public class TestingUtils
    {
        public TestingUtils()
        {
            if (communicator.Connected)
            {
                communicator.SendData("Hello");
                communicator.OnMessageReceived += Communicator_OnMessageReceived;
            }

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.TestEnv");
            harmony.PatchAll();
        }

        private void Communicator_OnMessageReceived(string obj)
        {
            // TODO add functionality or something
        }

        #region Private
        private static RunnerCommunicator communicator = new RunnerCommunicator();
        #endregion

        #region Singleton
        public static TestingUtils Instance { get; } = new TestingUtils();
        #endregion
    }
}
