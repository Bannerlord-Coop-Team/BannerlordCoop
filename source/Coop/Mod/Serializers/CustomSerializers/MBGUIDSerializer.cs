using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class MBGUIDSerializer : ICustomSerializer
    {
        private uint id;
        public MBGUIDSerializer(MBGUID _MBGUID)
        {
            id = _MBGUID.InternalValue;
        }

        public byte[] Serialize()
        {
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(id);
            return writer.ToArray();
        }

        public static MBGUID Deserialize(ByteReader reader)
        {
            uint id = reader.Binary.ReadUInt32();
            return new MBGUID(id);
        }

        public object Deserialize()
        {
            return new MBGUID(id);
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}
