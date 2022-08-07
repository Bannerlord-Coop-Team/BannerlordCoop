namespace Coop.Serialization
{
    public interface ISerializer
    {
        byte[] Serialize(object message);
        
        T Deserialize<T>(byte[] message);
    }
}