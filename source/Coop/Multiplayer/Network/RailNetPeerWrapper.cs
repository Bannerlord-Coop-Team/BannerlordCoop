using System;
using System.Diagnostics;
using Coop.Network;
using RailgunNet.Connection.Traffic;

namespace Coop.Multiplayer.Network
{
    public class RailNetPeerWrapper : IRailNetPeer, IGameStatePersistence
    {
        private readonly INetworkConnection m_Connection;

        public RailNetPeerWrapper(INetworkConnection connection)
        {
            m_Connection = connection;
        }

        public void Receive(ArraySegment<byte> buffer)
        {
            AssertIsPersistencePayload(buffer);
            PayloadReceived?.Invoke(
                this,
                new ArraySegment<byte>(buffer.Array, buffer.Offset + 1, buffer.Count - 1));
        }

        public event EventHandler OnDisconnected;

        [Conditional("DEBUG")]
        private static void AssertIsPersistencePayload(ArraySegment<byte> buffer)
        {
            if (buffer.Array == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            Protocol.EPacket eType = PacketReader.DecodePacketType(buffer.Array[buffer.Offset]);
            if (eType != Protocol.EPacket.Persistence)
            {
                throw new ArgumentException(nameof(buffer));
            }
        }

        public void Disconnect()
        {
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        #region IRailNetPeer
        public object PlayerData { get; set; }

        public float? Ping => m_Connection.Latency;

        public event RailNetPeerEvent PayloadReceived;

        public void SendPayload(ArraySegment<byte> buffer)
        {
            // TODO: Remove this copy
            byte[] toSend = new byte[buffer.Count + 1];
            toSend[0] = PacketWriter.EncodePacketType(Protocol.EPacket.Persistence);
            Array.Copy(buffer.Array, buffer.Offset, toSend, 1, buffer.Count);
            m_Connection.SendRaw(new ArraySegment<byte>(toSend));
        }
        #endregion
    }
}
