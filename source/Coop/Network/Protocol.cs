using System.IO;

namespace Coop.Network
{
    public static class Protocol
    {
        public enum EPacket : byte
        {
            Client_Hello,               // Introduces the client to the server.
            Client_Info,                // Contains ClientInfo.
            Client_Joined,              // Sent once the client has loaded the initial world state.

            Server_RequestClientInfo,   // Instructs the client to send its ClientInfo.
            Server_JoinRequestAccepted, // Client is allowed to join the server.
            Server_WorldData,           // Contains the initial state of the game world.

            Sync,
            KeepAlive
        }
        public const int Version = 0;

        #region Client payload serializers
        public class Client_Hello
        {
            public readonly int m_Version;
            public Client_Hello(int version)
            {
                m_Version = version;
            }
            public byte[] Serialize()
            {
                ByteWriter writer = new ByteWriter();
                writer.Binary.Write(m_Version);
                return writer.ToArray();
            }

            public static Client_Hello Deserialize(ByteReader reader)
            {
                return new Client_Hello(reader.Binary.ReadInt32());
            }
        }
        public class Client_Info
        {
            public readonly Player m_Player;
            public Client_Info(Player player)
            {
                m_Player = player;
            }
            public byte[] Serialize()
            {
                ByteWriter writer = new ByteWriter();
                writer.Binary.Write(m_Player.Name);
                return writer.ToArray();
            }

            public static Client_Info Deserialize(ByteReader reader)
            {
                return new Client_Info(new Player(reader.Binary.ReadString()));
            }
        }
        public class Client_Joined
        {
            public byte[] Serialize()
            {
                // Empty
                return new byte[0];
            }

            public static Client_Joined Deserialize(ByteReader reader)
            {
                // Empty
                return new Client_Joined();
            }
        }
        #endregion

        #region Server payload serializers
        public class Server_RequestClientInfo
        {
            public byte[] Serialize()
            {
                // Empty
                return new byte[0];
            }

            public static Server_RequestClientInfo Deserialize(ByteReader reader)
            {
                // Empty
                return new Server_RequestClientInfo();
            }
        }
        public class Server_JoinRequestAccepted
        {
            public byte[] Serialize()
            {
                // Empty
                return new byte[0];
            }

            public static Server_JoinRequestAccepted Deserialize(ByteReader reader)
            {
                // Empty
                return new Server_JoinRequestAccepted();
            }
        }
        #endregion

        public class KeepAlive
        {
            public readonly int m_iKeepAliveID;
            public KeepAlive(int iKeepAliveID)
            {
                m_iKeepAliveID = iKeepAliveID;
            }
            public byte[] Serialize()
            {
                ByteWriter writer = new ByteWriter();
                writer.Binary.Write(m_iKeepAliveID);
                return writer.ToArray();
            }

            public static KeepAlive Deserialize(ByteReader reader)
            {
                return new KeepAlive(reader.Binary.ReadInt32());
            }
        }
    }
}
