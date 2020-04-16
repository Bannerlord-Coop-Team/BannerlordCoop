using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer
{
    public class InvalidStateException : Exception
    {
        public InvalidStateException(string msg) : base(msg)
        {
        }
    }
    public class InvalidNetworkPackageException : Exception
    {
        public InvalidNetworkPackageException(string msg) : base(msg)
        {
        }
    }
    public class NetworkConnectionFailedException : Exception
    {
        public NetworkConnectionFailedException(string msg) : base(msg)
        {
        }
    }
}
