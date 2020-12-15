using SimpleTCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ModTestingFramework
{
    class RunnerCommunicator
    {
        private readonly int PORT = 8998;
        private readonly Encoding encoding = Encoding.UTF8;

        public readonly TimeSpan ACK_TIMEOUT = TimeSpan.FromSeconds(5);

        public delegate void MessageReceivedDelegate(object sender, Message message);

        public event MessageReceivedDelegate OnMessageReceived;
        public event Action<string> OnMessageSent;

        public bool Connected => SocketClient.TcpClient.Connected;

        private static RunnerCommunicator instance;

        public static RunnerCommunicator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RunnerCommunicator();
                }

                return instance;
            }
        }

        public RunnerCommunicator()
        {
            // Enforce Singleton
            if(instance != null)
            {
                throw new Exception($"Use {GetType().Name}.Instance instead of creating new object.");
            }

            try
            {
                SocketClient.StringEncoder = encoding;
                SocketClient.DelimiterDataReceived += SocketClient_DelimiterDataReceived;
                SocketClient.Connect("127.0.0.1", PORT);
            }
            catch (SocketException) { }
        }

        private void SocketClient_DelimiterDataReceived(object sender, Message e)
        {
            OnMessageReceived?.Invoke(sender, e);
        }

        internal void SendData(string data)
        {
            SocketClient.WriteLine(data);
        }

        #region Private
        private readonly static SimpleTcpClient SocketClient = new SimpleTcpClient();
        #endregion

    }
}
