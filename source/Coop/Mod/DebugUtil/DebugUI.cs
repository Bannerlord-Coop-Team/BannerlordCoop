using System;
using System.Collections.Generic;
using System.Diagnostics;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Network.Infrastructure;
using NLog;
using RailgunNet.Connection;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using Sync;
using TaleWorlds.Engine;

namespace Coop.Mod.DebugUtil
{
    public class DebugUI : IUpdateable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string m_WindowTitle = "Debug UI";

        public bool Visible { get; set; }

        public void Update(TimeSpan frameTime)
        {
            if (Visible)
            {
                Begin();
                AddButtons();
                DisplayConnectionInfo();
                DisplayMethodRegistry();
                DisplayPersistenceMenu();
                End();
            }
        }

        private static void DisplayPersistenceMenu()
        {
            if (!Imgui.TreeNode("Persistence"))
            {
                return;
            }

            DisplayPersistenceInfo();
            DisplayClientRpcInfo();
            DisplayEntities();

            Imgui.TreePop();
        }

        private static void DisplayPersistenceInfo()
        {
            List<SPeer> peers = new List<SPeer>();
            if (CoopClient.Instance.Persistence != null)
            {
                RailClientPeer peer = CoopClient.Instance.Persistence.Peer;
                if (peer != null)
                {
                    SPeer peerInfo = new SPeer();
                    peerInfo.Peer = peer;
                    peerInfo.Type = SPeer.EType.ClientSide;
                    peers.Add(peerInfo);
                }
            }

            if (CoopServer.Instance.Persistence != null)
            {
                foreach (RailServerPeer peer in CoopServer.Instance.Persistence.ConnectedClients)
                {
                    SPeer peerInfo = new SPeer();
                    peerInfo.Peer = peer;
                    peerInfo.Type = SPeer.EType.ServerSide;
                    peers.Add(peerInfo);
                }
            }

            Imgui.Columns(5);
            Imgui.Text("Type");
            peers.ForEach(p => Imgui.Text(p.Type.ToString()));

            Imgui.NextColumn();
            Imgui.Text("Local tick");
            peers.ForEach(p => Imgui.Text(p.Peer.LocalTick.ToString()));

            Imgui.NextColumn();
            Imgui.Text("Latest remote tick");
            peers.ForEach(p => Imgui.Text(p.Peer.RemoteClock.LatestRemote.ToString()));

            Imgui.NextColumn();
            Imgui.Text("Estimated remote tick");
            peers.ForEach(p => Imgui.Text(p.Peer.RemoteClock.EstimatedRemote.ToString()));

            Imgui.NextColumn();
            Imgui.Text("LatestRemote - EstimatedRemote");
            Imgui.Separator();
            peers.ForEach(p => Imgui.Text($"{p.Slack}"));

            Imgui.Columns();
        }

        private static void DisplayMethodRegistry()
        {
            if (!Imgui.TreeNode("Patched method registry"))
            {
                return;
            }

            foreach (KeyValuePair<MethodId, MethodAccess> registrar in MethodRegistry.IdToMethod)
            {
                MethodAccess access = registrar.Value;
                string sName = $"{registrar.Key} {access}";
                if (!Imgui.TreeNode(sName))
                {
                    continue;
                }

#if DEBUG
                Imgui.Columns(2);
                Imgui.Separator();
                Imgui.Text("Instance");

                // first line: global handler
                Imgui.Text("global");

                // instance specific handlers
                foreach (KeyValuePair<object, Action<object>> handler in access
                    .InstanceSpecificHandlers)
                {
                    Imgui.Text(handler.Key.ToString());
                }

                Imgui.NextColumn();
                Imgui.Text("Handler");
                Imgui.Separator();

                // first line: global handler
                Imgui.Text(
                    access.GlobalHandler != null ?
                        access.GlobalHandler.Target + "." + access.GlobalHandler.Method.Name :
                        "-");

                // instance specific handlers
                foreach (KeyValuePair<object, Action<object>> handler in access
                    .InstanceSpecificHandlers)
                {
                    Imgui.Text(handler.Value.Target + "." + handler.Value.Method.Name);
                }

                Imgui.Columns();
                Imgui.TreePop();
#else
                DisplayDebugDisabledText();
#endif
            }

            Imgui.TreePop();
        }

