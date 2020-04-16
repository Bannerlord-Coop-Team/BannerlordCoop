using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Network
{
    public class PacketSendException : Exception
    {
        public PacketSendException(string msg) : base(msg)
        {
        }
    }
    public class PacketSerializingException : Exception
    {
        public PacketSerializingException(string msg) : base(msg)
        {
        }
    }
    public class InvalidStateException : Exception
    {
        public InvalidStateException(string msg) : base(msg)
        {
        }
    }
    public class InvalidServerConfiguration : Exception
    {
        public InvalidServerConfiguration(string msg) : base(msg)
        {
        }
    }
    public class MissingPacketHandlerAttributeException : Exception
    {
        public MissingPacketHandlerAttributeException(string msg) : base(msg)
        {
        }
    }
    public class DuplicatePacketHandlerRegistration : Exception
    {
        public DuplicatePacketHandlerRegistration(string msg) : base(msg)
        {
        }
    }
}
