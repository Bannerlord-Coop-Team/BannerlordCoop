using System;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldState : RailState<WorldState>
    {
        public World Data { get; }

        public WorldState(IEnvironment env)
        {
            Data = new World(env);
        }

        protected override void ResetAllData()
        {
            Data.Reset();
        }

        #region Mutable
        [Flags]
        private enum Field : uint
        {
            None = 0x0,
            TimeControlMode = 0x01,
            All = 0xFF
        }

        protected override int FlagBits => 3;

        protected override void ApplyMutableFrom(WorldState source, uint uField)
        {
            Field flag = (Field) uField;
            if (flag.HasFlag(Field.TimeControlMode))
                Data.TimeControlMode = source.Data.TimeControlMode;
        }

        protected override uint CompareMutableData(WorldState other)
        {
            return (uint) (Data.TimeControlMode == other.Data.TimeControlMode_LastWritten ?
                Field.None :
                Field.TimeControlMode);
        }

        protected override void DecodeMutableData(RailBitBuffer buffer, uint uField)
        {
            Field flag = (Field) uField;
            if (flag.HasFlag(Field.TimeControlMode))
                Data.TimeControlMode = (CampaignTimeControlMode) buffer.ReadByte();
        }

        protected override void EncodeMutableData(RailBitBuffer buffer, uint uField)
        {
            Field flag = (Field) uField;
            if (flag.HasFlag(Field.TimeControlMode))
                buffer.WriteByte(Convert.ToByte(Data.TimeControlMode));
        }
        #endregion

        #region Immutable
        protected override void ApplyImmutableFrom(WorldState source)
        {
        }

        protected override void DecodeImmutableData(RailBitBuffer buffer)
        {
        }

        protected override void EncodeImmutableData(RailBitBuffer buffer)
        {
        }
        #endregion

        #region Controller
        protected override void ApplyControllerFrom(WorldState source)
        {
        }

        protected override void DecodeControllerData(RailBitBuffer buffer)
        {
        }

        protected override void EncodeControllerData(RailBitBuffer buffer)
        {
        }

        protected override bool IsControllerDataEqual(WorldState basis)
        {
            return true;
        }

        protected override void ResetControllerData()
        {
        }
        #endregion
    }
}
