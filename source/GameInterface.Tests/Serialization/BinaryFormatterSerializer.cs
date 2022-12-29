using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace GameInterface.Tests.Serialization
{
    public class BinaryFormatterSerializer
    {        
        private static BinaryFormatter BinaryFormatter = new BinaryFormatter();

        public static byte[] Serialize(object obj)
        {
            using(MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter.Serialize(ms, obj);

                return ms.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            return (T)Deserialize(bytes);
        }

        public static object Deserialize(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                return BinaryFormatter.Deserialize(ms);
            }
        }
    }
}
