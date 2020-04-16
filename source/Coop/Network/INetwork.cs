using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Network
{
    public interface INetwork
    {
        bool Connect();
        void Disconnect();
        bool IsConnected { get; }
    }
}
