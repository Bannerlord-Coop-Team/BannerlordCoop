using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RailgunNet.System.Encoding;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Contains all data relevant to a movement command using local game objects.
    /// </summary>
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
            get => (AiBehavior) Values[(int) Field.DefaultBehavior];
            set => Values[(int) Field.DefaultBehavior] = value;
        }

        public Settlement TargetSettlement
        {
            get => (Settlement) Values[(int) Field.TargetSettlement];
            set => Values[(int) Field.TargetSettlement] = value;
        }

        public MobileParty TargetParty
        {
            get => (MobileParty) Values[(int) Field.TargetParty];
            set => Values[(int) Field.TargetParty] = value;
        }

        public Vec2 TargetPosition
        {
            get => (Vec2) Values[(int) Field.TargetPosition];
            set => Values[(int) Field.TargetPosition] = value;
        }

        public int NumberOfFleeingsAtLastTravel
        {
            get => (int) Values[(int) Field.NumberOfFleeingsAtLastTravel];
            set => Values[(int) Field.NumberOfFleeingsAtLastTravel] = value;
        }

        private List<object> Values { get; }

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

        public override bool Equals(Object obj)
        {
            if (!(obj is MovementData))
            {
                return false;
            }

            MovementData other = (MovementData)obj;
            return DefaultBehaviour == other.DefaultBehaviour &&
                   TargetPosition.NearlyEquals(other.TargetPosition, Compare.COORDINATE_PRECISION) &&
                   TargetParty?.Id == other.TargetParty?.Id &&
                   TargetSettlement?.Id == other.TargetSettlement?.Id;
        }

        private enum Field
        {
            DefaultBehavior = 0,
            TargetSettlement = 1,
            TargetParty = 2,
            TargetPosition = 3,
            NumberOfFleeingsAtLastTravel = 4
        }

        public override string ToString()
        {
            return $"{TargetPosition}, {TargetParty}, {TargetSettlement}, {DefaultBehaviour}, {NumberOfFleeingsAtLastTravel}";
        }

        public bool IsValid()
        {
            bool bRequiresTargetParty = DefaultBehaviour == AiBehavior.EngageParty ||
                                        DefaultBehaviour == AiBehavior.EscortParty ||
                                        DefaultBehaviour == AiBehavior.JoinParty ||
                                        DefaultBehaviour == AiBehavior.GoAroundParty;
            if (bRequiresTargetParty && TargetParty == null)
            {
                return false;
            }

            bool bRequiresTargetSettlement = DefaultBehaviour == AiBehavior.AssaultSettlement ||
                                             DefaultBehaviour == AiBehavior.BesiegeSettlement ||
                                             DefaultBehaviour == AiBehavior.DefendSettlement ||
                                             DefaultBehaviour == AiBehavior.RaidSettlement ||
                                             DefaultBehaviour == AiBehavior.GoToSettlement;
            if (bRequiresTargetSettlement && TargetSettlement == null)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     Railgun encoder & decoder for the movement data.
    /// </summary>
    public static class MovementDataSerializer
    {
        [Encoder]
        public static void WriteMovementData(this RailBitBuffer buffer, MovementData movementData)
        {
            buffer.WriteByte((byte) movementData.DefaultBehaviour);
            buffer.WriteMBGUID(
                movementData.TargetSettlement != null ?
                    movementData.TargetSettlement.Id :
                    MovementState.InvalidIndex);
            buffer.WriteMBGUID(
                movementData.TargetParty != null ?
                    movementData.TargetParty.Id :
                    MovementState.InvalidIndex);
            MovementStateSerializer.CoordinateCompressor.WriteVec2(
                buffer,
                movementData.TargetPosition);
            buffer.WriteInt(movementData.NumberOfFleeingsAtLastTravel);
        }

        [Decoder]
        public static MovementData ReadMovementData(this RailBitBuffer buffer)
        {
            MBGUID id;
            return new MovementData
            {
                DefaultBehaviour = (AiBehavior) buffer.ReadByte(),
                TargetSettlement = (id = buffer.ReadMBGUID()) != MovementState.InvalidIndex ?
                    (Settlement) MBObjectManager.Instance.GetObject(id) :
                    null,
                TargetParty = (id = buffer.ReadMBGUID()) != MovementState.InvalidIndex ?
                    (MobileParty) MBObjectManager.Instance.GetObject(id) :
                    null,
                TargetPosition = MovementStateSerializer.CoordinateCompressor.ReadVec2(buffer),
                NumberOfFleeingsAtLastTravel = buffer.ReadInt()
            };
        }
    }
}
