using Common.Serialization;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Mission.Network
{
    public class LiteNetPackager
    {
        public static NetDataWriter Pack(object obj)
        {
            NetDataWriter writer = new NetDataWriter();

            Enum protocol;

            if (IsProtoBufContract(obj))
            {
                protocol = DefaultProtocol.ProtoBuf;
            }
            else if (obj.GetType().IsSerializable)
            {
                protocol = DefaultProtocol.BinaryFormatter;
            }
            else
            {
                throw new InvalidTypeException($"{obj.GetType()} is not marked as serializable or a protobuf contract.");
            }

            writer.Put(CommonSerializer.ProtocolToId(protocol));
            writer.PutBytesWithLength(CommonSerializer.Serialize(obj, protocol));

            return writer;
        }

        public static object Unpack(NetDataReader reader)
        {
            Enum protocol = CommonSerializer.IdToProtocol(reader.GetInt());
            byte[] data = reader.GetBytesWithLength();

            return CommonSerializer.Deserialize(data, protocol);
        }


        private static bool IsProtoBufContract(object obj)
        {
            if (obj == null) return false;
            if (!obj.GetType().GetCustomAttributes(false).Contains(typeof(ProtoBuf.ProtoContractAttribute))) return false;

            return true;
        }

    }
}
