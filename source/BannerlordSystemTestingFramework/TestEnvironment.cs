using HarmonyLib;
using SuperSocket.SocketBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BannerlordSystemTestingLibrary
{
    public class TestEnvironment : IDisposable
    {
        readonly List<GameInstance> instances = new List<GameInstance>();

        TestEnvironment()
        {
            WebSocketServer.Instance.NewSessionConnected += WsServer_NewSessionConnected;
            WebSocketServer.Instance.NewMessageReceived += WsServer_NewMessageReceived;
            WebSocketServer.Instance.NewDataReceived += WsServer_NewDataReceived;
            WebSocketServer.Instance.SessionClosed += WsServer_SessionClosed;
        }

        

        public TestEnvironment(GameInstance instance) : this()
        {
            instances.Add(instance);
            instance.Start();
        }

        public TestEnvironment(List<GameInstance> instances) : this()
        {
            foreach (GameInstance instance in instances)
            {
                this.instances.Add(instance);
                instance.Start();
            }
            
        }
        public void Dispose()
        {
            foreach(GameInstance instance in instances)
            {
                instance.Dispose();
            }
        }

        #region Private
        private void WsServer_SessionClosed(SuperWebSocket.WebSocketSession session, CloseReason value)
        {
            Trace.WriteLine("Session Closed");
        }

        private void WsServer_NewDataReceived(SuperWebSocket.WebSocketSession session, byte[] value)
        {
            throw new NotImplementedException();
        }

        private void WsServer_NewMessageReceived(SuperWebSocket.WebSocketSession session, string value)
        {
            Trace.WriteLine(value);
        }

        private void WsServer_NewSessionConnected(SuperWebSocket.WebSocketSession session)
        {
            Trace.WriteLine("Session Connected");
        }
        #endregion
    }
}
