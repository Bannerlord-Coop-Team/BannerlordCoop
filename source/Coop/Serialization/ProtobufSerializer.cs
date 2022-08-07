using System;
using System.IO;
using Coop.Serialization.Models;
using ProtoBuf;
using ProtoBuf.Meta;
using TaleWorlds.ObjectSystem;

namespace Coop.Serialization
{
    public class ProtobufSerializer : ISerializer
    {
        private readonly RuntimeTypeModel _model;

        public ProtobufSerializer()
        {
            _model = RuntimeTypeModel.Create();
            _model.Add<MBObjectBase>().SetSurrogate(typeof(MBObjectSurrogate));
            
            _model.CompileInPlace();
        }
        
        /// <summary>
        ///     Serializes the message into a byte array for sending over the network.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Byte array representing the message</returns>
        public byte[] Serialize(object message)
        {
            var memoryStream = new MemoryStream();
            _model.Serialize(memoryStream, message);
            
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
            return _model.Deserialize<T>(memoryStream);
        }
    }
}