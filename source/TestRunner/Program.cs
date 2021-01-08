using BannerlordSystemTestingLibrary;
using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestRunner
{
    /// <summary>
    /// Runner that works similar to running a test but as an application
    /// </summary>
    static class TestRunner
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        static GameInstance host = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        static GameInstance client = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");

        static List<GameInstance> instances = new List<GameInstance>
            {
                host,
                client,
            };

        static TestEnvironment environment;

        static void Main(string[] args)
        {
            environment = new TestEnvironment(instances);

            // Some biolerplate to react to close window event
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);
            

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            environment.OnRegistrationFinished += (instance) =>
            {
                instance.OnGameStateChanged += (state) => {
                    if (state == GameStates.MainMenuReadyState)
                    {
                        instance.SendCommand("StartCoop");
                    }
                    else if(state == GameStates.UnspecifiedDedicatedServerState)
                    {
                        tcs.SetResult(true);
                    }
                    
                    else if (!typeof(GameStates)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(string))
                     .ToDictionary(f => f.Name,
                            f => (string)f.GetValue(null)).Values.Contains(state)) {
                        throw new Exception($"{state} not in GameStates");
                    };
                };

                instance.OnCommandResponse += (response) =>
                {
                    Console.WriteLine(response);
                };
            };


            Console.WriteLine("Server is running.");
            
            Console.ReadKey();
        }

        #region Private

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        /// <summary>
        /// Handler for command window events
        /// </summary>
        /// <param name="sig">Control signal</param>
        /// <returns>True if signal is valid else false</returns>
        private static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    foreach(GameInstance instance in instances)
                    {
                        instance.Stop();
                    }
                    return true;
                default:
                    return false;
            }
        }
        #endregion
    }
}
