using System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace Coop.Mod.Persistence.RPC
{
    public static class ArgumentSerializer
    {
        [Encoder]
        public static void EncodeEventArg(this RailBitBuffer buffer, Argument arg)
        {
            buffer.Write(3, Convert.ToByte(arg.EventType));
            switch (arg.EventType)
            {
                case EventArgType.EntityReference:
                    buffer.WriteEntityId(arg.RailId.Value);
                    break;
                case EventArgType.MBGUID:
                    buffer.WriteMBGUID(arg.MbGUID.Value);
                    break;
                case EventArgType.Null:
                    // Empty
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [Decoder]
        public static Argument DecodeEventArg(this RailBitBuffer buffer)
        {
            EventArgType eType = (EventArgType) buffer.Read(3);
            switch (eType)
            {
                case EventArgType.EntityReference:
                    return new Argument(buffer.ReadEntityId());
                case EventArgType.MBGUID:
                    return new Argument(buffer.ReadMBGUID());
                case EventArgType.Null:
                    return Argument.Null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
