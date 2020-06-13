using System;
using System.Collections.Generic;
using System.Diagnostics;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Network.Infrastructure;
using RailgunNet.Logic;
using Sync;
using TaleWorlds.Engine;

namespace Coop.Mod.DebugUtil
{
    public class DebugUI : IUpdateable
    {
        private readonly string m_WindowTitle = "";

        public bool Visible { get; set; }

        public void Update(TimeSpan frameTime)
        {
            if (Visible)
            {
                Begin();
                AddButtons();
                DisplayConnectionInfo();
                DisplayMethodRegistry();
                DisplayClientRpcInfo();
                DisplayEntities();
                End();
            }
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
            Imgui.Text("DO NOT MOVE THIS WINDOW! It will crash the game.");
        }

        private void AddButtons()
        {
            if (Imgui.SmallButton("Close"))
            {
                Visible = false;
            }

            if (Imgui.SmallButton("Toggle console"))
            {
                DebugConsole.Toggle();
            }

            if (CoopServer.Instance.Current == null)
            {
                Imgui.SameLine(200);
                if (Imgui.SmallButton("Start Server"))
                {
                    CoopServer.Instance.StartServer();
                    ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
                    CoopClient.Instance.Connect(config.LanAddress, config.LanPort);
                }
            }

            if (!CoopClient.Instance.Connected)
            {
                Imgui.SameLine(400);
                if (Imgui.SmallButton("Connect to local host"))
                {
                    ServerConfiguration defaultConfiguration = new ServerConfiguration();
                    CoopClient.Instance.Connect(
                        defaultConfiguration.LanAddress,
                        defaultConfiguration.LanPort);
                }
            }
        }

        private static void DisplayEntities()
        {
            if (!Imgui.TreeNode("Persistence: Parties"))
            {
                Imgui.TreePop();
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
            }

            Imgui.TreePop();
        }

        private static void DisplayConnectionInfo()
        {
            if (Imgui.TreeNode("Connection info"))
            {
                Imgui.Text(CoopServer.Instance.ToString());
                Imgui.Text(CoopClient.Instance.ToString());
                Imgui.TreePop();
            }
        }

        private static void DisplayClientRpcInfo()
        {
            if (!Imgui.TreeNode("Persistence: client synchronized method calls"))
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
    }
}
