using Common.Logging;
using Common.PacketHandlers;
using LiteNetLib;
using Serilog;

namespace Missions.Services.Network;

/// <summary>
/// Restores compressed movement packets before dispatching them to the mission packet handlers.
/// </summary>
public class CompressedMovementPacketHandler : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CompressedMovementPacketHandler>();

    private readonly IPacketManager packetManager;
    private readonly IMovementPacketCompressor movementPacketCompressor;

    public PacketType PacketType => PacketType.CompressedMovement;

    public CompressedMovementPacketHandler(
        IPacketManager packetManager,
        IMovementPacketCompressor movementPacketCompressor)
    {
        this.packetManager = packetManager;
        this.movementPacketCompressor = movementPacketCompressor;

        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (!movementPacketCompressor.TryRestore(packet, out IPacket restored))
        {
            Logger.Warning("Discarding an invalid compressed movement packet");
            return;
        }

        packetManager.HandleReceive(peer, restored);
    }
}
