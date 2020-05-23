using Common;
using System;
using Coop.Mod.Persistence;
using Network.Infrastructure;
using RailgunNet.Logic;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Coop.Mod.DebugUtil
{
    public class DebugUI : IUpdateable
    {
        private readonly string m_WindowTitle = "";

        public bool Visible { get; set; } = false;

        public void Update(TimeSpan frameTime)
        {
            if (Visible)
            {
                Begin();
                AddButtons();
                DisplayConnectionInfo();
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
                    CoopClient.Instance.Connect(defaultConfiguration.LanAddress, defaultConfiguration.LanPort);
                }
            }
        }

        private void Text(string sText)
        {
            Imgui.Text(sText);
        }

        private void DisplayEntities()
        {
            if (!Imgui.TreeNode("Persistence: Parties")) return;

            if (CoopServer.Instance?.Persistence?.EntityManager == null)
            {
                Text("No coop server running.");
            }
            else
            {
                EntityManager manager = CoopServer.Instance.Persistence.EntityManager;
                Imgui.Columns(3);
                Imgui.Separator();
                Imgui.Text("ID");
                foreach (RailEntityServer entity in manager.Parties)
                {
                    Imgui.Text(entity.Id.ToString());
                }

                Imgui.NextColumn();
                foreach (RailEntityServer entity in manager.Parties)
                {
                    Imgui.Text(entity.GetType().ToString());
                }

                Imgui.NextColumn();
                Imgui.Text("Entity");
                foreach (RailEntityServer entity in manager.Parties)
                {
                    Imgui.Text(entity.ToString());
                }
                Imgui.Separator();
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

        private void End()
        {
            Imgui.End();
            Imgui.EndMainThreadScope();
        }
    }
}