        private void Begin()
        {
            Imgui.BeginMainThreadScope();
            Imgui.Begin(m_WindowTitle);
            Imgui.Text("DO NOT MOVE THIS WINDOW IN MAIN MENU! It will crash the game.");
        }

        private void AddButtons()
        {
            Imgui.NewLine();
            string startServerResult = null;
            string connectResult = null;

            Imgui.SameLine(20);
            if (Imgui.SmallButton("Close DebugUI"))
            {
                Visible = false;
            }

            Imgui.SameLine(130);
            if (Imgui.SmallButton("Toggle console"))
            {
                DebugConsole.Toggle();
            }

            if (CoopServer.Instance.Current == null)
            {
                Imgui.SameLine(250);
                if (Imgui.SmallButton("Start Server"))
                {
                    if ((startServerResult = CoopServer.Instance.StartServer()) == null)
                    {
                        ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
                        connectResult = CoopClient.Instance.Connect(config.LanAddress, config.LanPort);
                    }
                }
            }

            if (!CoopClient.Instance.ClientConnected)
            {
                Imgui.SameLine(350);
                if (Imgui.SmallButton("Connect to local"))
                {
                    ServerConfiguration defaultConfiguration = new ServerConfiguration();
                    connectResult = CoopClient.Instance.Connect(
                        defaultConfiguration.LanAddress,
                        defaultConfiguration.LanPort);
                }
            }

            if (CoopClient.Instance.ClientConnected)
            {
                Imgui.SameLine(300);
                if (Imgui.SmallButton("Disconnect"))
                {
                    CoopClient.Instance.Disconnect();
                }
            }

            if (startServerResult != null)
            {
                Logger.Warn(startServerResult);
            }

            if (connectResult != null)
            {
                Logger.Warn(connectResult);
            }
        }

        private static void DisplayEntities()
        {
            if (!Imgui.TreeNode("Parties"))
            {
                return;
            }

            if (CoopServer.Instance?.Persistence?.EntityManager == null)
            {
                Imgui.Text("No coop server running.");
            }
            else
            {
                EntityManager manager = CoopServer.Instance.Persistence.EntityManager;
                Imgui.Columns(2);
                Imgui.Separator();
                Imgui.Text("ID");
                foreach (RailEntityServer entity in manager.Parties)
                {
                    Imgui.Text(entity.Id.ToString());
                }

                Imgui.NextColumn();
                Imgui.Text("Entity");
                Imgui.Separator();
                foreach (RailEntityServer entity in manager.Parties)
                {
                    Imgui.Text(entity.ToString());
                }

                Imgui.Columns();
            }

            Imgui.TreePop();
        }

