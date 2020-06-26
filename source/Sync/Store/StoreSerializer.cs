using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Sync.Store
{
    public static class StoreSerializer
    {
        public static byte[] Serialize(object obj)
        {
            IFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, obj);
            return stream.ToArray();
        }

        public static object Deserialize(byte[] raw)
        {
            MemoryStream buffer = new MemoryStream(raw);
            return new BinaryFormatter().Deserialize(buffer);
        }
    }
}
