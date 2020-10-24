using System;
using System.IO;
using JetBrains.Annotations;
using Network.Protocol;

namespace Network.Infrastructure
{
    /// <summary>
    ///     A connection represents state and logic for data exchange between a client and a server.
    ///     - State management & transitions are to be implemented by inherting classes.
    ///     - Physical data exchange is implemented in a <see cref="INetworkConnection" />.
    /// </summary>
    public abstract class ConnectionBase
    {
        public readonly PacketDispatcher Dispatcher;

        /// <summary>
        /// </summary>
        /// <param name="network">Networking implementation.</param>
        /// <param name="persistence">Implementation to relay Persistance packets to.</param>
        protected ConnectionBase(
            [NotNull] INetworkConnection network,
            [NotNull] IGameStatePersistence persistence)
        {
            Dispatcher = new PacketDispatcher();
            Network = network ?? throw new ArgumentNullException(nameof(network));
            GameStatePersistence =
                persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        #region Send and Receive
        public IGameStatePersistence GameStatePersistence { get; }

        public void Send(Packet packet)
        {
            if (!State.Equals(EClientConnectionState.Disconnected))
            {
                if (packet.Length > Network.MaxPackageLength)
                {
                    throw new PacketSendException(
                        $"Payload for package {packet.Type} too large ({packet.Length}>{Network.MaxPackageLength}).");
                }

                new PacketWriter(packet).Write(new BinaryWriter(m_Stream));
                Network.SendRaw(
                    new ArraySegment<byte>(m_Stream.ToArray()),
                    EDeliveryMethod.Reliable);
                m_Stream.SetLength(0);
            }
        }

        public void SendFragmented(Packet packet)
        {
            if (!State.Equals(EClientConnectionState.Disconnected))
            {
                if (packet.Length > Network.MaxPackageLength)
                {
                    throw new PacketSendException(
                        $"Payload for package {packet.Type} too large ({packet.Length}>{Network.MaxPackageLength}).");
                }

                BinaryWriter writer = new BinaryWriter(m_Stream);
                PacketWriter packetWriter = new PacketWriter(packet);
                while (!packetWriter.Done)
                {
                    packetWriter.Write(writer, Network.FragmentLength);
                    Network.SendRaw(
                        new ArraySegment<byte>(m_Stream.ToArray()),
                        EDeliveryMethod.Reliable);
                    m_Stream.SetLength(0);
                }
            }
        }

        public void Receive(ArraySegment<byte> buffer)
        {
            if (State.Equals(EClientConnectionState.Disconnected)) return;

            EPacket eType = PacketReader.DecodePacketType(buffer.Array[buffer.Offset]);
            if (eType == EPacket.Persistence)
            {
                GameStatePersistence.Receive(buffer);
            }
            else
            {
                if (m_PackageReader == null)
                {
                    m_PackageReader = new PacketReader();
                }

                Packet packet = m_PackageReader.Read(new ByteReader(buffer));
                if (packet != null)
                {
                    m_PackageReader = null;
                    Dispatcher.Dispatch(this, packet);
                }
            }
        }
        #endregion

        #region State & transitions
        public INetworkConnection Network { get; }
        public abstract Enum State { get; }
        public int Latency = 0;

        /// <summary>
        ///     Closes the connection.
        ///     Postcondition: <see cref="State" /> is equal to <see cref="EConnectionState.Disconnected" />.
        /// </summary>
        /// <param name="eReason">Reason that caused the disconnect.</param>
        public abstract void Disconnect(EDisconnectReason eReason);

        public override string ToString()
        {
            return $"{Latency,-5}{State,-30}{Network}";
        }
        #endregion

        #region Internals
        private readonly MemoryStream m_Stream = new MemoryStream();
        private PacketReader m_PackageReader;
        #endregion
    }
}
