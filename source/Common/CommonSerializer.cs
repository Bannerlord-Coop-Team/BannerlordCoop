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
        static readonly BinaryFormatter formatter = new BinaryFormatter();
        public static byte[] Serialize(object obj)
        {
            using(MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        private static object DeserializeBytes(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(ms);
            }
        }

        public static T Deserialize<T>(ArraySegment<byte> bytes)
        {
            return (T)Deserialize(bytes.Array);
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            return (T)DeserializeBytes(bytes);
        }

        public static object Deserialize(ArraySegment<byte> bytes)
        {
            return Deserialize(bytes.Array);
        }

        public static object Deserialize(byte[] bytes)
        {
            return DeserializeBytes(bytes);
        }
    }
}
