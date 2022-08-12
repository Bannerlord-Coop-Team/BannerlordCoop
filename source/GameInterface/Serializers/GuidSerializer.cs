using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class GuidSerializer : CustomSerializer
    {
        string _guid;

        public GuidSerializer(Guid guid)
        {
            _guid = guid.ToString();
        }

        //public object FromByteArray(ArraySegment<byte> bytes)
        //{
        //    ByteReader reader = new ByteReader(bytes);
        //    reader.readst
        //}

        //public object FromByteArray(byte[] bytes)
        //{
            
        //    using (MemoryStream stream = new MemoryStream(bytes))
        //    {
        //        using ((stream))
        //        {
        //            reader.ReadString()
        //        }
        //    }
        //}

        public override object Deserialize()
        {
            return Guid.Parse(_guid);
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}
