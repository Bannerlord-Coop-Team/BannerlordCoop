using BannerlordSystemTestingLibrary;
using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestRunner
{
    static class TestRunner
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        static GameInstance host = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        static GameInstance client = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        //static GameInstance client2 = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");

        static List<GameInstance> instances = new List<GameInstance>
            {
                //host,
                //client,
            };

        static TestEnvironment environment;

        static void Main(string[] args)
        {
            environment = new TestEnvironment(instances);

            //Console.WriteLine("PID " + host.PID.ToString());

            // Some biolerplate to react to close window event
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            //host.Attach().Wait();

            //Console.WriteLine("Attached.");

            //host.SendCommand("StartGame");

            environment.OnRegistrationFinished += (instance) =>
            {
                instance.SendCommand("ExitGame");
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

        private static void WsServer_SessionClosed(WebSocketSession session, SuperSocket.SocketBase.CloseReason value)
        {
            Console.WriteLine("Session Closed");
        }

        private static void WsServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
            throw new NotImplementedException();
        }

        private static void WsServer_NewMessageReceived(WebSocketSession session, string value)
        {
            Console.WriteLine(value);
        }

        private static void WsServer_NewSessionConnected(WebSocketSession session)
        {
            Console.WriteLine("Session Connected");
        }
        #endregion
    }
}
