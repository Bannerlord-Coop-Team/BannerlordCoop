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

        private static readonly Dictionary<WebSocketSession, string> sessionId = new Dictionary<WebSocketSession, string>();
        private static GameProcess hostProcess;
        private static GameProcess clientProcess;

        static void Main(string[] args)
        {
            WebSocketServer.Instance.NewSessionConnected += WsServer_NewSessionConnected;
            WebSocketServer.Instance.NewMessageReceived += WsServer_NewMessageReceived;
            WebSocketServer.Instance.NewDataReceived += WsServer_NewDataReceived;
            WebSocketServer.Instance.SessionClosed += WsServer_SessionClosed;

            // Some biolerplate to react to close window event
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            hostProcess = new GameProcess(GameType.Host);
            clientProcess = new GameProcess(GameType.Client);

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
                    hostProcess.Kill();
                    clientProcess.Kill();
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
