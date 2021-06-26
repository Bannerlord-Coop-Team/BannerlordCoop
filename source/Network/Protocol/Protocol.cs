using Network.Infrastructure;
using System;

namespace Network.Protocol
{
    public enum EPacket : byte
    {
        Client_Hello, // Introduces the client to the server.
        Client_Info, // Contains ClientInfo.
        Client_RequestWorldData, // Client wants to be sent a save game of the current state.
        Client_RequestParty, // Client wants to know if they already created a party.
        Client_DeclineWorldData, // Client does not need world data
        Client_Loaded, // Sent once the client has loaded the initial world state.
        Client_PartyChanged, // When the player party is switched

        Server_RequestClientInfo, // Instructs the client to send its ClientInfo.
        Server_JoinRequestAccepted, // Client is allowed to join the server.
        Server_RequireCharacterCreation, // Instructs the client to create a character.
        Server_NotifyCharacterExists, // Notifies the client a party already exists for that player id.
        Server_WorldData, // Contains the initial state of the game world.

        Sync,
        StoreAdd, // Adds an object to the global object store
        StoreAck, // Sent after receiving an object via StoreAdd
        KeepAlive,
        Persistence // Will be forwarded to the game state persistence layer.
    }

    public static class Version
    {
        public const int Number = 0;
    }

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

    public class Client_Request_Party
    {
        public readonly string m_ClientId;

        public Client_Request_Party(string clientId)
        {
            m_ClientId = clientId;
        }

        public byte[] Serialize()
        {
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(m_ClientId);
            return writer.ToArray();
        }

        public static Client_Request_Party Deserialize(ByteReader reader)
        {
            return new Client_Request_Party(reader.Binary.ReadString());
        }
    }

    public class Client_RequestWorldData
    {
        public byte[] Serialize()
        {
            // Empty
            return new byte[0];
        }

        public static Client_RequestWorldData Deserialize(ByteReader reader)
        {
            // Empty
            return new Client_RequestWorldData();
        }
    }

    public class Client_DeclineWorldData
    {
        public byte[] Serialize()
        {
            // Empty
            return new byte[0];
        }

        public static Client_RequestWorldData Deserialize(ByteReader reader)
        {
            // Empty
            return new Client_RequestWorldData();
        }
    }

    public class Client_GameLoading
    {
        public byte[] Serialize()
        {
            // Empty
            return new byte[0];
        }

        public static Client_GameLoading Deserialize(ByteReader reader)
        {
            // Empty
            return new Client_GameLoading();
        }
    }

    public class Client_GameLoaded
    {
        public byte[] Serialize()
        {
            // Empty
            return new byte[0];
        }

        public static Client_GameLoaded Deserialize(ByteReader reader)
        {
            // Empty
            return new Client_GameLoaded();
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

    public class Server_RequireCharacterCreation
    {
        public byte[] Serialize()
        {
            // Empty
            return new byte[0];
        }

        public static Server_RequireCharacterCreation Deserialize(ByteReader reader)
        {
            // Empty
            return new Server_RequireCharacterCreation();
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
}
