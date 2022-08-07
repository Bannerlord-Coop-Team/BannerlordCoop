using System.IO;
using ProtoBuf;

namespace Coop.Serialization
{
    public class ProtobufSerializer : ISerializer
    {

        /// <summary>
        ///     Serializes the message into a byte array for sending over the network.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Byte array representing the message</returns>
        public byte[] Serialize(object message)
        {
            var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, message);
            
            return memoryStream.ToArray();
        }
        
        /// <summary>
        ///     Deserializes the message into the represented object.
        /// </summary>
        /// <typeparam name="T">Type of object the message should look like</typeparam>
        /// <returns>The object deserialized</returns>
        public T Deserialize<T>(byte[] message)
        {
            var memoryStream = new MemoryStream(message);
            return Serializer.Deserialize<T>(memoryStream);
        }
    }
}