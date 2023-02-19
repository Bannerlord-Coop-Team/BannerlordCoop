using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Tests.Extensions
{
    internal static class NetPeerExtensions
    {
        private static readonly FieldInfo Id = typeof(NetPeer).GetField(nameof(NetPeer.Id));
        public static void SetId(this NetPeer peer, int id)
        {
            Id.SetValue(peer, id);
        }
    }
}
