using Common.BattleBenchmark;
using Common.Logging;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Agents.Packets;
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
    private readonly IBattleBenchmarkRecorder battleBenchmark;

    public PacketType PacketType => PacketType.CompressedMovement;

    public CompressedMovementPacketHandler(
        IPacketManager packetManager,
        IMovementPacketCompressor movementPacketCompressor,
        IBattleBenchmarkRecorder battleBenchmark)
    {
        this.packetManager = packetManager;
        this.movementPacketCompressor = movementPacketCompressor;
        this.battleBenchmark = battleBenchmark;

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

        // Counted under the packet it carries, as the sender counted it, with the bytes that arrived.
        if (battleBenchmark.IsRecording && packet is CompressedMovementPacket compressed)
            battleBenchmark.RecordTypeReceived(restored.GetType(), movementPacketCompressor.GetSerializedLength(compressed));

        packetManager.HandleReceive(peer, restored);
    }
}
