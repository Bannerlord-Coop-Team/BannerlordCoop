using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IntroServer.Server
{
    public class P2PPeer
    {
        public IPEndPoint InternalAddr { get; }
        public IPEndPoint ExternalAddr { get; }
        public DateTime RefreshTime { get; private set; }
        public NetPeer NetPeer { get; }

        public void Refresh()
        {
            RefreshTime = DateTime.UtcNow;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj is P2PPeer other == false) return false;

            return InternalAddr.Equals(other.InternalAddr) && ExternalAddr.Equals(other.ExternalAddr);
        }

        public override int GetHashCode()
        {
            int hashCode = -1359847852;
            hashCode = hashCode * -1521134295 + EqualityComparer<IPEndPoint>.Default.GetHashCode(InternalAddr);
            hashCode = hashCode * -1521134295 + EqualityComparer<IPEndPoint>.Default.GetHashCode(ExternalAddr);
            return hashCode;
        }

        public P2PPeer(NetPeer peer, IPEndPoint internalAddr, IPEndPoint externalAddr)
        {
            NetPeer = peer;
            Refresh();
            InternalAddr = internalAddr;
            ExternalAddr = externalAddr;
        }
    }
}
