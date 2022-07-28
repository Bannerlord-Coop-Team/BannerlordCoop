using System;

namespace Common.Serialization
{
    public interface ISerializer
    {
        Enum Protocol { get; }

        byte[] Serialize(object obj);
        object Deserialize(byte[] data);

    }
}