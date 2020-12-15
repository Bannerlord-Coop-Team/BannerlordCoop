using SimpleTCP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BannerlordSystemTestingLibrary
{
    public class GameInstance : IDisposable
    {
        GameProcess process;

        public bool Running => process.Running;
        public int PID => process.GetPID();
        
        public Message PIDMsg
        {
            get { return _PIDMsg; } 
            set
            {
                if (_PIDMsg == null)
                {
                    _PIDMsg = value;
                }
                else
                {
                    throw new Exception("Client already assigned");
                }
            } 
        }

        public GameInstance(string args)
        {
            process = new GameProcess(args);
        }

        public GameInstance(Process p)
        {
            process = new GameProcess(p);
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            process.Start();

            // Wait for menu to be ready
        }

        public void Stop()
        {
            process.Kill();
        }

        public void SendCommand(string command)
        {
            if (attached)
            {
                PIDMsg.ReplyLine($"COMMAND {command}");
            }
        }

        public Task Attach()
        {
            return Task.Run(() =>
            {
                while (!attached)
                {
                    Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            });
        }

        #region private
        private Message _PIDMsg;
        private bool attached => _PIDMsg != null;
        #endregion
    }
}
