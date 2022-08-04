using System;
using System.IO;
using System.Linq;
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
            using(var stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(id);
                return stream.ToArray();
            }
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
