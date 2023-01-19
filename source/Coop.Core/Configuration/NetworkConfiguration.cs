using System;
using Coop.Mod;

namespace Coop.Core.Configuration
{
    public interface INetworkConfiguration
    {
        string Address { get; }
        int Port { get; }
        string Token { get; }
        string P2PToken { get; }
    }

    public class NetworkConfiguration : INetworkConfiguration
    {
        public string Address => "localhost";

        public int Port => 4200;

        // TODO find better token
        public string Token => "TempToken";

        public string P2PToken => throw new NotImplementedException();

        public void LoadFromFile()
        {
            throw new NotImplementedException();
        }
    }
}
