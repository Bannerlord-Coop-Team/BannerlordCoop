using System;
using Coop.Mod;

namespace Coop.Configuration
{
    public class ServerConfiguration : INetworkConfiguration
    {
        public string Address => throw new NotImplementedException();

        public int Port => throw new NotImplementedException();

        public string Token => throw new NotImplementedException();

        public string P2PToken => throw new NotImplementedException();

        public void LoadFromFile()
        {
            throw new NotImplementedException();
        }
    }
}
