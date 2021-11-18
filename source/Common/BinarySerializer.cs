using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class CommonSerializer
    {
        public static byte[] Serialize(object obj)
        {
            using(MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static object Deserialize(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(ms);
            }
        }

        public static object Deserialize(ArraySegment<byte> bytes)
        {
            return Deserialize(bytes.Array);
        }
    }
}
