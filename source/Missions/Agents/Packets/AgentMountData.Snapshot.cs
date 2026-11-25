using Common.Serialization;
using System;
using System.IO;
using System.Text;

namespace Missions.Agents.Packets
{
    public partial class AgentMountData
    {
        private const int GuidBytes = 16;
        private const byte HasScope = 1;
        private const byte HasGuid = 2;
        private const byte HasRevision = 4;
        private const byte IsLocomotion = 8;
        private const byte IsSyntheticTurn = 16;

        internal int GetSnapshotExtraBytes()
        {
            int length = 0;
            if (MountIdentityScopeId != null)
            {
                // BinaryWriter prefixes strings with their UTF-8 byte count as a 7-bit integer.
                int utf8Length = Encoding.UTF8.GetByteCount(MountIdentityScopeId);
                length += ProtoBufSerializer.GetVarintLength((uint)utf8Length) + utf8Length;
            }
            if (MountAgentId != Guid.Empty) length += GuidBytes;
            if (MountAuthorityRevision.HasValue) length += sizeof(long);
            return length;
        }

        internal const int SnapshotBytes =
            sizeof(byte) +                      // flags
            MovementSnapshotColumns.Vec3Bytes + // MountPosition
            MovementSnapshotColumns.Vec2Bytes + // MountInputVector
            MovementSnapshotColumns.Vec3Bytes + // MountLookDirection
            MovementSnapshotColumns.Vec2Bytes + // MountMovementDirection
            sizeof(float) +                     // MountSpeed
            sizeof(uint) +                      // MountMovementFlag
            sizeof(ushort) +                    // MountMovementId
            sizeof(int) +                       // MountAction0Index
            sizeof(ulong) +                     // MountAction0Flag
            sizeof(float) +                     // MountAction0Progress
            sizeof(float) +                     // MountAction0Speed
            sizeof(int) +                       // MountAction0TurnDirection
            sizeof(int) +                       // MountAction0TurnActionIndexWire
            sizeof(int) +                       // MountAction1IndexWire
            sizeof(ulong) +                     // MountAction1Flag
            sizeof(float);                      // MountAction1Progress

        internal void WriteSnapshot(ref MovementSnapshotColumns columns, BinaryWriter extras)
        {
            byte flags = 0;
            if (MountIdentityScopeId != null)
            {
                flags |= HasScope;
                extras.Write(MountIdentityScopeId);
            }
            if (MountAgentId != Guid.Empty)
            {
                flags |= HasGuid;
                extras.Write(MountAgentId.ToByteArray());
            }
            if (MountAuthorityRevision.HasValue)
            {
                flags |= HasRevision;
                extras.Write(MountAuthorityRevision.Value);
            }
            if (MountAction0IsLocomotion) flags |= IsLocomotion;
            if (MountAction0IsSyntheticTurn) flags |= IsSyntheticTurn;

            columns.WriteByte(flags);
            columns.WriteVec3(MountPosition);
            columns.WriteVec2(MountInputVector);
            columns.WriteVec3(MountLookDirection);
            columns.WriteVec2(MountMovementDirection);
            columns.WriteSingle(MountSpeed);
            columns.WriteUInt32(MountMovementFlag);
            columns.WriteUInt16(MountMovementId);
            columns.WriteInt32(MountAction0Index);
            columns.WriteUInt64(MountAction0Flag);
            columns.WriteSingle(MountAction0Progress);
            columns.WriteSingle(MountAction0Speed);
            columns.WriteInt32(MountAction0TurnDirection);
            columns.WriteInt32(MountAction0TurnActionIndexWire);
            columns.WriteInt32(MountAction1IndexWire);
            columns.WriteUInt64(MountAction1Flag);
            columns.WriteSingle(MountAction1Progress);
        }

        internal static AgentMountData ReadSnapshot(ref MovementSnapshotColumns columns, BinaryReader extras)
        {
            byte flags = columns.ReadByte();
            // Read extras, then the remaining columns, in WriteSnapshot order.
            string scopeId = null;
            if ((flags & HasScope) != 0) scopeId = extras.ReadString();
            Guid agentId = Guid.Empty;
            if ((flags & HasGuid) != 0) agentId = new Guid(extras.ReadBytes(GuidBytes));
            long? revision = null;
            if ((flags & HasRevision) != 0) revision = extras.ReadInt64();

            return new AgentMountData
            {
                MountPosition = columns.ReadVec3(),
                MountInputVector = columns.ReadVec2(),
                MountLookDirection = columns.ReadVec3(),
                MountMovementDirection = columns.ReadVec2(),
                MountSpeed = columns.ReadSingle(),
                MountMovementFlag = columns.ReadUInt32(),
                MountMovementId = columns.ReadUInt16(),
                MountAction0Index = columns.ReadInt32(),
                MountAction0Flag = columns.ReadUInt64(),
                MountAction0Progress = columns.ReadSingle(),
                MountAction0Speed = columns.ReadSingle(),
                MountAction0TurnDirection = columns.ReadInt32(),
                MountAction0TurnActionIndexWire = columns.ReadInt32(),
                MountAction1IndexWire = columns.ReadInt32(),
                MountAction1Flag = columns.ReadUInt64(),
                MountAction1Progress = columns.ReadSingle(),
                MountIdentityScopeId = scopeId,
                MountAgentId = agentId,
                MountAuthorityRevision = revision,
                MountAction0IsLocomotion = (flags & IsLocomotion) != 0,
                MountAction0IsSyntheticTurn = (flags & IsSyntheticTurn) != 0,
            };
        }
    }
}
