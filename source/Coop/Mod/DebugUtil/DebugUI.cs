using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using Common;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RemoteAction;
using Coop.NetImpl.LiteNet;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;
using RailgunNet.Connection;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using Registry = Sync.Registry;
using TaleWorlds.Library;
using Logger = NLog.Logger;

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
                DisplayDiscovery();
                DisplayConnectionInfo();
                DisplayHarmonyPatches();
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

        private static void DisplayHarmonyPatches()
        {
            if (!Imgui.TreeNode("Harmony patches"))
            {
                return;
            }
            
            Dictionary<MethodBase, List<MethodId>> coopPatchMethods = new Dictionary<MethodBase, List<MethodId>>();
            foreach (KeyValuePair<MethodId, MethodAccess> registrar in Registry.IdToMethod)
            {
                var key = registrar.Value.MethodBase;
                if (!coopPatchMethods.ContainsKey(key))
                {
                    coopPatchMethods[key] = new List<MethodId>();
                }
                coopPatchMethods[key].Add(registrar.Key);
            }
            
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                string sName = $"{method.DeclaringType?.Name}.{method.Name}";
                if (!Imgui.TreeNode(sName))
                {
                    continue;
                }
                
#if DEBUG
                var patches = Harmony.GetPatchInfo(method);
                ShowPatches(nameof(patches.Prefixes), patches.Prefixes);
                ShowPatches(nameof(patches.Postfixes), patches.Postfixes);
                ShowPatches(nameof(patches.Transpilers), patches.Transpilers);
                ShowPatches(nameof(patches.Finalizers), patches.Finalizers);
                
                if (coopPatchMethods.ContainsKey(method))
                {
                    if (Imgui.TreeNode("Coop synchronization"))
                    {
                        ShowCoopPatchInfo(coopPatchMethods[method]);
                        Imgui.TreePop();
                    }
                }
#else
                DisplayDebugDisabledText();
#endif
                Imgui.TreePop();
            }

            Imgui.TreePop();
        }

        private static void ShowPatches(string name, 
            ReadOnlyCollection<HarmonyLib.Patch> patches)
        {
            List<HarmonyLib.Patch> list = patches.ToList();
            if (list.Count == 0)
            {
                return;
            }
            
            if (!Imgui.TreeNode($"{name} ({list.Count})"))
            {
                return;
            }

            foreach (HarmonyLib.Patch patch in list)
            {
                string header = patch.PatchMethod.DeclaringType?.FullName;
                if (Imgui.TreeNode(header))
                {
                    const float tabWidth = 200;
                    Imgui.Text("Patch method:");
                    Imgui.SameLine(tabWidth);
                    Imgui.Text(patch.PatchMethod.Name);
                    
                    Imgui.Text("Priority:");
                    Imgui.SameLine(tabWidth);
                    Imgui.Text($"{patch.priority}");
                    
                    Imgui.Text("Owner:");
                    Imgui.SameLine(tabWidth);
                    Imgui.Text(patch.owner);
                    
                    if (patch.before.Length > 0)
                    {
                        Imgui.Text("Before:");
                        Imgui.SameLine(tabWidth);
                        Imgui.Text(string.Join(",", patch.before));
                    }
                    
                    if (patch.after.Length > 0)
                    {
                        Imgui.Text("After:");
                        Imgui.SameLine(tabWidth);
                        Imgui.Text(string.Join(",", patch.after));
                    }

                    Imgui.NewLine();
                    Imgui.TreePop();
                }
            }
            Imgui.TreePop();
        }

        private static Dictionary<SyncBuffered, int> m_LogEntrySize = new Dictionary<SyncBuffered, int>();

        private static void ShowCoopPatchInfo(List<MethodId> coopPatch)
        {
            const float indent = 50f;
            const float tabWidth = 200f;
            foreach (MethodId methodId in coopPatch)
            {
                Imgui.Text("MethodId:");
                Imgui.SameLine(tabWidth);
                Imgui.Text("" + methodId.InternalValue);

                HashSet<ValueId> relatedFields = new HashSet<ValueId>();
                if (Registry.Relation.ContainsKey(methodId))
                {
                    foreach (ValueId valueId in Registry.Relation[methodId])
                    {
                        void PrintField(ValueAccess valueAccess, float indentF = 0f)
                        {
                            relatedFields.Add(valueAccess.Id);
                            Imgui.Text("Related FieldId:");
                            Imgui.SameLine(tabWidth + indentF);
                            Imgui.Text($"{valueAccess.Id.InternalValue} [" + valueAccess + "]");
                        }

                        ValueAccess access = Registry.IdToValue[valueId];
                        PrintField(access);
                        if (access is FieldAccessGroup accessGroup)
                        {
                            foreach (FieldAccess groupMember in accessGroup.Fields)
                            {
                                PrintField(groupMember, indent);
                            }
                        }
                    }
                }
                
                Imgui.NewLine();
                
                foreach (SyncBuffered sync in SynchronizationManager.SynchronizationInstances)
                {
                    var history = sync.BroadcastHistory
                        .Where(c => (c.Call.HasValue && Equals(c.Call.Value, methodId)) ||
                                    (c.Value.HasValue && relatedFields.Contains(c.Value.Value)));
                    if (history.IsEmpty())
                    {
                        continue;
                    }
                    
                    if (!m_LogEntrySize.ContainsKey(sync))
                    {
                        m_LogEntrySize[sync] = 8;
                    }

                    int length = m_LogEntrySize[sync];
                    Imgui.Text($"History of {sync.GetType().FullName}");
                    
                    history = history
                        .Take(m_LogEntrySize[sync])
                        .ToList();

                    int iButton = 0;
                    foreach (CallTrace trace in history)
                    {
                        object instance = trace.Instance;
                        if (instance is Argument arg)
                        {
                            instance = ArgumentFactory.Resolve(CoopClient.Instance.GetStore(), arg);
                        }

                        object[] arguments = trace.Arguments;
                        if (!arguments.IsEmpty() && arguments.All(a => a is Argument))
                        {
                            arguments = ArgumentFactory.Resolve(CoopClient.Instance.GetStore(), arguments.Select(a=>(Argument) a));
                        }
                        
                        Imgui.Text($"{trace.Tick}:");
                        Imgui.SameLine(200f); 
                        if (Imgui.SmallButton($"Resend this action {iButton++}"))
                        {
                            if (trace.Call.HasValue)
                            {
                                sync.Broadcast(trace.Call.Value, instance, arguments);
                            }
                            else if (trace.Value.HasValue && arguments.Length > 0)
                            {
                                object argument = arguments[0];
                                ValueAccess access = Registry.IdToValue[trace.Value.Value];
                                FieldChangeBuffer buffer = new FieldChangeBuffer();
                                buffer.AddChange(access, new FieldData(access, instance, argument), argument);
                                sync.Broadcast(buffer);
                            }
                        }

                        
                        Imgui.Text("Instance: ");
                        Imgui.SameLine(tabWidth);
                        Imgui.Text($"{instance ?? "null"}");
                        
                        
                        Imgui.Text("Arguments: ");
                        Imgui.SameLine(tabWidth);
                        Imgui.Text($"{string.Join(",", arguments.Select(a => a.ToString()))}");
                    }
                    Imgui.InputInt($"Show more of {sync.GetType().Name}", ref length);
                    if (length > sync.BroadcastHistory.Count)
                    {
                        length = sync.BroadcastHistory.Count;
                    }
                    m_LogEntrySize[sync] = length;
                }
            }
        }

        private void Begin()
        {
            Imgui.BeginMainThreadScope();
            Imgui.Begin(m_WindowTitle);
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

            if (CoopServer.Instance.Current == null && !CoopClient.Instance.ClientConnected)
            {
                Imgui.SameLine(250);
                if (Imgui.SmallButton("Start Server"))
                {
                    if ((startServerResult = CoopServer.Instance.StartServer()) == null)
                    {
                        ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
                        connectResult = CoopClient.Instance.Connect(config.NetworkConfiguration.LanAddress, config.NetworkConfiguration.LanPort);
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
                        defaultConfiguration.NetworkConfiguration.LanAddress,
                        defaultConfiguration.NetworkConfiguration.LanPort);
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
            
            Imgui.SameLine(400);
            Imgui.Checkbox("Show whole map", ref DebugShowWholeMapPatch.IsCheatEnabled);

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

            if (CoopServer.Instance?.Persistence?.MobilePartyEntityManager == null)
            {
                Imgui.Text("No coop server running.");
            }
            else
            {
                MobilePartyEntityManager manager = CoopServer.Instance.Persistence.MobilePartyEntityManager;
                Imgui.Columns(2);
                Imgui.Separator();
                Imgui.Text("ID");
                var parties = manager.Parties.ToList();
                foreach (RailEntityServer entity in parties)
                {
                    if (entity != null)
                    {
                        Imgui.Text(entity.Id.ToString());
                    }
                }

                Imgui.NextColumn();
                Imgui.Text("Entity");
                Imgui.Separator();
                foreach (RailEntityServer entity in parties)
                {
                    Imgui.Text(entity.ToString());
                }

                Imgui.Columns();
            }

            Imgui.TreePop();
        }

        [CanBeNull] private static DiscoveryThread m_discoveryThread = null;
        private static void DisplayDiscovery()
        {
            if (!Imgui.TreeNode("LAN server discovery"))
            {
                m_discoveryThread = null;
                return;
            }

            if (m_discoveryThread == null)
            {
                m_discoveryThread = new DiscoveryThread(new NetworkConfiguration());
            }

            List<IPEndPoint> servers = m_discoveryThread.ServerList;
            if (!servers.IsEmpty())
            {
                foreach (IPEndPoint ipEndPoint in servers)
                {
                    if (Imgui.Button($"Connect to {ipEndPoint}"))
                    {
                        ServerConfiguration defaultConfiguration = new ServerConfiguration();
                        CoopClient.Instance.Connect(
                            defaultConfiguration.NetworkConfiguration.LanAddress,
                            defaultConfiguration.NetworkConfiguration.LanPort);
                    }
                }
            }
            Imgui.Text("Scanning...");

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
                Imgui.Columns(3);
                Imgui.Text("Ping");
                Imgui.Text($"{session.Connection.Latency}");

                Imgui.NextColumn();
                Imgui.Text("Network");
                Imgui.Text(session.Connection.Network.ToString());

                Imgui.NextColumn();
                Imgui.Text("Delay [campaign seconds]");
                Imgui.Text($"min {TimeSynchronization.Delay.Min} / avg {Math.Round(TimeSynchronization.Delay.Average)} / max {TimeSynchronization.Delay.Max}");
                Imgui.Separator();
                
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
                    $"LAN:   {server.ActiveConfig.NetworkConfiguration.LanAddress}:{server.ActiveConfig.NetworkConfiguration.LanPort}");
                Imgui.Text(
                    $"WAN:   {server.ActiveConfig.NetworkConfiguration.WanAddress}:{server.ActiveConfig.NetworkConfiguration.WanPort}");
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

            if (CoopClient.Instance?.Synchronization.BroadcastHistory == null)
            {
                Imgui.Text("Coop client not connected.");
            }
            else
            {
                EventBroadcastingQueue queue = CoopServer.Instance.Environment?.EventQueue;
                if (queue != null)
                {
                    int currentQueueSize = queue.Count;
                    double avgSize = m_AverageEventsInQueue.Push(currentQueueSize);
                    Imgui.Text(
                        $"Event queue {queue.Count}/{EventBroadcastingQueue.MaximumQueueSize}.");
                    Imgui.Text(
                        $"    min {m_AverageEventsInQueue.AllTimeMin} / avg {Math.Round(m_AverageEventsInQueue.Average)} / max {m_AverageEventsInQueue.AllTimeMax}.");
                }

#if DEBUG
                CallStatistics history = CoopClient.Instance?.Synchronization.BroadcastHistory;
                Imgui.Columns(2);
                
                Imgui.Text("Tick");
                foreach (CallTrace trace in history)
                {
                    Imgui.Text(trace.Tick.ToString());
                }
                
                Imgui.NextColumn();
                Imgui.Text("Call");
                foreach (CallTrace trace in history)
                {
                    Imgui.Text(trace.Call.ToString());
                }
                
                Imgui.Columns();
                
                
#else
                    DisplayDebugDisabledText();
#endif
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
        
        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch(nameof(MobileParty.SeeingRange), MethodType.Getter)]
        private static class DebugShowWholeMapPatch
        {
            public static bool IsCheatEnabled = false;
            static bool Prefix(MobileParty __instance, ref float __result)
            {
                if (IsCheatEnabled && __instance == MobileParty.MainParty)
                {
                    __result = Single.MaxValue;
                    return false;
                }
                return true;
            }
        }
        
    }
    
    
}
