using System;
using System.Diagnostics;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Network.Infrastructure;
using RailgunNet.Logic;
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
                DisplayClientRpcInfo();
                DisplayEntities();
                End();
            }
        }

        private void Begin()
        {
            Imgui.BeginMainThreadScope();
            Imgui.Begin(m_WindowTitle);
            Text("DO NOT MOVE THIS WINDOW! It will crash the game.");
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

        private void Text(string sText)
        {
            Imgui.Text(sText);
        }

        private void DisplayEntities()
        {
            if (!Imgui.TreeNode("Persistence: Parties"))
            {
                Imgui.TreePop();
                return;
            }

            if (CoopServer.Instance?.Persistence?.EntityManager == null)
            {
                Text("No coop server running.");
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

        private void DisplayConnectionInfo()
        {
            if (Imgui.TreeNode("Connection info"))
            {
                Imgui.Text(CoopServer.Instance.ToString());
                Imgui.Text(CoopClient.Instance.ToString());
                Imgui.TreePop();
            }
        }

        private void DisplayClientRpcInfo()
        {
            if (!Imgui.TreeNode("Persistence: client synchronized method calls"))
            {
                return;
            }

            if (CoopClient.Instance?.Persistence?.RpcSyncHandlers == null)
            {
                Text("Coop client not connected.");
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
#else
                    DisplayDebugDisabledText();
#endif

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
                }
            }

            Imgui.TreePop();
        }

        [Conditional("DEBUG")]
        private void DisplayDebugDisabledText()
        {
            Imgui.Text("DEBUG is disabled. No information available.");
        }

        private void End()
        {
            Imgui.End();
            Imgui.EndMainThreadScope();
        }
    }
}
