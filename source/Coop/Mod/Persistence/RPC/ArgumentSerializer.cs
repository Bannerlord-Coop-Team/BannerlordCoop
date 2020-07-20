using System;
using RailgunNet.System.Encoding;
using Sync.Store;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Serializer for <see cref="Argument" />. It's important to keep in mind, that the
    ///     serialized payload may never exceed <see cref="RailgunNet.RailConfig.MAXSIZE_EVENT" />!
    /// </summary>
    public static class ArgumentSerializer
    {
        private static int NumberOfBitsForArgType => GetNumberOfBitsForArgType();

        private static int GetNumberOfBitsForArgType()
        {
            int numberOfValues = Enum.GetNames(typeof(EventArgType)).Length;
            return Convert.ToInt32(Math.Ceiling(Math.Log(numberOfValues, 2)));
        }

        [Encoder]
        public static void EncodeEventArg(this RailBitBuffer buffer, Argument arg)
        {
            buffer.Write(NumberOfBitsForArgType, Convert.ToByte(arg.EventType));
            switch (arg.EventType)
            {
                case EventArgType.MBObject:
                    buffer.WriteMBGUID(arg.MbGUID.Value);
                    break;
                case EventArgType.Null:
                    // Empty
                    break;
                case EventArgType.MBObjectManager:
                    // Empty
                    break;
                case EventArgType.Int:
                    buffer.WriteInt(arg.Int.Value);
                    break;
                case EventArgType.Float:
                    buffer.WriteUInt(
                        BitConverter.ToUInt32(BitConverter.GetBytes(arg.Float.Value), 0));
                    break;
                case EventArgType.StoreObjectId:
                    buffer.WriteUInt(arg.StoreObjectId.Value.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [Decoder]
        public static Argument DecodeEventArg(this RailBitBuffer buffer)
        {
            EventArgType eType = (EventArgType) buffer.Read(NumberOfBitsForArgType);
            switch (eType)
            {
                case EventArgType.MBObject:
                    return new Argument(buffer.ReadMBGUID());
                case EventArgType.MBObjectManager:
                    return Argument.MBObjectManager;
                case EventArgType.Null:
                    return Argument.Null;
                case EventArgType.Int:
                    return new Argument(buffer.ReadInt());
                case EventArgType.Float:
                    uint ui = buffer.ReadUInt();
                    float f = BitConverter.ToSingle(BitConverter.GetBytes(ui), 0);
                    return new Argument(f);
                case EventArgType.StoreObjectId:
                    return new Argument(new ObjectId(buffer.ReadUInt()));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
