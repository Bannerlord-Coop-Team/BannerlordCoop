using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestRunner
{
    class Program
    {
        

        private readonly Dictionary<WebSocketSession, string> sessionId = new Dictionary<WebSocketSession, string>();
        private static GameProcess hostProcess;
        private static GameProcess clientProcess;
        static void Main(string[] args)
        {
            WebSocketServer.Instance.NewSessionConnected += WsServer_NewSessionConnected;
            WebSocketServer.Instance.NewMessageReceived += WsServer_NewMessageReceived;
            WebSocketServer.Instance.NewDataReceived += WsServer_NewDataReceived;
            WebSocketServer.Instance.SessionClosed += WsServer_SessionClosed;

            hostProcess = new GameProcess(GameType.Host);
            clientProcess = new GameProcess(GameType.Client);

            Console.WriteLine("Server is running.");
            
            Console.ReadKey();
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
    }
}
