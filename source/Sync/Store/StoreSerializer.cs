using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Common;
using Common.Serialization;
using JetBrains.Annotations;

namespace Sync.Store
{
    public class StoreSerializer
    {
        public StoreSerializer([CanBeNull] ISerializableFactory factory)
        {
            Factory = factory;
        }

        public ISerializableFactory Factory { get; }

        public byte[] Serialize(object obj)
        {
            var serializable = Factory != null ? Factory.Wrap(obj) : obj;
            return CommonSerializer.Serialize(serializable);
        }

        public object Deserialize(byte[] raw)
        {
            using(MemoryStream buffer = new MemoryStream(raw))
            {
                return Factory == null
                   ? new BinaryFormatter().Deserialize(buffer)
                   : Factory.Unwrap(new BinaryFormatter().Deserialize(buffer));
            }
            
        }
    }
}