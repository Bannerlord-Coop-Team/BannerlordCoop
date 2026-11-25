using TaleWorlds.Library;

namespace Missions.Agents.Packets
{
    public partial struct AgentData
    {
        internal const int SnapshotBytes =
            MovementSnapshotColumns.Vec3Bytes +
            MovementSnapshotColumns.Vec2Bytes +
            MovementSnapshotColumns.Vec3Bytes +
            MovementSnapshotColumns.Vec2Bytes +
            sizeof(float) +
            sizeof(uint);

        internal void WriteSnapshot(ref MovementSnapshotColumns columns)
        {
            columns.WriteVec3(Position);
            columns.WriteVec2(InputVector);
            columns.WriteVec3(LookDirection);
            columns.WriteVec2(MovementDirection);
            columns.WriteSingle(Speed);
            columns.WriteUInt32(MovementFlag);
        }

        // Read each column in the order used by WriteSnapshot.
        internal static AgentData ReadSnapshot(
            ref MovementSnapshotColumns columns,
            (int ShipIndex, Vec3 Local) deck,
            AgentMountData mount) => new AgentData
        {
            Position = columns.ReadVec3(),
            InputVector = columns.ReadVec2(),
            LookDirection = columns.ReadVec3(),
            MovementDirection = columns.ReadVec2(),
            Speed = columns.ReadSingle(),
            MovementFlag = columns.ReadUInt32(),
            DeckShipIndex = deck.ShipIndex,
            DeckLocal = deck.Local,
            MountData = mount,
        };

        internal const int DeckSnapshotBytes = sizeof(int) + MovementSnapshotColumns.Vec3Bytes;

        internal void WriteDeckSnapshot(ref MovementSnapshotColumns columns)
        {
            columns.WriteInt32(DeckShipIndex);
            columns.WriteVec3(DeckLocal);
        }

        internal static (int ShipIndex, Vec3 Local) ReadDeckSnapshot(ref MovementSnapshotColumns columns)
        {
            int shipIndex = columns.ReadInt32();
            Vec3 local = columns.ReadVec3();
            return (shipIndex, local);
        }
    }
}
