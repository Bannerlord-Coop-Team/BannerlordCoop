using Coop.Mod.Persistence.Party;
using RailgunNet.System.Encoding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence
{
    public class MovementData : IEnumerable<object>
    {
        public MovementData()
        {
            Values = new List<object>();
            Values.Add(AiBehavior.None);
            Values.Add(null);
            Values.Add(null);
            Values.Add(Vec2.Invalid);
            Values.Add(0);
        }

        public MovementData(IEnumerable<object> collection)
        {
            Values = collection.ToList();
        }

        private static Type[] Types { get; } =
        {
            typeof(AiBehavior),
            typeof(Settlement),
            typeof(MobileParty),
            typeof(Vec2),
            typeof(int)
        };

        public AiBehavior DefaultBehaviour
        {
            get => (AiBehavior)Values[(int)Field.DefaultBehavior];
            set => Values[(int)Field.DefaultBehavior] = value;
        }

        public Settlement TargetSettlement
        {
            get => (Settlement)Values[(int)Field.TargetSettlement];
            set => Values[(int)Field.TargetSettlement] = value;
        }

        public MobileParty TargetParty
        {
            get => (MobileParty)Values[(int)Field.TargetParty];
            set => Values[(int)Field.TargetParty] = value;
        }

        public Vec2 TargetPosition
        {
            get => (Vec2)Values[(int)Field.TargetPosition];
            set => Values[(int)Field.TargetPosition] = value;
        }

        public int NumberOfFleeingsAtLastTravel
        {
            get => (int)Values[(int)Field.NumberOfFleeingsAtLastTravel];
            set => Values[(int)Field.NumberOfFleeingsAtLastTravel] = value;
        }

        private List<object> Values { get; }

        public bool NearlyEquals(MovementData other)
        {
            return this.DefaultBehaviour == other.DefaultBehaviour &&
                this.TargetPosition.NearlyEquals(other.TargetPosition) &&
                this.TargetParty?.Id == other.TargetParty?.Id &&
                this.TargetSettlement?.Id == other.TargetSettlement?.Id;
        }

        public IEnumerator<object> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        private enum Field
        {
            DefaultBehavior = 0,
            TargetSettlement = 1,
            TargetParty = 2,
            TargetPosition = 3,
            NumberOfFleeingsAtLastTravel = 4
        }
    }

    public static class MovementDataSerializer
    {
        [Encoder]
        public static void WriteMovementData(this RailBitBuffer buffer, MovementData movementData)
        {
            buffer.WriteByte((byte)movementData.DefaultBehaviour);
            buffer.WriteMBGUID(movementData.TargetSettlement != null ? movementData.TargetSettlement.Id : MovementState.InvalidIndex);
            buffer.WriteMBGUID(movementData.TargetParty != null ? movementData.TargetParty.Id : MovementState.InvalidIndex);
            MovementStateSerializer.CoordinateCompressor.WriteVec2(buffer, movementData.TargetPosition);
            buffer.WriteInt(movementData.NumberOfFleeingsAtLastTravel);
        }

        [Decoder]
        public static MovementData ReadMovementData(this RailBitBuffer buffer)
        {
            MBGUID id;
            return new MovementData
            {
                DefaultBehaviour = (AiBehavior)buffer.ReadByte(),
                TargetSettlement = (id = buffer.ReadMBGUID()) != MovementState.InvalidIndex ? (Settlement)MBObjectManager.Instance.GetObject(id) : null,
                TargetParty = (id = buffer.ReadMBGUID()) != MovementState.InvalidIndex ? (MobileParty)MBObjectManager.Instance.GetObject(id) : null,
                TargetPosition = MovementStateSerializer.CoordinateCompressor.ReadVec2(buffer),
                NumberOfFleeingsAtLastTravel = buffer.ReadInt()
            };
        }
    }
}
