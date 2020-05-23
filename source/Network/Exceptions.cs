using System;

namespace Network
{
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
