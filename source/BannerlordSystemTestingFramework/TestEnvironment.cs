using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BannerlordSystemTestingLibrary
{
    public class TestEnvironment
    {
        public static readonly Encoding encoding = Encoding.UTF8;
        readonly List<GameInstance> instances = new List<GameInstance>();
        readonly ProtocolRegistry protocolRegistry = new ProtocolRegistry();
        readonly int PORT = 8998;

        // TODO find better way to notify completed registration
        public event Action<GameInstance> OnRegistrationFinished;

        TestEnvironment()
        {
            SocketServer.Instance.StringEncoder = encoding;
            SocketServer.Instance.DelimiterDataReceived += Instance_DelimiterDataReceived;
            SocketServer.Instance.ClientConnected += Instance_ClientConnected;
            SocketServer.Instance.ClientDisconnected += Instance_ClientDisconnected;
            SocketServer.Instance.Start(PORT);
        }

        public TestEnvironment(GameInstance instance) : this(new List<GameInstance>() { instance }) { }

        public TestEnvironment(List<GameInstance> instances) : this()
        {
            foreach (GameInstance instance in instances)
            {
                this.instances.Add(instance);
                instance.Start();
            }
        }

        #region Private
        private void Instance_ClientDisconnected(object sender, System.Net.Sockets.TcpClient e)
        {
            Trace.WriteLine($"Client disconnected: {e.Client.RemoteEndPoint}");
        }

        private void Instance_ClientConnected(object sender, System.Net.Sockets.TcpClient e)
        {
            Trace.WriteLine($"New Connection from: {e.Client.RemoteEndPoint}");
        }

        private void Instance_DelimiterDataReceived(object sender, SimpleTCP.Message e)
        {
            if (e.MessageString.StartsWith("REGISTER "))
            {
                ParseRegister(e);
            }
            else if (e.MessageString.StartsWith("REGISTRATION_COMPLETE")) 
            {
                GameInstance instance = instances.Find((i) =>
                    i.PIDMsg.TcpClient.Client.RemoteEndPoint == e.TcpClient.Client.RemoteEndPoint);
                OnRegistrationFinished?.Invoke(instance);
            }
            else if (e.MessageString.StartsWith("PID "))
            {
                ParsePID(e);
            }
            else
            {
                Trace.WriteLine(e.MessageString);
            }
        }

        #region parsers
        private void ParseRegister(SimpleTCP.Message e)
        {
            try
            {
                protocolRegistry.ParseAndRegisterCommand(e);
                e.ReplyLine($"REGISTERED {e.MessageString.Remove(0, "REGISTER ".Length)}");
            }
            catch (ProtocolRegisterException ex)
            {
                e.ReplyLine($"REGISTER_FAILED {ex.Message}");
            }
        }

        private void ParsePID(SimpleTCP.Message e)
        {
            string PID = e.MessageString.Remove(0, "PID ".Length);
            GameInstance gameInstance;
            if (instances.Where((instance) => instance.PID.ToString() == PID).Count() == 1)
            {
                gameInstance = instances.Where((instance) => instance.PID.ToString() == PID).Single();
            }
            else
            {
                Process gameProcess = Process.GetProcessById(int.Parse(PID));
                gameInstance = new GameInstance(gameProcess);
                instances.Add(gameInstance);
            }
            gameInstance.PIDMsg = e;
        }
        #endregion // Parsers
        #endregion // Private
    }
}