        private static void DisplayConnectionInfo()
        {
            if (!Imgui.TreeNode("Connectioninfo"))
            {
                return;
            }

            Server server = CoopServer.Instance.Current;
            GameSession session = CoopClient.Instance.Session;

            if (session.Connection == null)
            {
                Imgui.Text("Coop not running.");
            }
            else if (Imgui.TreeNode($"Client is {session.Connection.State}"))
            {
                Imgui.Columns(2);
                Imgui.Text("Ping");
                Imgui.Text($"{session.Connection.Latency}");

                Imgui.NextColumn();
                Imgui.Text("Network");
                Imgui.Separator();
                Imgui.Text(session.Connection.Network.ToString());

                Imgui.Columns();
                Imgui.TreePop();
            }

            if (server == null)
            {
                Imgui.Text("No coop server running.");
            }
            else if (Imgui.TreeNode(
                $"Server is {server.State.ToString()} with {server.ActiveConnections.Count}/{server.ActiveConfig.MaxPlayerCount} players.")
            )
            {
                if (server.ServerType == Server.EType.Threaded)
                {
                    double ticksPerFrame = server.AverageFrameTime.Ticks;
                    int tickRate = (int) (TimeSpan.TicksPerSecond / ticksPerFrame);
                    Imgui.Text($"Tickrate [Hz]: {tickRate}");
                }

                Imgui.Text(
                    $"LAN:   {server.ActiveConfig.LanAddress}:{server.ActiveConfig.LanPort}");
                Imgui.Text(
                    $"WAN:   {server.ActiveConfig.WanAddress}:{server.ActiveConfig.WanPort}");
                Imgui.Text("");

                Imgui.Columns(3);
                Imgui.Text("Ping");
                server.ActiveConnections.ForEach(c => Imgui.Text($"{c.Latency}"));

                Imgui.NextColumn();
                Imgui.Text("State");
                server.ActiveConnections.ForEach(c => Imgui.Text(c.State.ToString()));

                Imgui.NextColumn();
                Imgui.Text("Network");
                Imgui.Separator();
                server.ActiveConnections.ForEach(c => Imgui.Text(c.Network.ToString()));
                Imgui.Columns();
                Imgui.TreePop();
            }

            Imgui.Columns();
            Imgui.TreePop();
        }

        private static readonly MovingAverage m_AverageEventsInQueue = new MovingAverage(60);

        private static void DisplayClientRpcInfo()
        {
            if (!Imgui.TreeNode("Client synchronized method calls"))
            {
                return;
            }

            if (CoopClient.Instance?.Persistence?.RpcSyncHandlers == null)
            {
                Imgui.Text("Coop client not connected.");
            }
            else
            {
                RPCSyncHandlers manager = CoopClient.Instance?.Persistence?.RpcSyncHandlers;

                EventBroadcastingQueue queue = CoopServer.Instance.Environment?.EventQueue;
                if (queue != null)
                {
                    int currentQueueSize = queue.Count;
                    double avgSize = m_AverageEventsInQueue.Push(currentQueueSize);
                    Imgui.Text(
                        $"Event queue {queue.Count}/{EventBroadcastingQueue.MaximumQueueSize}.");
                    Imgui.Text(
                        $"    min {m_AverageEventsInQueue.Min} / avg {Math.Round(m_AverageEventsInQueue.Average)} / max {m_AverageEventsInQueue.Max}.");
                }

                foreach (MethodCallSyncHandler handler in manager.Handlers)
                {
                    if (!Imgui.TreeNode(handler.MethodAccess.ToString()))
                    {
                        continue;
                    }
#if DEBUG
                    Imgui.Columns(2);
                    Imgui.Separator();
                    Imgui.Text("Requested on");

                    foreach (MethodCallSyncHandler.Statistics.Trace trace in handler.Stats.History)
                    {
                        Imgui.Text(trace.Tick.ToString());
                    }

                    Imgui.NextColumn();
                    Imgui.Text("Request");
                    Imgui.Separator();
                    foreach (MethodCallSyncHandler.Statistics.Trace trace in handler.Stats.History)
                    {
                        Imgui.Text(trace.Call.ToString());
                    }

                    Imgui.Columns();
                    Imgui.TreePop();
#else
                    DisplayDebugDisabledText();
#endif
                }
            }

            Imgui.TreePop();
        }

        [Conditional("DEBUG")]
        private static void DisplayDebugDisabledText()
        {
            Imgui.Text("DEBUG is disabled. No information available.");
        }

        private static void End()
        {
            Imgui.End();
            Imgui.EndMainThreadScope();
        }

        private class SPeer
        {
            public enum EType
            {
                ClientSide,
                ServerSide
            }

            public RailPeer Peer;
            public EType Type;

            public int Slack => Peer.RemoteClock.LatestRemote - Peer.RemoteClock.EstimatedRemote;
        }
    }
}
