using SimpleTCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BannerlordSystemTestingLibrary
{
    class ProtocolRegisterException : Exception
    {
        public ProtocolRegisterException(string message) : base(message)
        {
        }
    }
    class ProtocolRegistry
    {
        protected readonly Dictionary<TcpClient, List<string>> registeredCommands = new Dictionary<TcpClient, List<string>>();
        protected readonly string REGISTER_COMMAND = "REGISTER";
        public void ParseAndRegisterCommand(Message message)
        {
            if (message.MessageString.StartsWith(REGISTER_COMMAND))
            {
                string formattedMsg = message.MessageString.Remove(0, REGISTER_COMMAND.Length + 1);
                string[] args = formattedMsg.Split(' ');
                if (args.Length != 1)
                {
                    throw new ProtocolRegisterException($"Register Arguments are incorrect length. Expected length of 1 but got {args.Length}.");
                }
                else if (args[0] == string.Empty)
                {
                    throw new ProtocolRegisterException($"Command was an empty string.");
                }
                RegisterCommand(message.TcpClient, args[0]);
            }
        }
        public void RegisterCommand(TcpClient user, string command)
        {
            if (registeredCommands.ContainsKey(user))
            {
                if (registeredCommands[user].Contains(command))
                {
                    throw new ProtocolRegisterException($"{command} has already been registered.");
                }
                registeredCommands[user].Add(command);
            }
            else
            {
                // First time register
                registeredCommands.Add(user, new List<string>() { command });

                // Remove all registerd
                //user.SetOnDisconnect((context) => { registeredCommands.Remove(context); });
            }
        }
        public bool InvokeCommand(TcpClient user, string command, string[] args = null)
        {
            if (registeredCommands.ContainsKey(user) && registeredCommands[user].Contains(command))
            {
                byte[] data = Encoding.UTF8.GetBytes(command + ' ' + string.Join(" ", args) + "!!");
                user.GetStream().Write(data, 0, data.Length);
                return true;
            }
            return false;
        }
    }
}
