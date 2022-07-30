using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Common.Serialization
{


    internal class BinaryFormatterSerializer : ISerializer
    {
        static readonly BinaryFormatter formatter = new BinaryFormatter();

        public Enum Protocol => SerializationMethod.BinaryFormatter;

        public byte[] Serialize(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public object Deserialize(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return formatter.Deserialize(ms);
            }
        }
    }
}
