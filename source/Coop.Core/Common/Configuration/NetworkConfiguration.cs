using Common.Network;
using System;

namespace Coop.Core.Common.Configuration
{
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
